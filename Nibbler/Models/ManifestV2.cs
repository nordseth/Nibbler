using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nibbler.Models
{
    /// <summary>
    /// https://github.com/docker/distribution/blob/master/docs/spec/manifest-v2-2.md
    /// </summary>
    public class ManifestV2
    {
        public const string MimeType = "application/vnd.docker.distribution.manifest.v2+json";
        public const string AltMimeType = "application/vnd.oci.image.manifest.v1+json";

        public int schemaVersion { get; set; } = 2;
        public string mediaType { get; set; } = MimeType;
        public ManifestV2Layer config { get; set; } = new ManifestV2Layer();
        public List<ManifestV2Layer> layers { get; set; } = new List<ManifestV2Layer>();

        public ManifestV2 Clone()
        {
            return new ManifestV2
            {
                schemaVersion = schemaVersion,
                mediaType = mediaType,
                config = config?.Clone(),
                layers = layers?.Select(l => l.Clone()).ToList(),
            };
        }
    }

    public class ManifestV2Layer
    {
        public const string MediaType = "application/vnd.oci.image.layer.v1.tar+gzip";
        public const string AltMediaType = "application/vnd.docker.image.rootfs.diff.tar.gzip";

        public string mediaType { get; set; } = AltMediaType;
        public long size { get; set; }
        public string digest { get; set; }
        public List<string> urls { get; set; }

        public ManifestV2Layer Clone()
        {
            return new ManifestV2Layer
            {
                mediaType = mediaType,
                size = size,
                digest = digest,
                urls = urls?.ToList(),
            };
        }
    }
}