using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Nibbler.Utils
{
    public static class FileHelper
    {
        public static JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        // https://github.com/docker/distribution/blob/master/docs/spec/api.md#digest-parameter
        public static string Digest(this Stream stream)
        {
            using (var hasher = SHA256.Create())
            {
                var hash = hasher.ComputeHash(stream);
                return ToDigestString(hash);
            }
        }

        public static string Digest(this byte[] bytes)
        {
            using (var hasher = SHA256.Create())
            {
                var hash = hasher.ComputeHash(bytes);
                return ToDigestString(hash);
            }
        }

        public static string ToDigestString(this byte[] hash)
        {
            return $"sha256:{BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant()}";
        }

        public static string JsonSerialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, JsonSerializerSettings);
        }

        public static string AsOctalString(this int? mode)
        {
            if (mode.HasValue)
            {
                return Convert.ToString(mode.Value, 8);
            }
            else
            {
                return null;
            }
        }

        public static string DigestToFilename(string digest)
        {
            return digest.Replace(":", "_");
        }
    }
}
