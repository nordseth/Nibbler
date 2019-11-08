using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nibbler.Test
{
    [TestClass]
    public class ArchiveTest
    {
        [TestMethod]
        [DataRow(@"../../../../TestTemp/test.tar.gz")]
        public void Tar_Read_Archive_Details(string archive)
        {
            byte[] dataBuffer = new byte[4096];

            using var fs = new FileStream(archive, FileMode.Open, FileAccess.Read);
            using var gzipStream = new GZipInputStream(fs);
            using var tarStream = new TarInputStream(gzipStream);

            TarEntry tarEntry;
            while ((tarEntry = tarStream.GetNextEntry()) != null)
            {
                Console.WriteLine(Archive.PrintEntry(tarEntry));
            }
        }

        [TestMethod]
        [DataRow(".")]
        public void Linux_File_Attributes(string path)
        {
            var e = Mono.Unix.UnixFileSystemInfo.GetFileSystemEntry(path);
        }

        [TestMethod]
        [DataRow(@"../../../../TestTemp/publish/", "/app")]
        public void Archive_Enumerate(string source, string dest)
        {
            var tar = new Archive(null, null, false);
            tar.CreateEntries(source, dest, false, null, null);

            foreach (var e in tar.Entries)
            {
                Console.WriteLine(Archive.PrintEntry(e.Item2));
            }
        }

        [TestMethod]
        [DataRow(@"../../../../TestTemp/publish/", "/app", "../../../../TestTemp")]
        public void Archive_Add_Files(string source, string dest, string tempFolder)
        {
            var layer = Path.Combine(tempFolder, "nibbler-test.tar.gz");
            var tempLayer = Path.Combine(tempFolder, "nibbler-test.tar");

            Console.WriteLine($"layer: {layer}");
            Console.WriteLine($"temp layer: {tempLayer}");
            var tar = new Archive(layer, tempLayer, true);
            tar.CreateEntries(source, dest, false, null, null);
            tar.WriteFiles();

            var size = tar.GetSize();
            Console.WriteLine($"size: {size}");
            var digest = tar.CalculateDigest();
            Console.WriteLine($"digest: {digest}");
            var diffId = tar.CalculateDiffId();
            Console.WriteLine($"diffId: {diffId}");
        }
    }
}
