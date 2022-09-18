using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nibbler.Command
{
    public class AddArgument
    {
        public string Source { get; set; }
        public string Dest { get; set; }
        public int? OwnerId { get; set; }
        public int? GroupId { get; set; }
        public int? Mode { get; set; }

        public static AddArgument Parse(string s, bool isFolder)
        {
            var split = s.Split(new[] { ':', ';' });
            if (!isFolder && split.Length < 2)
            {
                throw new Exception($"Invalid add {s}");
            }

            int i = 0;
            string source = null;
            if (!isFolder)
            {
                source = split[i++];
            }

            string dest = split[i++];

            bool hasOwner = int.TryParse(split.Skip(i++).FirstOrDefault(), out int ownerId);
            bool hasGroup = int.TryParse(split.Skip(i++).FirstOrDefault(), out int groupId);
            string modeString = split.Skip(i++).FirstOrDefault();
            int? mode = null;
            if (!string.IsNullOrEmpty(modeString))
            {
                mode = Convert.ToInt32(modeString, 8);
            }

            return new AddArgument
            {
                Source = source,
                Dest = dest,
                OwnerId = hasOwner ? ownerId : (int?)null,
                GroupId = hasGroup ? groupId : (int?)null,
                Mode = mode,
            };
        }
    }
}
