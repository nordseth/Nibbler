using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Nibbler.Utils
{
    public static class ImageHelper
    {
        public static string GetImageName(string image)
        {
            var tmpUri = new Uri($"https://{image}");
            var path = tmpUri.PathAndQuery;
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            var splitTag = path.Split(new[] { ':' }, 2);
            var splitDigest = path.Split(new[] { '@' }, 2);

            if (splitTag.Length > 1)
            {
                return splitTag[0];
            }
            else if (splitDigest.Length > 1)
            {
                return splitDigest[0];
            }
            else
            {
                return path;
            }
        }

        public static string GetImageReference(string image)
        {
            var tmpUri = new Uri($"https://{image}");
            var path = tmpUri.PathAndQuery;
            var splitTag = path.Split(new[] { ':' }, 2);
            var splitDigest = path.Split(new[] { '@' }, 2);

            if (splitTag.Length > 1)
            {
                return splitTag[1];
            }
            else if (splitDigest.Length > 1)
            {
                return splitDigest[1];
            }
            else
            {
                return null;
            }
        }

        public static string GetRegistryName(string image)
        {
            var imageUri = new Uri($"https://{image}");
            return imageUri.Authority;
        }

        public static Uri GetRegistryBaseUrl(string image, bool insecure)
        {
            return new Uri($"{(insecure ? "http" : "https")}://{GetRegistryName(image)}");
        }
    }
}
