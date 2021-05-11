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
            throw new NotImplementedException();
        }

        public Task<Image> LoadImage()
        {
            throw new NotImplementedException();
        }

        private static string FixFilename(string digest)
        {
            return digest.Replace(":", "_");
        }
    }
}
