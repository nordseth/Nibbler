﻿using Nibbler.Models;

namespace Nibbler.Test;

[TestClass]
public class RegistryUploadTest
{
    private readonly Logger _registryLogger;
    private readonly HttpClientFactory _httpClientFactory;

    public RegistryUploadTest()
    {
        _registryLogger = new Logger("REGISTRY", true, true);
        var httpLogger = new Utils.Logger("HTTPCLIENT", true, true);
        _httpClientFactory = new HttpClientFactory(httpLogger);
    }

    [TestMethod]
    [DataRow("https://mcr.microsoft.com", "dotnet/aspnet", "9.0")]
    public async Task Digest_Compare(string registryUrl, string imageName, string imageTag)
    {
        var registry = new Registry(_registryLogger, _httpClientFactory.Create(new Uri(registryUrl)));
        var imageSource = new RegistryImageSource(imageName, imageTag, registry, _registryLogger);

        var image = await imageSource.LoadImageMetadata();
        var calcedImageDigest = FileHelper.Digest(image.ConfigBytes);

        Assert.AreEqual(image.Manifest.config.digest.ToLowerInvariant(), calcedImageDigest.ToLowerInvariant());
    }

    [TestMethod]
    [DataRow("https://mcr.microsoft.com", "dotnet/aspnet", "9.0", "http://localhost:5000")]
    public async Task Registry_Upload(string sourceRegistryUrl, string imageName, string imageTag, string destRegistryUrl)
    {
        var registry = new Registry(_registryLogger, _httpClientFactory.Create(new Uri(sourceRegistryUrl)));
        var imageSource = new RegistryImageSource(imageName, imageTag, registry, _registryLogger);

        var image = await imageSource.LoadImageMetadata();

        var dest = new Registry(_registryLogger, _httpClientFactory.Create(new Uri(destRegistryUrl)));
        var uploadUri = await dest.StartUpload(imageName);
        Console.WriteLine($"uploadUri: {uploadUri}");

        await CopyBlobIfNeeded(imageName, registry, dest, uploadUri, image.Manifest.config);
        foreach (var l in image.Manifest.layers)
        {
            await CopyBlobIfNeeded(imageName, registry, dest, uploadUri, l);
        }

        await dest.UploadManifest(imageName, imageTag, image.Manifest);
    }

    [TestMethod]
    public async Task Retry_Fails()
    {
        try
        {
            await RetryHelper.Retry(3, new Logger("RETRY", true, true), () => throw new Exception());
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
            await dest.UploadBlob(uploadUri, layer.digest, blob, layer.size);
            Console.WriteLine($"Done copying layer: {layer.digest} to {uploadUri}");
        }
    }
}
