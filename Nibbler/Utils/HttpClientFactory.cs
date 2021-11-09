using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Nibbler.Utils
{
    public class HttpClientFactory
    {
        private readonly ILogger _logger;

        public HttpClientFactory(ILogger logger)
        {
            _logger = logger;
        }

        public HttpClient Create()
        {
            return Create(null);
        }

        public HttpClient Create(Uri baseUri)
        {
            return Create(baseUri, false);
        }

        public HttpClient Create(Uri baseUri, bool skipTlsVerify, params DelegatingHandler[] additionalHandlers)
        {
            var primaryHandler = new HttpClientHandler();
            primaryHandler.UseCookies = true;
            if (skipTlsVerify)
            {
                primaryHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            HttpMessageHandler handler = primaryHandler;

            if (_logger != null)
            {
                handler = new LoggingHandler(handler, _logger);
            }

            if (additionalHandlers != null)
            {
                foreach (var handerToAdd in additionalHandlers)
                {
                    if (handerToAdd != null)
                    {
                        handerToAdd.InnerHandler = handler;
                        handler = handerToAdd;
                    }
                }
            }

            var client = new HttpClient(handler);
            if (baseUri != null)
            {
                client.BaseAddress = baseUri;
            }

            return client;
        }
    }
}
