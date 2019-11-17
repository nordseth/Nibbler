using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.Models
{
    public class BuilderLayer
    {
        public string Name { get; set; }
        public string Digest { get; set; }
        public string DiffId { get; set; }
        public long Size { get; set; }

        public string Description { get; set; }

        public override string ToString()
        {
            return $"{Name}: {Digest} - {Size} bytes - diff_id: {DiffId}";
        }
    }
}
