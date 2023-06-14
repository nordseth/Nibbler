using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace Nibbler.Test;

[TestClass]
public class ImageCompareTest
{
    [TestMethod]
    [Ignore]
    [DataRow("localhost:5000/nibbler-test:kaniko", true)]
    [DataRow("localhost:5000/nibbler-test:docker", true)]
    [DataRow("localhost:5000/nibbler-test:nibbler", true)]
    public async Task ImageCompare_Download_Image_And_Layer(string image, bool insecure)
    {
        var logger = new Logger("REGISTRY", true, true);
        var registry = new Registry(logger, new HttpClientFactory(logger).Create(ImageHelper.GetRegistryBaseUrl(image, insecure)));

        var imageSource = new RegistryImageSource(ImageHelper.GetImageName(image), ImageHelper.GetImageReference(image), registry, logger);
        var loadedImage = await imageSource.LoadImageMetadata();
        //Console.WriteLine("-------------");
        //Console.WriteLine(JsonConvert.SerializeObject(manifest, Formatting.Indented));

        //Console.WriteLine("-------------");
        //Console.WriteLine(JsonConvert.SerializeObject(imageConfig, Formatting.Indented));

        var layer = await registry.DownloadBlob(ImageHelper.GetImageName(image), loadedImage.Manifest.layers.Last().digest);

        using var gzipStream = new GZipInputStream(layer);
        using var tarStream = new TarInputStream(gzipStream, null);
        //Console.WriteLine("-------------");

        var tarEntries = new List<TarEntry>();
        TarEntry tarEntry;
        while ((tarEntry = tarStream.GetNextEntry()) != null)
        {
            tarEntries.Add(tarEntry);
        }

        foreach (var e in tarEntries.OrderByDescending(e => e.IsDirectory).ThenBy(e => e.Name))
        {
            Console.WriteLine($"{Convert.ToString(e.TarHeader.Mode, 8)} {e.UserId}/{e.UserName} {e.GroupId}/{e.GroupName} {(e.IsDirectory ? "- D " : "")}- {e.Name} ");
        }
    }
}
