using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nibbler.Utils;

namespace Nibbler
{
    public class BuilderState
    {
        public string BaseImage { get; set; }
        public bool Insecure { get; set; }
        public bool SkipTlsVerify { get; set; }
        public string Auth { get; set; }
        public List<BuilderLayer> LayersAdded { get; set; } = new List<BuilderLayer>();

        public Registry GetRegistry()
        {
            var baseUri = ImageHelper.GetRegistryBaseUrl(BaseImage, Insecure);
            var reg = new Registry(baseUri, SkipTlsVerify);

            if (Auth != null)
            {
                reg.UseAuthorization(Auth);
            }

            return reg;
        }
    }

    public class BuilderLayer
    {
        public string Source { get; set; }
        public string Dest { get; set; }
        public string Name { get; set; }
        public string Digest { get; set; }
        public string DiffId { get; set; }
        public long Size { get; set; }

        public override string ToString()
        {
            return $"{Name}: {Digest} - {Size} bytes - diff_id: {DiffId}";
        }
    }
}
