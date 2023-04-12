using Nibbler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nibbler
{
    public interface IImageDestination
    {
        Task<bool> CheckConfigExists(ManifestV2 manifest);
        Task CopyLayers(IImageSource imageSource, IEnumerable<ManifestV2Layer> missingLayers);
        Task<IEnumerable<ManifestV2Layer>> FindMissingLayers(ManifestV2 manifest);
        Task PushConfig(ManifestV2Layer config, Func<Stream> configStream);
        Task PushLayers(Func<string, Stream> layerStream);
        Task PushManifest(string mediaType, Func<Stream> manifestStream);
    }
}