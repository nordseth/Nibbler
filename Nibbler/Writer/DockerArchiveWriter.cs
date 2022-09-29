using ICSharpCode.SharpZipLib.Tar;
using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Writer
{
    /// <summary>
    /// https://github.com/moby/moby/blob/master/image/spec/v1.2.md#combined-image-json--filesystem-changeset-format
    /// only uses new format with manifest.json
    /// </summary>
    public class DockerArchiveWriter : IImageWriter
    {
        public const string ManifestFileName = "manifest.json";

        private readonly string _path;
        private readonly ILogger _logger;

        private FileStream _outFile;
        private TarOutputStream _archive;

        public DockerArchiveWriter(string path, ILogger logger)
        {
            _path = path;
            _logger = logger;
        }

        public async Task WriteImage(Image image, Func<string, Task<Stream>> layerSource)
        {
            try
            {
                _outFile = File.Create(_path);
                _archive = new TarOutputStream(_outFile, null);

                await WriteConfig(image.Manifest.config.digest, image.ConfigBytes);
                await WriteLayers(image.Manifest.layers, layerSource);
                await WriteManifest(image);
            }
            finally
            {
                _archive?.Dispose();
                _archive = null;
                _outFile?.Dispose();
                _outFile = null;
            }

            _logger.LogDebug($"Wrote archive {_path}");
        }

        private async Task WriteConfig(string digest, byte[] configBytes)
        {
            using var stream = new MemoryStream(configBytes);
            var entry = TarEntry.CreateTarEntry(ConfigFileName(digest));
            entry.Size = configBytes.Length;
            _archive.PutNextEntry(entry);
            await stream.CopyToAsync(_archive);
            _archive.CloseEntry();

            _logger.LogDebug($"Wrote config {digest}.");
        }

        private async Task WriteLayers(IEnumerable<ManifestV2Layer> layers, Func<string, Task<Stream>> layerSource)
        {
            foreach (var layer in layers)
            {
                using (var stream = await layerSource(layer.digest))
                {
                    var entry = TarEntry.CreateTarEntry(LayerFileName(layer.digest));
                    entry.Size = layer.size;
                    _archive.PutNextEntry(entry);
                    await stream.CopyToAsync(_archive);
                    _archive.CloseEntry();

                    _logger.LogDebug($"Wrote layer {layer.digest}, {layer.size} bytes.");
                }
            }
        }

        private async Task WriteManifest(Image image)
        {
            var manifest = new[]
            {
                new
                {
                    Config = ConfigFileName(image.Manifest.config.digest),
                    RepoTags = new string[] { },
                    Layers = image.Manifest.layers.Select(l => LayerFileName(l.digest)).ToList(),
                }
            };
            var manifestJson = FileHelper.JsonSerialize(manifest);
            var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
            using var stream = new MemoryStream(manifestBytes);

            var entry = TarEntry.CreateTarEntry(ManifestFileName);
            entry.Size = manifestBytes.Length;
            _archive.PutNextEntry(entry);
            await stream.CopyToAsync(_archive);
            _archive.CloseEntry();

            _logger.LogDebug($"Wrote {ManifestFileName}.");
        }

        private string NameFromDigest(string digest)
        {
            return digest.Substring(digest.IndexOf(":") + 1);
        }

        private string ConfigFileName(string digest)
        {
            return $"{NameFromDigest(digest)}.json";
        }

        private string LayerFileName(string digest)
        {
            return $"{NameFromDigest(digest)}/layer.tar";
        }
    }
}
