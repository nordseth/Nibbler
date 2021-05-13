using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler
{
    /// <summary>
    /// A folder as destination for a image
    /// Same format as FileImageDestination
    ///   manifest.json
    ///   sha265_[digest] (blob)
    /// </summary>
    public class FileImageSource : IImageSource
    {
        private readonly string _path;
        private readonly ILogger _logger;

        public FileImageSource(string path, ILogger logger)
        {
            _path = path;
            _logger = logger;
        }

        public string Name => _path;

        public Task<Stream> GetBlob(string digest)
        {
            var filename = FileHelper.DigestToFilename(digest);
            var file = File.OpenRead(Path.Combine(_path, filename));
            return Task.FromResult<Stream>(file);
        }

        public async Task<Image> LoadImage()
        {
            if (!Directory.Exists(_path))
            {
                throw new Exception($"error image {_path} not found!");
            }

            var manifestBytes = await File.ReadAllBytesAsync(Path.Combine(_path, FileImageDestination.ManifestFileName));

            var image = await Image.LoadMetadata(new ByteContentWrapper(manifestBytes, Encoding.UTF8), GetBlobContent);

            _logger.LogDebug($"Loaded image mata data from file {_path} with image digest: {image.ManifestDigest}");
            
            return image;
        }

        private async Task<IContents> GetBlobContent(string digest)
        {
            using (var blob = await GetBlob(digest))
            using (var ms = new MemoryStream())
            {
                blob.CopyTo(ms);
                return new ByteContentWrapper(ms.ToArray(), Encoding.UTF8);
            }
        }
    }
}
