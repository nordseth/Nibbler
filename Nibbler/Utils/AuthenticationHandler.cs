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
        private readonly string _registry;
        private readonly CredentialHelper _credentialHelper;
        private readonly ILogger _logger;
        private readonly HttpClient _tokenClient;

        private AuthenticationHeaderValue _authorization;
        private string _scope;

        public AuthenticationHandler(string registry, CredentialHelper credentialHelper, ILogger logger)
        {
            _registry = registry;
            _credentialHelper = credentialHelper;
            _logger = logger;
            _tokenClient = new HttpClient();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_authorization != null)
            {
                request.Headers.Authorization = _authorization;
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized
                || !response.Headers.WwwAuthenticate.Any())
            {
                return response;
            }

            var wwwAuth = response.Headers.WwwAuthenticate.First();
            _logger.LogDebug($"Failed to authenticate {wwwAuth}");
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

            var queryString = $"service={ authParams["service"]}";
            if (scope != null)
            {
                queryString += $"&scope={scope}";
                _scope = scope;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"{authParams["realm"]}?{queryString}");
            var tokenCredentials = _credentialHelper?.GetEncodedCredentials(_registry);
            if (tokenCredentials != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", tokenCredentials);
            }

            var response = await _tokenClient.SendAsync(request);

            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(content);

            _authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.token ?? tokenResponse.access_token);
            _logger.LogDebug($"Using Bearer token for {_registry} ({queryString})");
            return true;
        }

        private bool TrySetBasicAuth()
        {
            var credentials = _credentialHelper?.GetEncodedCredentials(_registry);

            if (credentials != null)
            {
                _authorization = new AuthenticationHeaderValue("Basic", credentials);
                _logger.LogDebug($"Using Basic auth for {_registry}");
                return true;
            }

            return false;
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