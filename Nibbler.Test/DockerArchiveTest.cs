using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Utils;
using Nibbler.Writer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Test
{
    [TestClass]
    public class DockerArchiveTest
    {
        private readonly Logger _logger;
        private readonly Logger _registryLogger;
        private readonly HttpClientFactory _httpClientFactory;

        public DockerArchiveTest()
        {
            _logger = new Logger("ARCHIVE", true, true);
            _registryLogger = new Logger("REGISTRY", true, true);
            var httpLogger = new Utils.Logger("HTTPCLIENT", true, false);
            _httpClientFactory = new HttpClientFactory(httpLogger);
        }

        [TestMethod]
        [DataRow("http://localhost:5000", "hello-world", "latest")]
        public async Task Docker_Archive_Write(string registryUrl, string imageName, string imageRef)
        {
            var registry = new Registry(_registryLogger, _httpClientFactory.Create(new Uri(registryUrl)));
            var imageSource = new RegistryImageSource(imageName, imageRef, registry, _registryLogger);

            var image = await imageSource.LoadImage();

            var writer = new DockerArchiveWriter("test.tar", _logger);
            await writer.WriteImage(image, s => imageSource.GetBlob(s));
        }
    }
}
