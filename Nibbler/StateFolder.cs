using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler
{
    public class StateFolder
    {
        private static string StateFileName = "state.json";
        private static string ImageConfigFileName = "image.json";
        private static string ManifestFileName = "manifest.json";
        private static string ManifestDigestFileName = "digest";

        public StateFolder(string path)
        {
            FolderPath = path;
        }

        public string FolderPath { get; }
        public BuilderState State { get; private set; }

        public bool Recreate()
        {
            bool deleted = false;
            if (Directory.Exists(FolderPath))
            {
                Directory.Delete(FolderPath, true);
                // delete can be delayed, so need to wait a bit...
                while (Directory.Exists(FolderPath))
                {
                    System.Threading.Thread.Sleep(10);
                }

                deleted = true;
            }

            Directory.CreateDirectory(FolderPath);
            return deleted;
        }

        public FileStream GetReadStream(string filename)
        {
            return File.OpenRead(Path.Combine(FolderPath, filename));
        }

        public BuilderState GetBuilderState()
        {
            var content = File.ReadAllText(Path.Combine(FolderPath, StateFileName));
            return JsonConvert.DeserializeObject<BuilderState>(content);
        }

        public ImageV1 GetImageConfig()
        {
            var content = File.ReadAllText(Path.Combine(FolderPath, ImageConfigFileName));
            return JsonConvert.DeserializeObject<ImageV1>(content);
        }

        public ManifestV2 GetManifest()
        {
            var content = File.ReadAllText(Path.Combine(FolderPath, ManifestFileName));
            return JsonConvert.DeserializeObject<ManifestV2>(content);
        }

        public Stream GetImageConfigStream()
        {
            return File.OpenRead(Path.Combine(FolderPath, ImageConfigFileName));
        }

        public Stream GetManifestStream()
        {
            return File.OpenRead(Path.Combine(FolderPath, ManifestFileName));
        }

        public void SaveState(BuilderState obj)
        {
            SaveJson(StateFileName, obj);
        }

        public void CreateManifest(ManifestV2 manifest)
        {
            SaveJson(ManifestFileName, manifest);
        }

        public void CreateImageConfig(ImageV1 image)
        {
            SaveJson(ImageConfigFileName, image);
        }

        public string AddLayerToConfig(BuilderLayer layer, string history)
        {
            var image = GetImageConfig();
            image.rootfs.diff_ids.Add(layer.DiffId);
            image.history.Add(ImageV1History.Create(history, null));

            var (imageSize, imageDigest) = SaveJson(ImageConfigFileName, image);

            var manifest = GetManifest();
            manifest.config.digest = imageDigest;
            manifest.config.size = imageSize;
            manifest.layers.Add(new ManifestV2Layer
            {
                digest = layer.Digest,
                size = layer.Size,
            });
            var (_, manifestDigest) = SaveJson(ManifestFileName, manifest);

            return manifestDigest;
        }

        public string UpdateImageConfig(ImageV1 image)
        {
            var (imageSize, imageDigest) = SaveJson(ImageConfigFileName, image);
            var manifest = GetManifest();
            manifest.config.digest = imageDigest;
            manifest.config.size = imageSize;
            var (_, manifestDigest) = SaveJson(ManifestFileName, manifest);
            return manifestDigest;
        }

        public void WritePushedImageDigest(string digest)
        {
            File.WriteAllText(Path.Combine(FolderPath, ManifestDigestFileName), digest);
        }

        private (long, string) SaveJson<T>(string filename, T obj)
        {
            var content = FileHelper.JsonSerialize(obj);
            var bytes = Encoding.UTF8.GetBytes(content);
            var digest = FileHelper.Digest(bytes);
            File.WriteAllBytes(Path.Combine(FolderPath, filename), bytes);
            return (bytes.Length, digest);
        }
    }
}
