using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Models;

namespace Nibbler.Test
{
    [TestClass]
    public class RegistryTest
    {
        [TestMethod]
        [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "3.1")]
        [DataRow("http://localhost:5000", "hello-world", "latest")]
        public async Task Registry_Get_ManifestFile(string registryUrl, string imageName, string imageRef)
        {
            var registry = new Registry(new Uri(registryUrl));

            var manifest = await registry.GetManifestFile(imageName, imageRef);
            Console.WriteLine(manifest);
        }

        [TestMethod]
        [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "3.1")]
        [DataRow("http://localhost:5000", "hello-world", "latest")]
        public async Task Registry_Get_Manifest(string registryUrl, string imageName, string imageRef)
        {
            var registry = new Registry(new Uri(registryUrl));

            var manifest = await registry.GetManifest(imageName, imageRef);
            Assert.IsNotNull(manifest);
            Assert.IsNotNull(manifest.layers);
            Assert.IsTrue(manifest.layers.Any());
            Assert.IsNotNull(manifest.layers.First().digest);
            Assert.IsNotNull(manifest.config);
            Assert.IsNotNull(manifest.config.digest);
            try
            {
                Assert.AreEqual(ImageV1.MimeType, manifest.config.mediaType);
            }
            catch
            {
                Assert.AreEqual(ImageV1.AltMimeType, manifest.config.mediaType);
            }
        }

        [TestMethod]
        [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "sha256:930743cb4e197dc01a680b604464724ad1344a07b395e9871482ef05dbd25950")]
        [DataRow("http://localhost:5000", "hello-world", "sha256:fce289e99eb9bca977dae136fbe2a82b6b7d4c372474c9235adc1741675f587e")]
        public async Task Registry_Get_ImageFile(string registryUrl, string imageName, string digest)
        {
            var registry = new Registry(new Uri(registryUrl));

            var image = await registry.GetImageFile(imageName, digest);
            Console.WriteLine(image);
        }

        [TestMethod]
        [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "sha256:930743cb4e197dc01a680b604464724ad1344a07b395e9871482ef05dbd25950")]
        [DataRow("http://localhost:5000", "hello-world", "sha256:fce289e99eb9bca977dae136fbe2a82b6b7d4c372474c9235adc1741675f587e")]
        public async Task Registry_Get_Image(string registryName, string imageName, string digest)
        {
            var registry = new Registry(new Uri(registryName));

            var image = await registry.GetImage(imageName, digest);
            Assert.IsNotNull(image);
            Assert.IsNotNull(image.config);
            Assert.IsNotNull(image.rootfs);
            Assert.IsNotNull(image.rootfs.diff_ids);
            Assert.IsTrue(image.rootfs.diff_ids.Any());
        }
    }
}
