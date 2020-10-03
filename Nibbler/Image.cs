using Newtonsoft.Json;
using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler
{
    public class Image
    {
        private Registry _registry;

        public Image(Registry registry, string name, string @ref)
        {
            _registry = registry;
            Name = name;
            Ref = @ref;
        }

        public string Name { get; }
        public string Ref { get; }
        public string ManifestFile { get; private set; }
        private ManifestV2 Manifest { get; set; }
        public string ConfigFile { get; private set; }
        public ImageV1 Config { get; private set; }
        public bool ManifestUpdated { get; set; }

        public async Task LoadMetadata()
        {
            ManifestFile = await _registry.GetManifestFile(Name, Ref);
            Manifest = JsonConvert.DeserializeObject<ManifestV2>(ManifestFile);
            var imageFileResult = await _registry.GetImageFile(Name, Manifest.config.digest);
            ConfigFile = imageFileResult.content;
            Config = JsonConvert.DeserializeObject<ImageV1>(ConfigFile);
            ManifestUpdated = false;
        }

        public void UpdateConfigInManifest()
        {
            var (imageBytes, imageDigest) = ToJson(Config);
            Manifest.config.digest = imageDigest;
            Manifest.config.size = imageBytes.Length;
            ManifestUpdated = true;
        }

        public void AddLayerToConfigAndManifest(BuilderLayer layer)
        {
            Config.rootfs.diff_ids.Add(layer.DiffId);
            Config.history.Add(ImageV1History.Create(layer.Description, null));

            UpdateConfigInManifest();
            Manifest.layers.Add(new ManifestV2Layer
            {
                digest = layer.Digest,
                size = layer.Size,
            });
            ManifestUpdated = true;
        }

        private (byte[], string) ToJson<T>(T obj)
        {
            var content = FileHelper.JsonSerialize(obj);
            var bytes = Encoding.UTF8.GetBytes(content);
            var digest = FileHelper.Digest(bytes);
            return (bytes, digest);
        }

        private Stream GetJsonStream<T>(T obj)
        {
            var (bytes, _) = ToJson(obj);
            return new MemoryStream(bytes);
        }
    }
}
