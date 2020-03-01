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

        private string _username;
        private string _password;
        private AuthenticationHeaderValue authorization;

        public AuthenticationHandler(string registry, string dockerConfig, bool push, ILogger logger)
        {
            _registry = registry;
            _dockerConfig = dockerConfig;
            _push = push;
            _logger = logger;
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
            if (authorization != null
                || response.StatusCode != System.Net.HttpStatusCode.Unauthorized
                || !response.Headers.WwwAuthenticate.Any())
            {
                return response;
            }

            var wwwAuth = response.Headers.WwwAuthenticate.First();
            if (!TrySetAuthorization(wwwAuth))
            {
                return response;
            }
            else
            {
                // recurse, but with authorization set
                return await SendAsync(request, cancellationToken);
            }
        }

        private bool TrySetAuthorization(AuthenticationHeaderValue wwwAuth)
        {
            if (!wwwAuth.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)
                && !wwwAuth.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // todo: only if basic
            if (_username != null && _password != null)
            {
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
                authorization = new AuthenticationHeaderValue(wwwAuth.Scheme, creds);
                _logger.LogDebug($"Using {wwwAuth.Scheme} with username/password parameters for {_registry}");
                return true;
            }

            var authConfig = GetDockerConfigAuth(_registry, _dockerConfig);

            // todo: check bearer parameters
            //   should call out to "realm" token endpoint here
            //   if auth set, we assume its a bearer token?

            if (authConfig.auth != null)
            {
                authorization = new AuthenticationHeaderValue(wwwAuth.Scheme, authConfig.auth);
                _logger.LogDebug($"Using {wwwAuth.Scheme} with auth from config for {_registry}");
                return true;
            }

            if (_username != null && _password != null)
            {
                var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
                authorization = new AuthenticationHeaderValue(wwwAuth.Scheme, creds);
                _logger.LogDebug($"Using {wwwAuth.Scheme} with username/password from config for {_registry}");
                return true;
            }

            // or should we throw an exception here?
            return false;
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
    }
}
