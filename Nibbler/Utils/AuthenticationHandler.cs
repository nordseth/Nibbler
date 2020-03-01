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
        private readonly CredentialHelper _credentialHelper;
        private readonly ILogger _logger;
        private readonly HttpClient _tokenClient;

        private AuthenticationHeaderValue authorization;

        public AuthenticationHandler(string registry, CredentialHelper credentialHelper, ILogger logger)
        {
            _registry = registry;
            _credentialHelper = credentialHelper;
            _logger = logger;
            _tokenClient = new HttpClient();
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
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _credentialHelper.GetEncodedCredentials(_registry));
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
            var credentials = _credentialHelper.GetEncodedCredentials(_registry);

            if (credentials != null)
            {
                authorization = new AuthenticationHeaderValue("Basic", credentials);
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
