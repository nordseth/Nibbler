using Newtonsoft.Json;
using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler
{
    public class RegistryImageSource : IImageSource
    {
        private readonly string _imageName;
        private readonly string _imageRef;
        private readonly ILogger _logger;

        public string Name => Registry.BaseUri.ToString() + _imageName;
        public Registry Registry { get; }

        public RegistryImageSource(string imageName, string imageRef, Registry registry, ILogger logger)
        {
            _imageName = imageName;
            _imageRef = imageRef;
            Registry = registry;
            _logger = logger;
        }

        public async Task<Image> LoadImage()
        {
            _logger.LogDebug($"--from-image {Registry.BaseUri}, {_imageName}, {_imageRef}");

            var image = await LoadImageMetadata();

            _logger.LogDebug($"Loaded image mata data for image digest: {image.ManifestDigest}");

            return image;
        }

        public async Task<Image> LoadImageMetadata()
        {
            var manifestContent = await Registry.GetManifest(_imageName, _imageRef);

            var image = await Image.LoadMetadata(new HttpContentWrapper(manifestContent), GetImageConfig);

            return image;
        }

        public Task<Stream> GetBlob(string digest)
        {
            return Registry.DownloadBlob(_imageName, digest);
        }

        private async Task<IContents> GetImageConfig(string digest)
        {
            var contents = await Registry.GetImageConfig(_imageName, digest);
            return new HttpContentWrapper(contents);
        }

        public static RegistryImageSource Create(string image, string username, string password, bool insecure, bool skipTlsVerify, string dockerConfig, ILogger logger)
        {
            var fromUri = ImageHelper.GetRegistryBaseUrl(image, insecure);

            var dockerConfigCredentials = new DockerConfigCredentials(dockerConfig);
            var fromRegAuthHandler = new AuthenticationHandler(
                ImageHelper.GetRegistryName(image),
                dockerConfigCredentials,
                logger);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                fromRegAuthHandler.SetCredentials(username, password);
            }

            var registry = new Registry(fromUri, logger, fromRegAuthHandler, skipTlsVerify);

            logger.LogDebug($"using {fromUri} for pull{(skipTlsVerify ? ", skipTlsVerify" : "")}");

            return new RegistryImageSource(ImageHelper.GetImageName(image), ImageHelper.GetImageReference(image), registry, logger);
        }
    }
}
