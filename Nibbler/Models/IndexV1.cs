using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Models
{
    /// <summary>
    /// https://github.com/opencontainers/image-spec/blob/main/image-index.md
    /// </summary>
    public class IndexV1
    {
        public const string MimeType = "application/vnd.oci.image.index.v1+json";
        public const string AltMimeType = "application/vnd.docker.distribution.manifest.list.v2+json";

        public int schemaVersion { get; set; } = 2;
        public string mediaType { get; set; } = MimeType;
        public List<IndexV1Manifest> manifests { get; set; }
        public Dictionary<string, string> annotations { get; set; }
    }

    /// <summary>
    /// https://github.com/opencontainers/image-spec/blob/main/image-index.md
    /// and https://github.com/opencontainers/image-spec/blob/main/descriptor.md#properties
    /// </summary>
    public class IndexV1Manifest
    {
        public string mediaType { get; set; } = ManifestV2.MimeType;
        public string digest { get; set; } 
        public long size { get; set; }
        public List<string> urls { get; set; }
        public Dictionary<string, string> annotations { get; set; }
        public string data { get; set; }
        public string artifactType { get; set; }
        public IndexV1Platform platform { get; set; }
    }

    public class IndexV1Platform
    {
        public const string DefaultOS = "linux";
        public const string DefaultArch = "amd64";

        public string architecture { get; set; } = DefaultOS;
        public string os { get; set; } = DefaultArch;
        public string variant { get; set; }
        public List<string> features { get; set; }
    }
}
