using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Writer
{
    public class RegistryImagePusher : IImageWriter
    {
        private readonly string _destination;
        private readonly string _targetImageName;
        private readonly ILogger _logger;
        private readonly int _chunckSize;
        private readonly int _retryUpload;

        public RegistryImagePusher(string destination, Registry registry, ILogger logger)
        {
            _destination = destination;
            _targetImageName = ImageHelper.GetImageName(_destination);
            Registry = registry;
            _logger = logger;
            // set to 0 to diable chunckes
            _chunckSize = 0;
            _retryUpload = 3;
        }

        public Registry Registry { get; }

        public async Task WriteImage(Image image, Func<string, Task<Stream>> layerSource)
        {
            await PushConfig(image);
            await PushLayers(image, layerSource);
            await PushManifest(image);
        }

        private async Task PushConfig(Image image)
        {
            var config = image.Manifest.config;
            var configExists = await Registry.BlobExists(_targetImageName, config.digest);

            if (configExists.HasValue)
            {
                _logger.LogDebug($"config {config.digest} ({config.size}) - already exists");
            }
            else
            {
                await PushBlob("config", config, () => Task.FromResult((Stream)new MemoryStream(image.ConfigBytes)));
            }
        }

        private async Task PushLayers(Image image, Func<string, Task<Stream>> layerSource)
        {
            foreach (var layer in image.Manifest.layers)
            {
                var existsBlobSize = await Registry.BlobExists(_targetImageName, layer.digest);
                if (existsBlobSize.HasValue)
                {
                    _logger.LogDebug($"layer {layer.digest} ({layer.size}) - Exists");
                }
                else
                {
                    await PushBlob("layer", layer, () => layerSource(layer.digest));
                }
            }
        }

        private async Task PushBlob(string type, ManifestV2Layer layer, Func<Task<Stream>> layerSource)
        {
            await RetryHelper.Retry(_retryUpload, _logger,
                async () =>
                {
                    var uploadUri = await GetUploadUri();
                    _logger.LogDebug($"pushing {type} - {layer.digest}, {layer.size} bytes.");

                    using (var stream = await layerSource())
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

        private async Task PushManifest(Image image)
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

                    using (var stream = new MemoryStream(image.ManifestBytes))
                    {
                        _logger.LogDebug($"uploading manifest {_targetImageName}:{destImageRef}");

                        await Registry.UploadManifest(_targetImageName, destImageRef, stream);
                    }
                });
        }

        private async Task<string> GetUploadUri()
        {
            return await Registry.StartUpload(_targetImageName);
        }
    }
}
