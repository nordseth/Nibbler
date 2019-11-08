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
        private readonly bool _tryMount;
        private readonly bool _debug;
        private readonly string _baseImageName;
        private readonly string _targetImageName;
        private readonly int _chunckSize;
        private readonly bool _reuseUploadUri;
        private readonly int _retryUpload;
        private string _uploadUri;

        public Pusher(BuilderState state, string pushTo, bool tryMount, bool debug)
        {
            State = state;
            PushTo = pushTo;
            _debug = debug;
            _tryMount = tryMount;
            _baseImageName = ImageHelper.GetImageName(state.BaseImage);
            _targetImageName = ImageHelper.GetImageName(PushTo);
            // set to 0 to diable chunckes
            _chunckSize = 1000000;
            _reuseUploadUri = false;
            _retryUpload = 3;
            Registry = state.GetRegistry();
        }

        public BuilderState State { get; }
        public string PushTo { get; }
        public Registry Registry { get; }

        public void ValidateDest()
        {
            var destRegistryUri = ImageHelper.GetRegistryBaseUrl(PushTo, State.Insecure);
            if (Registry.BaseUri != destRegistryUri)
            {
                throw new Exception($"Source ({Registry.BaseUri.Authority}) and destination ({destRegistryUri.Authority}) registries must be the same.");
            }
        }

        public async Task<bool> CheckConfigExists(ManifestV2 manifest)
        {
            var configExists = await Registry.BlobExists(_targetImageName, manifest.config.digest);
            if (_debug)
            {
                Console.WriteLine($"debug: config {manifest.config.digest} ({manifest.config.size}) - {(configExists.HasValue ? "Exists" : "To be uploaded")}");
            }

            return configExists.HasValue;
        }

        public async Task ValidateLayers(ManifestV2 manifest)
        {
            var missingLayer = new List<string>();
            foreach (var layer in manifest.layers)
            {
                var existsBlobSize = await Registry.BlobExists(_targetImageName, layer.digest);
                var exists = existsBlobSize.HasValue;
                var toAdd = State.LayersAdded.Any(x => x.Digest.Equals(layer.digest));
                bool mounted = false;
                if (!exists && !toAdd)
                {
                    if (_tryMount)
                    {
                        try
                        {
                            await Registry.MountBlob(_targetImageName, layer.digest, _baseImageName);
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
                        else if (_tryMount)
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
                            await Registry.UploadBlobChuncks(uploadUri, config.digest, stream, _chunckSize, _debug);
                        }
                        else
                        {
                            await Registry.UploadBlob(uploadUri, config.digest, stream, config.size);
                        }
                    }
                });
        }

        public async Task PushLayers(Func<string, System.IO.Stream> layerStream)
        {
            foreach (var layer in State.LayersAdded)
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
                            await Registry.UploadBlobChuncks(uploadUri, layer.Digest, stream, _chunckSize, _debug);
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
            if (!_reuseUploadUri || _uploadUri == null)
            {
                _uploadUri = await Registry.StartUpload(_targetImageName);
            }

            return _uploadUri;
        }

        public async Task PushManifest(Func<System.IO.Stream> manifestStream)
        {
            await RetryHelper.Retry(_retryUpload, _debug,
                async () =>
                {
                    var destImageRef = ImageHelper.GetImageReference(PushTo);
                    using (var stream = manifestStream())
                    {
                        if (_debug)
                        {
                            Console.WriteLine($"debug: uploading manifest");
                        }

                        await Registry.UploadManifest(_targetImageName, destImageRef, stream);
                    }
                });
        }
    }
}
