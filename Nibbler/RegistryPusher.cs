using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler
{
    public class RegistryPusher
    {
        private readonly string _destination;
        private readonly string _targetImageName;
        private readonly IEnumerable<BuilderLayer> _addedLayers;
        private readonly ILogger _logger;
        private readonly int _chunckSize;
        private readonly int _retryUpload;

        public RegistryPusher(string destination, Registry registry, IEnumerable<BuilderLayer> addedLayers, ILogger logger)
        {
            _destination = destination;
            _targetImageName = ImageHelper.GetImageName(_destination);
            Registry = registry;
            _addedLayers = addedLayers;
            _logger = logger;
            // set to 0 to diable chunckes
            _chunckSize = 0;
            _retryUpload = 3;
        }

        public Registry Registry { get; }

        public async Task<bool> CheckConfigExists(ManifestV2 manifest)
        {
            var configExists = await Registry.BlobExists(_targetImageName, manifest.config.digest);
            _logger.LogDebug($"config {manifest.config.digest} ({manifest.config.size}) - {(configExists.HasValue ? "Exists" : "To be uploaded")}");

            return configExists.HasValue;
        }

        public async Task<IEnumerable<ManifestV2Layer>> FindMissingLayers(ManifestV2 manifest)
        {
            var missingLayers = new List<ManifestV2Layer>();
            foreach (var layer in manifest.layers)
            {
                var existsBlobSize = await Registry.BlobExists(_targetImageName, layer.digest);
                var exists = existsBlobSize.HasValue;
                var toAdd = _addedLayers.Any(x => x.Digest.Equals(layer.digest));
                if (!exists && !toAdd)
                {
                    missingLayers.Add(layer);
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
                    logMsg = $"Missing!";
                }
                else
                {
                    logMsg = $"Exists";
                }

                _logger.LogDebug($"layer {layer.digest} ({layer.size}) - {logMsg}");
            }

            return missingLayers;
        }

        public async Task PushConfig(ManifestV2Layer config, Func<System.IO.Stream> configStream)
        {
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    _logger.LogDebug($"uploading config.");

                    using (var stream = configStream())
                    {
                        if (_chunckSize > 0)
                        {
                            await Registry.UploadBlobChuncks(uploadUri, config.digest, stream, _chunckSize);
                        }
                        else
                        {
                            await Registry.UploadBlob(uploadUri, config.digest, stream, config.size);
                        }
                    }
                });
        }

        public async Task CopyLayers(IImageSource imageSource, IEnumerable<ManifestV2Layer> missingLayers)
        {
            foreach (var layer in missingLayers)
            {
                await CopyLayer(layer, imageSource);
            }
        }

        private async Task CopyLayer(ManifestV2Layer layer, IImageSource imageSource)
        {
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    _logger.LogDebug($"copy layer from {imageSource.Name} - {layer.digest}, {layer.size} bytes.");

                    using (var stream = await imageSource.GetBlob(layer.digest))
                    {
                        if (_chunckSize > 0)
                        {
                            await Registry.UploadBlobChuncks(uploadUri, layer.digest, stream, _chunckSize);
                        }
                        else
                        {
                            await Registry.UploadBlob(uploadUri, layer.digest, stream, layer.size);
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
                    _logger.LogDebug($"uploading layer {layer.Digest}, {layer.Size} bytes.");

                    using (var stream = layerStream($"{layer.Name}.tar.gz"))
                    {
                        if (_chunckSize > 0)
                        {
                            await Registry.UploadBlobChuncks(uploadUri, layer.Digest, stream, _chunckSize);
                        }
                        else
                        {
                            await Registry.UploadBlob(uploadUri, layer.Digest, stream, layer.Size);
                        }

                    }
                });
        }

        private async Task<string> GetUploadUri()
        {
            return await Registry.StartUpload(_targetImageName);
        }

        public async Task PushManifest(Func<System.IO.Stream> manifestStream)
        {
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var destImageRef = ImageHelper.GetImageReference(_destination);
                    if (string.IsNullOrEmpty(destImageRef))
                    {
                        destImageRef = "latest";
                        _logger.LogWarning($"no to image tag specified, will use \"latest\"");
                    }

                    using (var stream = manifestStream())
                    {
                        _logger.LogDebug($"uploading manifest {_targetImageName}:{destImageRef}");

                        await Registry.UploadManifest(_targetImageName, destImageRef, stream);
                    }
                });
        }
    }
}
