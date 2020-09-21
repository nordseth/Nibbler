using System;
using System.Collections.Generic;
using System.Text;

namespace Nibbler.LinuxFileUtils
{
    public class FileInfo
    {
        public int Mode { get; set; }
        public bool IsLink { get; set; }
        public int OwnerId { get; set; }
        public int GroupId { get; set; }
        public DateTime LastModTime { get; set; }

        public override string ToString()
        {
            return $"{Convert.ToString(Mode, 8)} {OwnerId} {GroupId} - {LastModTime:u}";
        }
    }
}
