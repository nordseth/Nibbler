using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Nibbler.Utils;

namespace Nibbler
{
    public class Archive
    {
        private readonly string _outfile;
        private readonly bool _reproducable;
        private readonly bool _isLinux;

        public Archive(string outfile, bool reproducable)
        {
            _outfile = outfile;
            _reproducable = reproducable;

            _isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        }

        public IDictionary<string, (string, TarEntry)> Entries { get; } = new Dictionary<string, (string, TarEntry)>();

        public void CreateEntries(string source, string dest, int? owner, int? group, int? mode)
        {
            foreach (var path in Directory.EnumerateFiles(source))
            {
                var fileInfo = new FileInfo(path);

                var tarName = Path.Combine(dest, fileInfo.Name);
                tarName = tarName.Replace('\\', '/');
                var entry = TarEntry.CreateTarEntry(tarName);
                entry.Size = fileInfo.Length;

                // entry.TarHeader.Mode = isDir ? 1003 : 33216;
                // not sure about the mode?

                FillEntry(entry, fileInfo, owner, group, mode);

                Entries[tarName] = (path, entry);
            }

            foreach (var path in Directory.EnumerateDirectories(source))
            {
                var dirInfo = new DirectoryInfo(path);
                var tarName = Path.Combine(dest, dirInfo.Name);

                // needs to end with "/" to be a dir
                //var entry = TarEntry.CreateTarEntry(tarName);
                //entry.Size = 0;
                // _entries.Add((path, entry));

                CreateEntries(path, Path.Combine(dest, dirInfo.Name), owner, group, mode);
            }
        }

        public void CreateFolderEntry(string dest, int? ownerId, int? groupId, int? mode)
        {
            var tarName = dest.Replace('\\', '/');
            if (!tarName.EndsWith("/"))
            {
                tarName = tarName + "/";
            }

            var entry = TarEntry.CreateTarEntry(tarName);

            if (ownerId.HasValue)
            {
                entry.UserId = ownerId.Value;
            }

            if (groupId.HasValue)
            {
                entry.GroupId = groupId.Value;
            }

            if (mode.HasValue)
            {
                // 040xxx
                int maskedMode = 16384 + (mode.Value & 0b_0001_1111_1111);
                entry.TarHeader.Mode = maskedMode;
            }
            else
            {
                entry.TarHeader.Mode = Convert.ToInt32("040755", 8);
            }

            if (_reproducable)
            {
                entry.ModTime = new DateTime(2000, 1, 1);
            }

            Entries[tarName] = (null, entry);
        }

        public (string gzipDigest, string tarDigest) WriteFileAndCalcDigests()
        {
            var gzipDigest = SHA256.Create();
            var tarDigest = SHA256.Create();

            using (var outStream = File.Create(_outfile))
            using (var gzipDigestStream = GetHashStream(outStream, gzipDigest))
            using (var gzipStream = GetGZipStream(gzipDigestStream))
            using (var tarDigestStream = GetHashStream(gzipStream, tarDigest))
            using (var tarStream = new TarOutputStream(tarDigestStream))
            {
                foreach (var i in Entries)
                {
                    tarStream.PutNextEntry(i.Value.Item2);

                    // directories have null in source file
                    if (i.Value.Item1 != null)
                    {
                        using (var fileStream = File.OpenRead(i.Value.Item1))
                        {
                            fileStream.CopyTo(tarStream);
                        }
                    }

                    tarStream.CloseEntry();
                }
            }

            gzipDigest.TransformFinalBlock(new byte[0], 0, 0);
            tarDigest.TransformFinalBlock(new byte[0], 0, 0);

            return (gzipDigest.Hash.ToDigestString(), tarDigest.Hash.ToDigestString());
        }

        public long GetSize()
        {
            var info = new FileInfo(_outfile);
            return info.Length;
        }

        public static string PrintEntry(TarEntry e)
        {
            return $"{e.ModTime} {Convert.ToString(e.TarHeader.Mode, 8)} {e.UserId}/{e.UserName} {e.GroupId}/{e.GroupName} {e.Size} bytes - {e.Name} {e.File}{(e.IsDirectory ? " - D" : "")}";
        }

        private void FillEntry(TarEntry entry, FileInfo fileInfo, int? ownerId, int? groupId, int? mode)
        {
            if (_isLinux)
            {
                FillLinuxFileInfo(entry, fileInfo);
            }
            else
            {
                entry.ModTime = fileInfo.LastWriteTime;
                entry.TarHeader.Mode = Convert.ToInt32("100755", 8);
            }

            if (ownerId.HasValue)
            {
                entry.UserId = ownerId.Value;
                entry.UserName = String.Empty;
            }

            if (groupId.HasValue)
            {
                entry.GroupId = groupId.Value;
                entry.GroupName = String.Empty;
            }

            if (mode.HasValue)
            {
                // 100xxx
                int maskedMode = 32768 + (mode.Value & 0b_0001_1111_1111);
                entry.TarHeader.Mode = maskedMode;
            }

            if (_reproducable)
            {
                entry.ModTime = new DateTime(2000, 1, 1);
            }
        }

        // ref: https://github.com/mono/mono/tree/master/mcs/class/Mono.Posix/Mono.Unix
        private void FillLinuxFileInfo(TarEntry entry, FileInfo fileInfo)
        {
            var info = LinuxFileUtils.LinuxFileInfo.LStat(fileInfo.FullName);
            entry.ModTime = info.LastModTime;
            // 100xxx
            int maskedMode = 32768 + (info.Mode & 0b_0001_1111_1111);
            entry.TarHeader.Mode = maskedMode;
            entry.UserId = info.OwnerId;
            entry.UserName = null;
            entry.GroupId = info.GroupId;
            entry.GroupName = null;
        }

        private static Stream GetHashStream(Stream outStream, HashAlgorithm hasher)
        {
            return new HashOutputStream(outStream, hasher);
        }

        private Stream GetGZipStream(Stream outStream)
        {
            return new System.IO.Compression.GZipStream(outStream, System.IO.Compression.CompressionLevel.Optimal);
        }
    }
}
