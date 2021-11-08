using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Test
{
    [TestClass]
    public class ImageDataTest
    {
        private readonly Utils.Logger _registryLogger;

        public ImageDataTest()
        {
            _registryLogger = new Utils.Logger("REGISTRY", true, true);
        }

        [TestMethod]
        [DataRow("http://localhost:5000", "hello-world", "latest")]
        public async Task Image_LoadMetadata(string registryUrl, string imageName, string imageRef)
        {
            var registry = new Registry(new Uri(registryUrl), _registryLogger, null);
            var imageSource = new RegistryImageSource(imageName, imageRef, registry, _registryLogger);

            var image = await imageSource.LoadImageMetadata();

            Assert.IsNotNull(image.ManifestBytes);
            Assert.IsNotNull(image.ManifestDigest);
            Assert.IsNotNull(image.Manifest);

            Assert.IsNotNull(image.ConfigBytes);
            Assert.IsNotNull(image.Config);

            Assert.IsNotNull(image.LayersAdded);
            Assert.IsFalse(image.LayersAdded.Any());
            Assert.IsFalse(image.ManifestUpdated);
        }

        [TestMethod]
        [DataRow("http://localhost:5000", "hello-world", "latest")]
        public async Task Image_UpdateImage(string registryUrl, string imageName, string imageRef)
        {
            var registry = new Registry(new Uri(registryUrl), _registryLogger, null);
            var imageSource = new RegistryImageSource(imageName, imageRef, registry, _registryLogger);

            var image = await imageSource.LoadImageMetadata();

            var originalConfigBytes = image.ConfigBytes;
            var originalManifestBytes = image.ManifestBytes;
            var originalManifestDigest = image.ManifestDigest;

            var config = image.Config.config;
            config.Labels ??= new Dictionary<string, string>();
            config.Labels["test"] = "test";
            image.ConfigUpdated();

            Assert.IsTrue(image.ManifestUpdated);
            Assert.AreNotEqual(originalConfigBytes, image.ConfigBytes);
            Assert.AreNotEqual(originalManifestBytes, image.ManifestBytes);
            Assert.AreNotEqual(originalManifestDigest, image.ManifestDigest);
        }

        [TestMethod]
        [DataRow("http://localhost:5000", "hello-world", "latest")]
        public async Task Image_AddLayer(string registryUrl, string imageName, string imageRef)
        {
            string desc = "test desc";
            string diffId = "invalid-diff-id";
            string digest = "invalid-digest";
            int size = 2;

            var registry = new Registry(new Uri(registryUrl), _registryLogger, null);
            var imageSource = new RegistryImageSource(imageName, imageRef, registry, _registryLogger);

            var image = await imageSource.LoadImageMetadata();
            int originalHistoryCount = image.Config.history.Count();

            var layer = new BuilderLayer { Description = desc, DiffId = diffId, Digest = digest, Name = "test", Size = size };
            image.AddLayer(layer);

            Assert.IsTrue(image.ManifestUpdated);
            Assert.IsTrue(image.Config.rootfs.diff_ids.Contains(diffId));
            Assert.IsTrue(image.Config.history.Count() > originalHistoryCount);
            Assert.IsTrue(image.Manifest.layers.Any(l => l.digest == digest && l.size == size));
        }
    }
}
