using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.Models
{
    /// <summary>
    /// https://github.com/opencontainers/image-spec/blob/master/config.md
    /// </summary>
    public class ImageV1
    {
        public const string MimeType = "application/vnd.oci.image.config.v1+json";
        public const string AltMimeType = "application/vnd.docker.container.image.v1+json";

        public string created { get; set; } // optional
        public string author { get; set; } // optional
        public string architecture { get; set; } = "amd64";
        public string os { get; set; } = "linux";
        public ImageV1Config config { get; set; } = new ImageV1Config();
        public ImageV1Rootfs rootfs { get; set; } = new ImageV1Rootfs();
        public List<ImageV1History> history { get; set; } // optional
    }

    public class ImageV1Config
    {
        public string User { get; set; } // optional
        public Dictionary<string, object> ExposedPorts { get; set; } // optional
        public List<string> Env { get; set; } // optional
        public List<string> Entrypoint { get; set; } // optional
        public List<string> Cmd { get; set; }  // optional
        public Dictionary<string, object> Volumes { get; set; }  // optional
        public string WorkingDir { get; set; }  // optional
        public Dictionary<string, string> Labels { get; set; }  // optional
        public string StopSignal { get; set; } // optional
    }

    public class ImageV1Rootfs
    {
        public List<string> diff_ids { get; set; }
        public string type { get; set; } = "layers";
    }

    public class ImageV1History
    {
        public string created { get; set; } // optional
        public string author { get; set; } // optional
        public string created_by { get; set; } // optional
        public string comment { get; set; } // optional
        public bool? empty_layer { get; set; } // optional

        public static ImageV1History Create(string createdBy, bool? empty)
        {
            return new ImageV1History
            {
                author = Builder.ProgramName,
                created = "0001-01-01T00:00:00Z",
                created_by = createdBy,
                empty_layer = empty,
            };
        }
    }
}