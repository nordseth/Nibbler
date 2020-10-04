﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler.Test
{
    [TestClass]
    public class ImageCompareTest
    {
        [TestMethod]
        [DataRow("localhost:5000/nibbler-test:kaniko", true)]
        [DataRow("localhost:5000/nibbler-test:docker", true)]
        [DataRow("localhost:5000/nibbler-test:nibbler", true)]
        public async Task ImageCompare_Download_Image_And_Layer(string image, bool insecure)
        {
            var registry = new Registry(ImageHelper.GetRegistryBaseUrl(image, insecure), new Logger("REGISTRY", true), null);

            var loadedImage = new Image(registry, ImageHelper.GetImageName(image), ImageHelper.GetImageReference(image));
            await loadedImage.LoadMetadata();
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
}
