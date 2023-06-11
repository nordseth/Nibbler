using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace Nibbler.Test;

[TestClass]
public class ArchiveTest
{
    [TestMethod]
    [DataRow(@"../../../../tests/TestData/test.tar.gz")]
    public void Tar_Read_Archive_Details(string archive)
    {
        using var fs = new FileStream(archive, FileMode.Open, FileAccess.Read);
        using var gzipStream = new GZipInputStream(fs);
        using var tarStream = new TarInputStream(gzipStream, null);

        TarEntry tarEntry;
        while ((tarEntry = tarStream.GetNextEntry()) != null)
        {
            Console.WriteLine(Archive.PrintEntry(tarEntry));
        }
    }

    [TestMethod]
    [DataRow(@"../../../../tests/TestData/publish/", "/app")]
    public void Archive_Enumerate(string source, string dest)
    {
        var tar = new Archive(null, false, null, null, null);
        tar.CreateEntries(source, dest, null, null, null);

        foreach (var e in tar.Entries)
        {
            Console.WriteLine(Archive.PrintEntry(e.Value.Item2));
        }
    }

    [TestMethod]
    [DataRow(@"../../../../tests/TestData/publish/", "/app")]
    public void Archive_Reproducable(string source, string dest)
    {
        var reprodTime = new DateTime(2000, 1, 1);

        var tar = new Archive(null, true, null, null, null);
        tar.CreateEntries(source, dest, null, null, null);

        Assert.IsTrue(tar.Entries.Any(), "Empty tar, not able to verify");

        foreach (var e in tar.Entries)
        {
            Assert.AreEqual(reprodTime, e.Value.Item2.ModTime);
        }
    }

    [TestMethod]
    [DataRow(@"../../../../tests/TestData/publish/", "/app")]
    public void Archive_Not_Reproducable(string source, string dest)
    {
        var reprodTime = new DateTime(2000, 1, 1);

        var tar = new Archive(null, false, null, null, null);
        tar.CreateEntries(source, dest, null, null, null);

        Assert.IsTrue(tar.Entries.Any(), "Empty tar, not able to verify");

        foreach (var e in tar.Entries)
        {
            Assert.AreNotEqual(reprodTime, e.Value.Item2.ModTime);
        }
    }

    [TestMethod]
    [DataRow(@"../../../../tests/TestData/publish/", "/app", "../../../../tests/TestData")]
    public void Archive_OneFile_Add_Files(string source, string dest, string tempFolder)
    {
        var layer = Path.Combine(tempFolder, "nibbler-test2.tar.gz");

        Console.WriteLine($"layer: {layer}");
        var tar = new Archive(layer, true, null, null, null);
        tar.CreateEntries(source, dest, null, null, null);
        var (gzipDigest, tarDigest) = tar.WriteFileAndCalcDigests();

        var size = tar.GetSize();
        Console.WriteLine($"size: {size}");
        Console.WriteLine($"digest: {gzipDigest}");
        Console.WriteLine($"diffId: {tarDigest}");
    }

    [TestMethod]
    [DataRow(@"../../../../tests/TestData/")]
    public void Archive_Ignore(string source)
    {
        var tarWithIgnore = new Archive(null, false, null, ".gitignore", null);
        tarWithIgnore.CreateEntries(source, "/app", null, null, null);

        var tarNoIgnore = new Archive(null, false, null, null, null);
        tarNoIgnore.CreateEntries(source, "/app", null, null, null);

        Console.WriteLine($"withIgnore: {tarWithIgnore.Entries.Count}, without: {tarNoIgnore.Entries.Count}");
        foreach (var e in tarWithIgnore.Entries)
        {
            Console.WriteLine(Archive.PrintEntry(e.Value.Item2));
        }
        Assert.IsTrue(tarWithIgnore.Entries.Count < tarNoIgnore.Entries.Count);
    }
}
