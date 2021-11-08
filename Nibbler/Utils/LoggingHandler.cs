using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nibbler.Utils
{
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;

        public LoggingHandler(HttpMessageHandler innerHandler, ILogger logger)
            : base(innerHandler)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger.LogTrace($"Request: {request.Method} {request.RequestUri}");
            foreach (var h in request.Headers)
            {
                _logger.LogTrace($"  {h.Key}={string.Join(", ", h.Value)}");
            }

            var stopwatch = Stopwatch.StartNew();

            var response = await base.SendAsync(request, cancellationToken);

            _logger.LogTrace($"Response: {(int)response.StatusCode} in {stopwatch.ElapsedMilliseconds} ms");
            foreach (var h in response.Headers)
            {
                _logger.LogTrace($"  {h.Key}={string.Join(", ", h.Value)}");
            }

            return response;
        }
    }
}
