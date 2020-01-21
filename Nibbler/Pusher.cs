using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler
{
    public class Pusher
    {
        private readonly string _baseImageName;
        private readonly string _destination;
        private readonly string _targetImageName;
        private readonly IEnumerable<BuilderLayer> _addedLayers;
        private readonly Registry _registry;
        private readonly bool _debug;
        private readonly int _chunckSize;
        private readonly int _retryUpload;

        public Pusher(string baseImage, string destination, IEnumerable<BuilderLayer> addedLayers, Registry registry, bool debug)
        {
            _baseImageName = ImageHelper.GetImageName(baseImage);
            _destination = destination;
            _targetImageName = ImageHelper.GetImageName(_destination);
            _addedLayers = addedLayers;
            _registry = registry;
            _debug = debug;
            // set to 0 to diable chunckes
            _chunckSize = 0;
            _retryUpload = 3;
        }

        public void ValidateDest()
        {
            var destRegistryUri = ImageHelper.GetRegistryBaseUrl(_destination, _registry.BaseUri.Scheme == "http");
            if (_registry.BaseUri != destRegistryUri)
            {
                throw new Exception($"Source ({_registry.BaseUri.Authority}) and destination ({destRegistryUri.Authority}) registries must be the same.");
            }
        }

        public async Task<bool> CheckConfigExists(ManifestV2 manifest)
        {
            var configExists = await _registry.BlobExists(_targetImageName, manifest.config.digest);
            if (_debug)
            {
                Console.WriteLine($"debug: config {manifest.config.digest} ({manifest.config.size}) - {(configExists.HasValue ? "Exists" : "To be uploaded")}");
            }

            return configExists.HasValue;
        }

        public async Task ValidateLayers(ManifestV2 manifest, bool tryMount)
        {
            var missingLayer = new List<string>();
            foreach (var layer in manifest.layers)
            {
                var existsBlobSize = await _registry.BlobExists(_targetImageName, layer.digest);
                var exists = existsBlobSize.HasValue;
                var toAdd = _addedLayers.Any(x => x.Digest.Equals(layer.digest));
                bool mounted = false;
                if (!exists && !toAdd)
                {
                    if (tryMount)
                    {
                        try
                        {
                            await _registry.MountBlob(_targetImageName, layer.digest, _baseImageName);
                            mounted = true;
                        }
                        catch
                        {
                            missingLayer.Add(layer.digest);
                        }
                    }
                    else
                    {
                        missingLayer.Add(layer.digest);
                    }
                }

                if (_debug)
                {
                    Console.Write($"debug: layer {layer.digest} ({layer.size}) - ");

                    if (toAdd && exists)
                    {
                        Console.WriteLine($"Exists (unchanged?)");
                    }
                    else if (toAdd)
                    {
                        Console.WriteLine($"To be uploaded");
                    }
                    else if (!exists && !toAdd)
                    {
                        if (mounted)
                        {
                            Console.WriteLine($"Was missing, Mount sucessful!");
                        }
                        else if (tryMount)
                        {
                            Console.WriteLine($"Error Missing! (tried to mount)");
                        }
                        else
                        {
                            Console.WriteLine($"Error Missing!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Exists");
                    }
                }
            }

            if (missingLayer.Any())
            {
                throw new Exception($"Layers {string.Join(", ", missingLayer)} does not exsist in target repo");
            }
        }

        public async Task PushConfig(ManifestV2Layer config, Func<System.IO.Stream> configStream)
        {
            await RetryHelper.Retry(_retryUpload, _debug,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    if (_debug)
                    {
                        Console.WriteLine($"debug: uploading config. (upload uri: {uploadUri})");
                    }

                    using (var stream = configStream())
                    {
                        if (_chunckSize > 0)
                        {
                            await _registry.UploadBlobChuncks(uploadUri, config.digest, stream, _chunckSize, _debug);
                        }
                        else
                        {
                            await _registry.UploadBlob(uploadUri, config.digest, stream, config.size);
                        }
                    }
                });
        }

        public async Task PushLayers(Func<string, System.IO.Stream> layerStream)
        {
            foreach (var layer in _addedLayers)
            {
                await PushLayer(layerStream, layer);
            }
        }

        private async Task PushLayer(Func<string, System.IO.Stream> layerStream, BuilderLayer layer)
        {
            await RetryHelper.Retry(_retryUpload, _debug,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    if (_debug)
                    {
                        Console.WriteLine($"debug: uploading layer {layer.Digest}, {layer.Size} bytes. (upload uri: {uploadUri})");
                    }

                    using (var stream = layerStream($"{layer.Name}.tar.gz"))
                    {
                        if (_chunckSize > 0)
                        {
                            await _registry.UploadBlobChuncks(uploadUri, layer.Digest, stream, _chunckSize, _debug);
                        }
                        else
                        {
                            await _registry.UploadBlob(uploadUri, layer.Digest, stream, layer.Size);
                        }

                    }
                });
        }

        private async Task<string> GetUploadUri()
        {
            return await _registry.StartUpload(_targetImageName);
        }

        public async Task PushManifest(Func<System.IO.Stream> manifestStream)
        {
            await RetryHelper.Retry(_retryUpload, _debug,
                async () =>
                {
                    var destImageRef = ImageHelper.GetImageReference(_destination);
                    using (var stream = manifestStream())
                    {
                        if (_debug)
                        {
                            Console.WriteLine($"debug: uploading manifest");
                        }

                        await _registry.UploadManifest(_targetImageName, destImageRef, stream);
                    }
                });
        }
    }
}
