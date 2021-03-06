﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Tmds.Linux;

namespace Nibbler.LinuxFileUtils
{
    public static class LinuxFileInfo
    {
        public unsafe static FileInfo LStat(string path)
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            stat stat = default;

            fixed (byte* pathname = bytes)
            {
                int rv = LibC.lstat(pathname, &stat);
                if (rv == -1)
                {
                    PlatformException.Throw();
                }
            }

            return ToFileInfo(stat);
        }

        public unsafe static string Readlink(string path)
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);

            int bufferLength = 1024;
            byte* buffer = stackalloc byte[bufferLength];

            long size;
            fixed (byte* pathname = pathBytes)
            {
                size = LibC.readlink(pathname, buffer, bufferLength);
                if (size == -1)
                {
                    PlatformException.Throw();
                }
            }

            return Encoding.UTF8.GetString(buffer, (int)size);
        }

        private static FileInfo ToFileInfo(stat stat)
        {
            return new FileInfo
            {
                Mode = Convert.ToInt32(stat.st_mode.ToString()),
                IsLink = LibC.S_ISLNK(stat.st_mode),
                OwnerId = Convert.ToInt32(stat.st_uid.ToString()),
                GroupId = Convert.ToInt32(stat.st_gid.ToString()),
                LastModTime = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(stat.st_mtim.tv_sec.ToString())).UtcDateTime,
            };
        }
    }
}
