using Newtonsoft.Json;
using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
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
        
        public byte[] ManifestBytes { get; private set; }
        public string ManifestDigest { get; private set; }
        public ManifestV2 Manifest { get; private set; }

        public byte[] ConfigBytes { get; private set; }
        public ImageV1 Config { get; private set; }

        private List<BuilderLayer> _layersAdded = new List<BuilderLayer>();
        public IEnumerable<BuilderLayer> LayersAdded => _layersAdded.AsReadOnly();

        public bool ManifestUpdated { get; set; }

        public async Task LoadMetadata()
        {
            var manifestContent = await _registry.GetManifest(Name, Ref);
            ManifestBytes = await manifestContent.ReadAsByteArrayAsync();
            ManifestDigest = FileHelper.Digest(ManifestBytes);
            var manifestJson = await manifestContent.ReadAsStringAsync();
            Manifest = JsonConvert.DeserializeObject<ManifestV2>(manifestJson);

            var imageConfigContent = await _registry.GetImageConfig(Name, Manifest.config.digest);
            ConfigBytes = await imageConfigContent.ReadAsByteArrayAsync();
            var configJson = await imageConfigContent.ReadAsStringAsync();
            Config = JsonConvert.DeserializeObject<ImageV1>(configJson);

            ManifestUpdated = false;
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

        private (byte[], string) ToJson<T>(T obj)
        {
            var content = FileHelper.JsonSerialize(obj);
            var bytes = Encoding.UTF8.GetBytes(content);
            var digest = FileHelper.Digest(bytes);
            return (bytes, digest);
        }
    }
}
