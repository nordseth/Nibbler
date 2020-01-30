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
        private readonly ILogger _logger;
        private readonly int _chunckSize;
        private readonly int _retryUpload;

        public Pusher(string baseImage, string destination, IEnumerable<BuilderLayer> addedLayers, Registry registry, ILogger logger)
        {
            _baseImageName = ImageHelper.GetImageName(baseImage);
            _destination = destination;
            _targetImageName = ImageHelper.GetImageName(_destination);
            _addedLayers = addedLayers;
            _registry = registry;
            _logger = logger;
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
            _logger.LogDebug($"config {manifest.config.digest} ({manifest.config.size}) - {(configExists.HasValue ? "Exists" : "To be uploaded")}");

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

                string logMsg;
                if (toAdd && exists)
                {
                    logMsg = $"Exists (unchanged?)";
                }
                else if (toAdd)
                {
                    logMsg = $"To be uploaded";
                }
                else if (!exists && !toAdd)
                {
                    if (mounted)
                    {
                        logMsg = $"Was missing, Mount sucessful!";
                    }
                    else if (tryMount)
                    {
                        logMsg = $"Error Missing! (tried to mount)";
                    }
                    else
                    {
                        logMsg = $"Error Missing!";
                    }
                }
                else
                {
                    logMsg = $"Exists";
                }

                _logger.LogDebug($"layer {layer.digest} ({layer.size}) - {logMsg}");
            }

            if (missingLayer.Any())
            {
                throw new Exception($"Layers {string.Join(", ", missingLayer)} does not exsist in target repo");
            }
        }

        public async Task PushConfig(ManifestV2Layer config, Func<System.IO.Stream> configStream)
        {
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    _logger.LogDebug($"uploading config. (upload uri: {uploadUri})");

                    using (var stream = configStream())
                    {
                        if (_chunckSize > 0)
                        {
                            await _registry.UploadBlobChuncks(uploadUri, config.digest, stream, _chunckSize);
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
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    _logger.LogDebug($"uploading layer {layer.Digest}, {layer.Size} bytes. (upload uri: {uploadUri})");

                    using (var stream = layerStream($"{layer.Name}.tar.gz"))
                    {
                        if (_chunckSize > 0)
                        {
                            await _registry.UploadBlobChuncks(uploadUri, layer.Digest, stream, _chunckSize);
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
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var destImageRef = ImageHelper.GetImageReference(_destination);
                    using (var stream = manifestStream())
                    {
                        _logger.LogDebug($"uploading manifest");

                        await _registry.UploadManifest(_targetImageName, destImageRef, stream);
                    }
                });
        }
    }
}
