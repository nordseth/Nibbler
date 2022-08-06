using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nibbler
{
    public class Image
    {
        public const string ManifestFileName = "manifest.json";

        public byte[] ManifestBytes { get; private set; }
        public string ManifestDigest { get; private set; }
        public ManifestV2 Manifest { get; private set; }

        public byte[] ConfigBytes { get; private set; }
        public ImageV1 Config { get; private set; }

        private List<BuilderLayer> _layersAdded = new List<BuilderLayer>();
        public IEnumerable<BuilderLayer> LayersAdded => _layersAdded.AsReadOnly();

        public bool ManifestUpdated { get; set; }

        public static async Task<Image> LoadMetadata(IContents manifestContent, Func<string, Task<IContents>> getConfig)
        {
            var image = new Image();

            image.ManifestBytes = await manifestContent.ReadBytesAsync();
            image.ManifestDigest = FileHelper.Digest(image.ManifestBytes);
            var manifestJson = await manifestContent.ReadStringAsync();
            image.Manifest = JsonSerializer.Deserialize<ManifestV2>(manifestJson);

            var imageConfigContent = await getConfig(image.Manifest.config.digest);
            image.ConfigBytes = await imageConfigContent.ReadBytesAsync();
            var configJson = await imageConfigContent.ReadStringAsync();
            image.Config = JsonSerializer.Deserialize<ImageV1>(configJson);

            image.ManifestUpdated = false;
            return image;
        }

        public void ConfigUpdated()
        {
            var (configBytes, configDigest) = ToJson(Config);
            ConfigBytes = configBytes;
            Manifest.config.digest = configDigest;
            Manifest.config.size = configBytes.Length;

            var (manifestBytes, manifestDigest) = ToJson(Manifest);
            ManifestDigest = manifestDigest;
            ManifestBytes = manifestBytes;
            ManifestUpdated = true;
        }

        public void AddLayer(BuilderLayer layer)
        {
            _layersAdded.Add(layer);

            Config.rootfs.diff_ids.Add(layer.DiffId);
            Config.history.Add(ImageV1History.Create(layer.Description, null));

            Manifest.layers.Add(new ManifestV2Layer
            {
                digest = layer.Digest,
                size = layer.Size,
            });

            ConfigUpdated();
        }

        public Image Clone()
        {
            return new Image
            {
                ManifestBytes = ManifestBytes,
                ManifestDigest = ManifestDigest,
                Manifest = Manifest.Clone(),
                ConfigBytes = ConfigBytes,
                Config = Config?.Clone(),
                ManifestUpdated = ManifestUpdated,
                _layersAdded = _layersAdded.Select(l => l.Clone()).ToList(),
            };
        }

        private (byte[], string) ToJson<T>(T obj)
        {
            var content = FileHelper.JsonSerialize(obj);
            var bytes = Encoding.UTF8.GetBytes(content);
            var digest = FileHelper.Digest(bytes);
            return (bytes, digest);
        }
    }
}
