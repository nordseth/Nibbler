using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

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

        public ImageV1 Clone()
        {
            return new ImageV1
            {
                created = created,
                author = author,
                architecture = architecture,
                os = os,
                config = config.Clone(),
                rootfs = rootfs.Clone(),
                history = history?.Select(h => h.Clone()).ToList(),
            };
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this, JsonContext.Default.ImageV1);
        }
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

        public ImageV1Config Clone()
        {
            return new ImageV1Config
            {
                User = User,
                ExposedPorts = CloneDict(ExposedPorts),
                Env = Env?.ToList(),
                Entrypoint = Entrypoint?.ToList(),
                Cmd = Cmd?.ToList(),
                Volumes = CloneDict(Volumes),
                WorkingDir = WorkingDir,
                Labels = CloneDict(Labels),
                StopSignal = StopSignal,
            };
        }

        private Dictionary<TKey, TValue> CloneDict<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            if (dict == null)
            {
                return null;
            }

            return new Dictionary<TKey, TValue>(dict);
        }
    }

    public class ImageV1Rootfs
    {
        public List<string> diff_ids { get; set; }
        public string type { get; set; } = "layers";

        public ImageV1Rootfs Clone()
        {
            return new ImageV1Rootfs
            {
                diff_ids = diff_ids?.ToList(),
                type = type,
            };
        }
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
                author = Program.ProgramName,
                created = "0001-01-01T00:00:00Z",
                created_by = createdBy,
                empty_layer = empty,
            };
        }

        public ImageV1History Clone()
        {
            return new ImageV1History
            {
                created = created,
                author = author,
                created_by = created_by,
                comment = comment,
                empty_layer = empty_layer,
            };
        }
    }
}