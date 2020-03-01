using Newtonsoft.Json;
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
        private readonly string _dockerConfig;
        private readonly bool _push;
        private readonly ILogger _logger;
        private readonly HttpClient _tokenClient;

        private string _username;
        private string _password;
        private AuthenticationHeaderValue authorization;

        public AuthenticationHandler(string registry, string dockerConfig, bool push, ILogger logger)
        {
            _registry = registry;
            _dockerConfig = dockerConfig;
            _push = push;
            _logger = logger;
            _tokenClient = new HttpClient();
        }

        public void SetUsernamePassword(string username, string password)
        {
            _username = username;
            _password = password;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (authorization != null)
            {
                request.Headers.Authorization = authorization;
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized
                || !response.Headers.WwwAuthenticate.Any())
            {
                return response;
            }

            var wwwAuth = response.Headers.WwwAuthenticate.First();
            _logger.LogDebug($"Failed to authenticate {wwwAuth}");
            if (authorization != null || !await TrySetAuthorization(wwwAuth))
            {
                return response;
            }
            else
            {
                // recurse, but with authorization set
                return await SendAsync(request, cancellationToken);
            }
        }

        private async Task<bool> TrySetAuthorization(AuthenticationHeaderValue wwwAuth)
        {
            if (wwwAuth.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase))
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

            var queryString = $"service={ authParams["service"]}";
            if (authParams.TryGetValue("scope", out var scope))
            {
                queryString += $"&scope={scope}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"{authParams["realm"]}?{queryString}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", GetEncodedCredentials());
            var response = await _tokenClient.SendAsync(request);

            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(content);

            authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.token ?? tokenResponse.access_token);
            _logger.LogDebug($"Using Bearer token for {_registry} ({queryString})");
            return true;
        }

        private bool TrySetBasicAuth()
        {
            var credentials = GetEncodedCredentials();

            if (credentials != null)
            {
                authorization = new AuthenticationHeaderValue("Basic", credentials);
                _logger.LogDebug($"Using Basic auth for {_registry}");
                return true;
            }

            return false;
        }

        private string GetEncodedCredentials()
        {
            if (_username != null && _password != null)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            }

            var authConfig = GetDockerConfigAuth(_registry, _dockerConfig);

            if (authConfig?.auth != null)
            {
                return authConfig.auth;
            }

            if (authConfig?.username != null && authConfig?.password != null)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authConfig.username}:{authConfig.password}"));
            }

            return null;
        }

        private static DockerConfigAuth GetDockerConfigAuth(string registry, string dockerConfigFile)
        {
            var configPath = dockerConfigFile;
            if (string.IsNullOrEmpty(configPath))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configPath = $"{home}/.docker/config.json";
            }

            DockerConfig config;
            try
            {
                var dockerConfigJson = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<DockerConfig>(dockerConfigJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not load docker config \"{configPath}\"", ex);
            }

            if (config.auths != null && config.auths.TryGetValue(registry, out var auth))
            {
                return auth;
            }

            throw new Exception($"Could find credentials for {registry} in \"{configPath}\"");
        }

        private class DockerConfig
        {
            public Dictionary<string, DockerConfigAuth> auths { get; set; }
        }

        private class DockerConfigAuth
        {
            public string username { get; set; }
            public string password { get; set; }
            public string auth { get; set; }
            public string email { get; set; }
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
