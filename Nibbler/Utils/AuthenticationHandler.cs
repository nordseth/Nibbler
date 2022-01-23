﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nibbler.Utils
{
    public class AuthenticationHandler : DelegatingHandler
    {
        private const string defaultClientID = "registry-client";

        private readonly string _registry;
        private readonly IDockerConfigCredentials _dockerConfigCredentials;
        private readonly bool _push;
        private readonly bool _forceOAuth;
        private readonly string _clientId;
        private readonly ILogger _logger;
        private readonly HttpClient _tokenClient;

        private AuthenticationHeaderValue _authorization;
        private string _refreshToken;
        private string _scope;
        private string _username;
        private string _password;

        public AuthenticationHandler(
            string registry,
            IDockerConfigCredentials dockerConfigCredentials,
            bool push,
            ILogger logger,
            HttpClient tokenClient,
            bool forceOauth = false,
            string clientId = null)
        {
            _registry = registry;
            _dockerConfigCredentials = dockerConfigCredentials;
            _push = push;
            _logger = logger;
            _tokenClient = tokenClient;
            _forceOAuth = forceOauth;
            _clientId = clientId;
        }

        public void SetCredentials(string username, string password)
        {
            _username = username;
            _password = password;
        }

        internal bool HasCredentials()
        {
            return _username != null && _password != null;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_authorization != null)
            {
                request.Headers.Authorization = _authorization;
            }

            var response = await base.SendAsync(request, cancellationToken);
            if ((response.StatusCode != System.Net.HttpStatusCode.Unauthorized
                    && response.StatusCode != System.Net.HttpStatusCode.Forbidden)
                || !response.Headers.WwwAuthenticate.Any())
            {
                return response;
            }

            var wwwAuth = response.Headers.WwwAuthenticate.First();
            _logger.LogDebug($"Unauthorized, trying to authenticate {wwwAuth}");
            if (!await TrySetAuthorization(wwwAuth))
            {
                return response;
            }
            else
            {
                // try again, but with authorization set 
                // maybe we should have a check here to avoid endless recursion
                return await SendAsync(request, cancellationToken);
            }
        }

        private async Task<bool> TrySetAuthorization(AuthenticationHeaderValue wwwAuth)
        {
            if (_authorization == null && wwwAuth.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
            {
                return TrySetBasicAuth();
            }

            if (wwwAuth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return await TrySetOAuth(wwwAuth.Parameter);
            }

            return false;
        }

        private async Task<bool> TrySetOAuth(string parameter)
        {
            var authParams = AuthParamParser.Parse(parameter);
            authParams.TryGetValue("scope", out var scope);

            // don't update unless we are asked for higher access
            if (_authorization != null && scope == _scope)
            {
                return false;
            }

            if (scope != null)
            {
                scope = TryUpdateScope(scope);
                _scope = scope;
            }

            AuthConfig tokenCredentials;
            if (HasCredentials())
            {
                tokenCredentials = new AuthConfig
                {
                    username = _username,
                    password = _password,
                };
            }
            else
            {
                tokenCredentials = _dockerConfigCredentials?.GetCredentials(_registry);
            }

            authParams.TryGetValue("service", out string service);
            authParams.TryGetValue("realm", out string realm);

            TokenResponse tokenResponse;
            if (_refreshToken != null || tokenCredentials?.identityToken != null || _forceOAuth)
            {
                tokenResponse = await FetchOAuthToken(realm, tokenCredentials, service, scope);
            }
            else
            {
                tokenResponse = await FetchBasicAuthToken(realm, tokenCredentials, service, scope);
            }

            _authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.token ?? tokenResponse.access_token);
            _logger.LogDebug($"Using Bearer token for {_registry} ({realm}, {service}, {scope})");
            if (tokenResponse.refresh_token != null)
            {
                _refreshToken = tokenResponse.refresh_token;
                _logger.LogDebug($"Set refresh token");
            }
            _logger.LogTrace($"Setting authorization to: {_authorization}");
            return true;
        }

        private string TryUpdateScope(string scope)
        {
            if (_push)
            {
                var resourceScope = ResourceScope.TryParse(scope);
                if (resourceScope != null && resourceScope.IsPullOnly())
                {
                    resourceScope.SetPullPush();
                    scope = resourceScope.ToString();
                    _logger.LogDebug($"Adding push to scope: {scope}");
                }
            }

            return scope;
        }

        // https://github.com/docker/cli/blob/6e2838e18645e06f3e4b6c5143898ccc44063e3b/vendor/github.com/docker/distribution/registry/client/auth/session.go#L323
        private async Task<TokenResponse> FetchOAuthToken(string realm, AuthConfig authConfig, string service, string scopes)
        {
            var form = new Dictionary<string, string>
            {
                ["scope"] = scopes,
                ["service"] = service,
                ["client_id"] = _clientId ?? defaultClientID,
            };

            if (_refreshToken != null || authConfig?.identityToken != null)
            {
                form["grant_type"] = "refresh_token";
                form["refresh_token"] = _refreshToken ?? authConfig.identityToken;
            }
            else if (authConfig?.HasUsernamePassword() ?? false)
            {
                form["grant_type"] = "password";
                form["username"] = authConfig?.username;
                form["password"] = authConfig?.password;
                form["access_type"] = "offline";
            }
            else
            {
                throw new InvalidOperationException("no supported grant type");
            }

            using var response = await _tokenClient.PostAsync(realm, new FormUrlEncodedContent(form));
            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(content);
            return tokenResponse;
        }

        private async Task<TokenResponse> FetchBasicAuthToken(string realm, AuthConfig authConfig, string service, string scopes)
        {
            Uri realmUri = new Uri(realm);
            var queryString = new QueryString(realmUri.Query);

            if (service != null)
            {
                queryString = queryString.Add("service", service);
            }

            if (scopes != null)
            {
                queryString = queryString.Add("scope", scopes);
            }

            // todo: support for refresh tokens here

            var request = new HttpRequestMessage(HttpMethod.Get, $"{realmUri.GetLeftPart(UriPartial.Path)}{queryString}");
            if (authConfig != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", EncodeBasicCredentials(authConfig));
            }

            using var response = await _tokenClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(content);
            return tokenResponse;
        }

        private bool TrySetBasicAuth()
        {
            string credentials;
            if (HasCredentials())
            {
                credentials = EncodeBasicCredentials(_username, _password);
            }
            else
            {
                var authConfig = _dockerConfigCredentials?.GetCredentials(_registry);
                credentials = EncodeBasicCredentials(authConfig);
            }

            if (credentials != null)
            {
                _authorization = new AuthenticationHeaderValue("Basic", credentials);
                _logger.LogDebug($"Using Basic auth for {_registry}");
                _logger.LogTrace($"Setting authorization to: {_authorization}");
                return true;
            }

            return false;
        }

        public static string EncodeBasicCredentials(string username, string password)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        }

        public static string EncodeBasicCredentials(AuthConfig authConfig)
        {
            if (authConfig?.auth != null)
            {
                return authConfig.auth;
            }

            if (authConfig?.username != null && authConfig?.password != null)
            {
                return EncodeBasicCredentials(authConfig.username, authConfig.password);
            }

            return null;
        }

        private class TokenResponse
        {
            public string token { get; set; }
            public string refresh_token { get; set; }
            public string access_token { get; set; }
            public string expires_in { get; set; }
            public string scope { get; set; }
        }
    }
}
