using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Writer
{
    public class FileImageWriter : IImageWriter
    {
        private readonly string _path;
        private readonly ILogger _logger;

        public FileImageWriter(string path, ILogger logger)
        {
            _path = path;
            _logger = logger;
        }

        public async Task WriteImage(Image image, Func<string, Task<Stream>> layerSource)
        {
            EnsureFolder();
            await WriteConfig(image);
            await WriteLayers(image, layerSource);
            await WriteManifest(image);
        }

        private async Task WriteConfig(Image image)
        {
            var config = image.Manifest.config;
            string filename = FileHelper.DigestToFilename(config.digest);

            using (var stream = new MemoryStream(image.ConfigBytes))
            using (var configFile = File.Open(Path.Combine(_path, filename), FileMode.CreateNew))
            {
                await stream.CopyToAsync(configFile);
            }

            _logger.LogDebug($"Wrote image config {config.digest}");
        }

        private async Task WriteLayers(Image image, Func<string, Task<Stream>> layerSource)
        {
            foreach (var layer in image.Manifest.layers)
            {
                string filename = FileHelper.DigestToFilename(layer.digest);

                using (var stream = await layerSource(layer.digest))
                using (var layerFile = File.Open(Path.Combine(_path, filename), FileMode.CreateNew))
                {
                    await stream.CopyToAsync(layerFile);
                }

                _logger.LogDebug($"Wrote layer {layer.digest}, {layer.size} bytes.");
            }
        }

        private async Task WriteManifest(Image image)
        {
            using (var stream = new MemoryStream(image.ManifestBytes))
            using (var manifestFile = File.Open(Path.Combine(_path, Image.ManifestFileName), FileMode.CreateNew))
            {
                await stream.CopyToAsync(manifestFile);
            }

            _logger.LogDebug($"Wrote {Image.ManifestFileName}");
        }

        private void EnsureFolder()
        {
            if (Directory.Exists(_path))
            {
                throw new Exception($"error {_path} already exists!");
            }

            var folder = Directory.CreateDirectory(_path);
            _logger.LogDebug($"Created folder {_path}");
        }
    }
}
