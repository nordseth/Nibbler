﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler.Test
{
    [TestClass]
    public class RegistryUploadTest
    {
        [TestMethod]
        [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "3.0")]
        public async Task Digest_Compare(string registryUrl, string imageName, string imageTag)
        {
            var source = new Registry(new Uri(registryUrl));
            var manifest = await source.GetManifest(imageName, imageTag);
            var (json, digest) = await source.GetImageFile(imageName, manifest.config.digest);

            Assert.AreEqual(manifest.config.digest.ToLowerInvariant(), digest.ToLowerInvariant());
        }

        [TestMethod]
        [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "3.0", "http://localhost:5000", false)]
        public async Task Registry_Upload(string sourceRegistryUrl, string imageName, string imageTag, string destRegistryUrl, bool useDockerConfigDest)
        {
            var source = new Registry(new Uri(sourceRegistryUrl));
            var manifest = await source.GetManifest(imageName, imageTag);
            var image = await source.GetImage(imageName, manifest.config.digest);

            var dest = new Registry(new Uri(destRegistryUrl));
            if (useDockerConfigDest)
            {
                dest.UseAuthorization(ImageHelper.GetDockerConfigAuth(destRegistryUrl, null));
            }
            var uploadUri = await dest.StartUpload(imageName);
            Console.WriteLine($"uploadUri: {uploadUri}");

            await CopyBlobIfNeeded(imageName, source, dest, uploadUri, manifest.config);
            foreach (var l in manifest.layers)
            {
                await CopyBlobIfNeeded(imageName, source, dest, uploadUri, l);
            }

            await dest.UploadManifest(imageName, imageTag, manifest);
        }

        [TestMethod]
        public async Task Retry_Fails()
        {
            try
            {
                await RetryHelper.Retry(3, true, () => throw new Exception());
                Assert.Fail("Should throw exception");
            }
            catch
            {
                // ok
            }
        }

        private static async Task CopyBlobIfNeeded(string imageName, Registry source, Registry dest, string uploadUri, ManifestV2Layer layer)
        {
            var exist = await dest.BlobExists(imageName, layer.digest);
            Console.WriteLine($"{layer.digest} - {layer.mediaType} - {layer.size} = {exist.HasValue}");
            if (!exist.HasValue)
            {
                Console.WriteLine($"Copying layer: {layer.digest} - {layer.mediaType} - {layer.size}");
                var blob = await source.DownloadBlob(imageName, layer.digest);
                await dest.UploadBlobChuncks(uploadUri, layer.digest, blob, 10000, true);
                Console.WriteLine($"Done copying layer: {layer.digest} to {uploadUri}");
            }
        }
    }
}
