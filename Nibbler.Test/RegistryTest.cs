using System.Text.Json;
using Nibbler.Models;

namespace Nibbler.Test;

[TestClass]
public class RegistryTest
{
    private readonly Utils.Logger _registryLogger;
    private readonly HttpClientFactory _httpClientFactory;

    public RegistryTest()
    {
        _registryLogger = new Utils.Logger("REGISTRY", true, true);
        var httpLogger = new Utils.Logger("HTTPCLIENT", true, true);
        _httpClientFactory = new HttpClientFactory(httpLogger);
    }

    [TestMethod]
    [DataRow("https://mcr.microsoft.com", "dotnet/aspnet", "9.0")]
    [DataRow("http://localhost:5000", "hello-world", "latest")]
    public async Task Registry_Get_ManifestFile(string registryUrl, string imageName, string imageRef)
    {
        var registry = new Registry(_registryLogger, _httpClientFactory.Create(new Uri(registryUrl)));

        var manifestContent = await registry.GetManifest(imageName, imageRef);
        var manifestJson = await manifestContent.ReadAsStringAsync();
        Console.WriteLine(manifestJson);
    }

    [TestMethod]
    [DataRow("https://mcr.microsoft.com", "dotnet/aspnet", "9.0")]
    [DataRow("http://localhost:5000", "hello-world", "latest")]
    public async Task Registry_Get_Manifest(string registryUrl, string imageName, string imageRef)
    {
        var registry = new Registry(_registryLogger, _httpClientFactory.Create(new Uri(registryUrl)));

        var manifestContent = await registry.GetManifest(imageName, imageRef);
        var manifestJson = await manifestContent.ReadAsStringAsync();
        var manifest = JsonSerializer.Deserialize<ManifestV2>(manifestJson);

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
    [DataRow("registry.hub.docker.com/library/hello-world:latest", false, false, false)]
    [DataRow("registry.hub.docker.com/nordseth/lego-kubectl:latest", false, false, true)]
    [DataRow("nordseth.azurecr.io/azure-test:latest_master", false, false, true)]
    public async Task Registry_Get_Manifest_With_Auth(string image, bool insecure, bool skipTlsVerify, bool useDockerConfig)
    {
        var registryName = ImageHelper.GetRegistryName(image);
        var registryUrl = ImageHelper.GetRegistryBaseUrl(image, insecure);
        DockerConfigCredentials dockerCredentials = null;
        if (useDockerConfig)
        {
            dockerCredentials = new DockerConfigCredentials(null);
        }
        var authHandler = new AuthenticationHandler(registryName, dockerCredentials, false, _registryLogger, _httpClientFactory.Create(registryUrl));
        var httpClient = _httpClientFactory.Create(registryUrl, skipTlsVerify, authHandler);
        var registry = new Registry(_registryLogger, httpClient);

        var imageName = ImageHelper.GetImageName(image);
        var imageRef = ImageHelper.GetImageReference(image);
        var manifestContent = await registry.GetManifest(imageName, imageRef);
        var manifestJson = await manifestContent.ReadAsStringAsync();
        var manifest = JsonSerializer.Deserialize<ManifestV2>(manifestJson);
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

        Console.WriteLine(manifestJson);
    }

    [TestMethod]
    [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "sha256:930743cb4e197dc01a680b604464724ad1344a07b395e9871482ef05dbd25950")]
    public async Task Registry_Get_ImageFile(string registryUrl, string imageName, string digest)
    {
        var httpClient = _httpClientFactory.Create(new Uri(registryUrl), false);
        var registry = new Registry(_registryLogger, httpClient);

        var imageContent = await registry.GetImageConfig(imageName, digest);
        var imageJson = await imageContent.ReadAsStringAsync();
        Console.WriteLine(imageJson);
    }

    [TestMethod]
    [DataRow("registry.hub.docker.com", "library/hello-world", "sha256:bf756fb1ae65adf866bd8c456593cd24beb6a0a061dedf42b26a993176745f6b", false, false)]
    public async Task Registry_Get_ImageFile_With_Auth(string registryName, string imageName, string digest, bool insecure, bool skipTlsVerify)
    {
        var registryUrl = ImageHelper.GetRegistryBaseUrl(registryName, insecure);
        var authHandler = new AuthenticationHandler(registryName, null, false, _registryLogger, _httpClientFactory.Create(registryUrl));
        var httpClient = _httpClientFactory.Create(registryUrl, skipTlsVerify, authHandler);
        var registry = new Registry(_registryLogger, httpClient);

        var imageContent = await registry.GetImageConfig(imageName, digest);
        var imageJson = await imageContent.ReadAsStringAsync();
        Console.WriteLine(imageJson);
    }

    [TestMethod]
    [DataRow("https://mcr.microsoft.com", "dotnet/core/aspnet", "sha256:930743cb4e197dc01a680b604464724ad1344a07b395e9871482ef05dbd25950")]
    public async Task Registry_Get_Image(string registryName, string imageName, string digest)
    {
        var registry = new Registry(_registryLogger, _httpClientFactory.Create(new Uri(registryName)));

        var imageContent = await registry.GetImageConfig(imageName, digest);
        var imageJson = await imageContent.ReadAsStringAsync();
        var image = JsonSerializer.Deserialize<ImageV1>(imageJson);
        Assert.IsNotNull(image);
        Assert.IsNotNull(image.config);
        Assert.IsNotNull(image.rootfs);
        Assert.IsNotNull(image.rootfs.diff_ids);
        Assert.IsTrue(image.rootfs.diff_ids.Any());
    }
}
