using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler
{
    /// <summary>
    /// A folder as destination for a image
    /// Same format as FileImageSource
    ///   manifest.json
    ///   sha265_[digest] (blob)
    ///   
    /// Will always try to add all layers, if folder exists it will fail
    /// </summary>
    public class FileImageDestination : IImageDestination
    {
        public const string ManifestFileName = "manifest.json";

        private readonly string _path;
        private readonly IEnumerable<BuilderLayer> _addedLayers;
        private readonly ILogger _logger;
        private bool _folderChecked;

        public FileImageDestination(string path, IEnumerable<BuilderLayer> addedLayers, ILogger logger)
        {
            _path = path;
            _addedLayers = addedLayers;
            _logger = logger;
        }

        public Task<bool> CheckConfigExists(ManifestV2 manifest)
        {
            return Task.FromResult(false);
        }

        public Task<IEnumerable<ManifestV2Layer>> FindMissingLayers(ManifestV2 manifest)
        {
            var missingLayers = new List<ManifestV2Layer>();
            foreach (var layer in manifest.layers)
            {
                var toAdd = _addedLayers.Any(x => x.Digest.Equals(layer.digest));
                if (!toAdd)
                {
                    missingLayers.Add(layer);
                }
            }

            return Task.FromResult(missingLayers.AsEnumerable());
        }

        public async Task PushManifest(string mediaType, Func<Stream> manifestStream)
        {
            CheckFolder();
            using (var stream = manifestStream())
            using (var manifestFile = File.Open(Path.Combine(_path, ManifestFileName), FileMode.CreateNew))
            {
                await stream.CopyToAsync(manifestFile);
            }

            _logger.LogDebug($"Wrote {ManifestFileName}");
        }

        public async Task PushConfig(ManifestV2Layer config, Func<Stream> configStream)
        {
            CheckFolder();
            string filename = FileHelper.DigestToFilename(config.digest);

            using (var stream = configStream())
            using (var configFile = File.Open(Path.Combine(_path, filename), FileMode.CreateNew))
            {
                await stream.CopyToAsync(configFile);
            }

            _logger.LogDebug($"Wrote image config {config.digest}");
        }

        public async Task CopyLayers(IImageSource imageSource, IEnumerable<ManifestV2Layer> missingLayers)
        {
            CheckFolder();

            foreach (var layer in missingLayers)
            {
                string filename = FileHelper.DigestToFilename(layer.digest);

                using (var stream = await imageSource.GetBlob(layer.digest))
                using (var layerFile = File.Open(Path.Combine(_path, filename), FileMode.CreateNew))
                {
                    await stream.CopyToAsync(layerFile);
                }

                _logger.LogDebug($"Downladed layer from {imageSource.Name} - {layer.digest}, {layer.size} bytes.");
            }
        }

        public async Task PushLayers(Func<string, Stream> layerStream)
        {
            CheckFolder();

            foreach (var layer in _addedLayers)
            {
                string filename = FileHelper.DigestToFilename(layer.Digest);

                using (var stream = layerStream($"{layer.Name}.tar.gz"))
                using (var layerFile = File.Open(Path.Combine(_path, filename), FileMode.CreateNew))
                {
                    await stream.CopyToAsync(layerFile);
                }

                _logger.LogDebug($"Wrote layer {layer.Digest}");
            }
        }

        private void CheckFolder()
        {
            if (!_folderChecked)
            {
                if (Directory.Exists(_path))
                {
                    throw new Exception($"error {_path} already exists!");
                }

                var folder = Directory.CreateDirectory(_path);
                _logger.LogDebug($"Created folder {_path}");
                _folderChecked = true;
            }
        }
    }
}
