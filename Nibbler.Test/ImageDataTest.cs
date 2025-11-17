using Nibbler.Models;

namespace Nibbler.Test;

[TestClass]
public class ImageDataTest
{
    private readonly Logger _registryLogger;
    private readonly Logger _httpLogger;

    public ImageDataTest()
    {
        _registryLogger = new Logger("REGISTRY", true, true);
        _httpLogger = new Logger("HTTP", true, true);
    }

    [TestMethod]
    [DataRow("http://localhost:5000", "hello-world", "latest")]
    public async Task Image_LoadMetadata(string registryUrl, string imageName, string imageRef)
    {
        var registry = new Registry(_registryLogger, new HttpClientFactory(_httpLogger).Create(new Uri(registryUrl)));
        var imageSource = new RegistryImageSource(imageName, imageRef, registry, _registryLogger);

        var image = await imageSource.LoadImageMetadata();

        Assert.IsNotNull(image.ManifestBytes);
        Assert.IsNotNull(image.ManifestDigest);
        Assert.IsNotNull(image.Manifest);

        Assert.IsNotNull(image.ConfigBytes);
        Assert.IsNotNull(image.Config);

        Assert.IsNotNull(image.LayersAdded);
        Assert.IsEmpty(image.LayersAdded);
        Assert.IsFalse(image.ManifestUpdated);
    }

    [TestMethod]
    [DataRow("http://localhost:5000", "hello-world", "latest")]
    public async Task Image_UpdateImage(string registryUrl, string imageName, string imageRef)
    {
        var registry = new Registry(_registryLogger, new HttpClientFactory(_httpLogger).Create(new Uri(registryUrl)));
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

        var registry = new Registry(_registryLogger, new HttpClientFactory(_httpLogger).Create(new Uri(registryUrl)));
        var imageSource = new RegistryImageSource(imageName, imageRef, registry, _registryLogger);

        var image = await imageSource.LoadImageMetadata();
        int originalHistoryCount = image.Config.history.Count();

        var layer = new BuilderLayer { Description = desc, DiffId = diffId, Digest = digest, Name = "test", Size = size };
        image.AddLayer(layer);

        Assert.IsTrue(image.ManifestUpdated);
        Assert.Contains(diffId, image.Config.rootfs.diff_ids);
        Assert.IsGreaterThan(originalHistoryCount, image.Config.history.Count());
        Assert.Contains(l => l.digest == digest && l.size == size, image.Manifest.layers);
    }
}
