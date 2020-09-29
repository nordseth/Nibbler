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
        private readonly Registry _registry;
        private readonly IEnumerable<BuilderLayer> _addedLayers;
        private readonly ILogger _logger;
        private readonly int _chunckSize;
        private readonly int _retryUpload;

        public Pusher(string baseImage, string destination, Registry registry, IEnumerable<BuilderLayer> addedLayers, ILogger logger)
        {
            _baseImageName = ImageHelper.GetImageName(baseImage);
            _destination = destination;
            _targetImageName = ImageHelper.GetImageName(_destination);
            _registry = registry;
            _addedLayers = addedLayers;
            _logger = logger;
            // set to 0 to diable chunckes
            _chunckSize = 0;
            _retryUpload = 3;
        }

        // This is a workaround for Openshift image streams, where layers look like they exsist but are only retrived on pull, not on mount.
        public bool FakePullAndRetryMount { get; set; } = true;

        public async Task<bool> CheckConfigExists(ManifestV2 manifest)
        {
            var configExists = await _registry.BlobExists(_targetImageName, manifest.config.digest);
            _logger.LogDebug($"config {manifest.config.digest} ({manifest.config.size}) - {(configExists.HasValue ? "Exists" : "To be uploaded")}");

            return configExists.HasValue;
        }

        public async Task<IEnumerable<ManifestV2Layer>> FindMissingLayers(ManifestV2 manifest, bool tryMount)
        {
            var missingLayers = new List<ManifestV2Layer>();
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
                            await MountLayer(layer);
                            mounted = true;
                        }
                        catch
                        {
                            missingLayers.Add(layer);
                        }
                    }
                    else
                    {
                        missingLayers.Add(layer);
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
                        logMsg = $"Missing! (tried to mount)";
                    }
                    else
                    {
                        logMsg = $"Missing!";
                    }
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
                            await _registry.UploadBlobChuncks(uploadUri, config.digest, stream, _chunckSize);
                        }
                        else
                        {
                            await _registry.UploadBlob(uploadUri, config.digest, stream, config.size);
                        }
                    }
                });
        }

        public async Task CopyLayers(Registry baseRegistry, string baseImageName, IEnumerable<ManifestV2Layer> missingLayers)
        {
            foreach (var layer in missingLayers)
            {
                await CopyLayer(layer, baseImageName, baseRegistry);
            }
        }

        private async Task CopyLayer(ManifestV2Layer layer, string baseImageName, Registry baseRegistry)
        {
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    _logger.LogDebug($"copy layer from {baseRegistry.BaseUri}{baseImageName} - {layer.digest}, {layer.size} bytes.");

                    using (var stream = await baseRegistry.DownloadBlob(baseImageName, layer.digest))
                    {
                        if (_chunckSize > 0)
                        {
                            await _registry.UploadBlobChuncks(uploadUri, layer.digest, stream, _chunckSize);
                        }
                        else
                        {
                            await _registry.UploadBlob(uploadUri, layer.digest, stream, layer.size);
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
                    if (string.IsNullOrEmpty( destImageRef))
                    {
                        destImageRef = "latest";
                        _logger.LogWarning($"no to image tag specified, will use \"latest\"");
                    }

                    using (var stream = manifestStream())
                    {
                        _logger.LogDebug($"uploading manifest {_targetImageName}:{destImageRef}");

                        await _registry.UploadManifest(_targetImageName, destImageRef, stream);
                    }
                });
        }

        private async Task MountLayer(ManifestV2Layer layer)
        {
            try
            {
                await _registry.MountBlob(_targetImageName, layer.digest, _baseImageName);
            }
            catch
            {
                if (FakePullAndRetryMount)
                {
                    await FakePullAndMount(layer);
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task FakePullAndMount(ManifestV2Layer layer)
        {
            _logger.LogDebug($"Mount failed, trying to fake pull and retry {_baseImageName} {layer.digest}");

            try
            {
                var blobStream = await _registry.DownloadBlob(_baseImageName, layer.digest);
                await blobStream.CopyToAsync(System.IO.Stream.Null);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, $"Fake pull failed");
                throw;
            }

            await _registry.MountBlob(_targetImageName, layer.digest, _baseImageName);
        }
    }
}
