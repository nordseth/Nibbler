﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Tar;
using Nibbler.Utils;

namespace Nibbler
{
    public class Archive
    {
        private readonly string _outfile;
        private readonly bool _reproducable;
        private readonly string _ignoreFile;
        private readonly IEnumerable<string> _ignorePaths;
        private readonly ILogger _logger;
        private readonly bool _isLinux;

        public Archive(string outfile, bool reproducable, IEnumerable<string> ignorePaths, string ignoreFile, ILogger logger)
        {
            _outfile = outfile;
            _reproducable = reproducable;
            _ignoreFile = ignoreFile;
            _ignorePaths = ignorePaths ?? Enumerable.Empty<string>();
            _logger = logger;

            _isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        }

        public IDictionary<string, (string, TarEntry)> Entries { get; } = new Dictionary<string, (string, TarEntry)>();

        public void CreateEntries(string source, string dest, int? owner, int? group, int? mode)
        {
            var ignore = CreateIgnore(source);

            CreateEntries(source, dest, ignore, ModifyEntry);

            void ModifyEntry(TarEntry entry)
            {
                if (owner.HasValue)
                {
                    entry.UserId = owner.Value;
                    entry.UserName = String.Empty;
                }

                if (group.HasValue)
                {
                    entry.GroupId = group.Value;
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
        }

        private Ignore.Ignore CreateIgnore(string source)
        {
            var ignore = new Ignore.Ignore();
            if (_ignoreFile != null)
            {
                var ignoreFilePath = Path.Combine(source, _ignoreFile);
                if (File.Exists(ignoreFilePath))
                {
                    _logger?.LogDebug($"Using ignore file \"{ignoreFilePath}\".");
                    var fileContent = File.ReadAllLines(ignoreFilePath);
                    ignore.Add(fileContent);
                }
            }

            return ignore;
        }

        private void CreateEntries(string source, string dest, Ignore.Ignore ignore, Action<TarEntry> modifyEntry)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(source))
            {
                if (ShouldIgnore(source, path, ignore))
                {
                    _logger?.LogDebug($"Ignoring \"{path}\" in layer.");
                }
                else
                {
                    var attr = File.GetAttributes(path);
                    if (attr.HasFlag(FileAttributes.Directory))
                    {
                        CreateFolderEntry(path, dest, ignore, modifyEntry);
                    }
                    else
                    {
                        CreateFileEntry(path, dest, modifyEntry);
                    }
                }
            }
        }

        private bool ShouldIgnore(string root, string path, Ignore.Ignore ignore)
        {
            var relative = Path.GetRelativePath(root, path);
            var full = Path.GetFullPath(path);
            if (_ignorePaths.Any(p => p == full))
            {
                return true;
            }
            else if (_ignoreFile != null && relative == _ignoreFile)
            {
                return true;
            }
            else if (ignore.IsIgnored(relative))
            {
                return true;
            }

            return false;
        }

        private void CreateFileEntry(string path, string dest, Action<TarEntry> modifyEntry)
        {
            var fileInfo = new FileInfo(path);

            var tarName = Path.Combine(dest, fileInfo.Name);
            tarName = tarName.Replace('\\', '/');
            var entry = TarEntry.CreateTarEntry(tarName);
            entry.Size = fileInfo.Length;

            if (_isLinux)
            {
                FillLinuxFileInfo(entry, fileInfo);
            }
            else
            {
                entry.ModTime = fileInfo.LastWriteTime;
                entry.TarHeader.Mode = Convert.ToInt32("100755", 8);
            }

            modifyEntry(entry);

            Entries[tarName] = (path, entry);
        }

        private void CreateFolderEntry(string path, string dest, Ignore.Ignore ignore, Action<TarEntry> modifyEntry)
        {
            var dirInfo = new DirectoryInfo(path);

            // not creating entries in tar for directories, just iterating over the contents
            // unless its a symlink...
            if (_isLinux)
            {
                var info = LinuxFileUtils.LinuxFileInfo.LStat(dirInfo.FullName);
                if (info.IsLink)
                {
                    CreateSymlinkFolder(path, Path.Combine(dest, dirInfo.Name), dirInfo.FullName, info, modifyEntry);
                    return;
                }
            }

            CreateEntries(path, Path.Combine(dest, dirInfo.Name), ignore, modifyEntry);
        }

        private void CreateSymlinkFolder(string path, string tarName, string fullName, LinuxFileUtils.FileInfo info, Action<TarEntry> modifyEntry)
        {
            tarName = tarName.Replace('\\', '/');
            var entry = TarEntry.CreateTarEntry(tarName);

            entry.ModTime = info.LastModTime;
            // 100xxx
            int maskedMode = 32768 + (info.Mode & 0b_0001_1111_1111);
            entry.TarHeader.Mode = maskedMode;
            entry.UserId = info.OwnerId;
            entry.UserName = null;
            entry.GroupId = info.GroupId;
            entry.GroupName = null;

            entry.TarHeader.TypeFlag = TarHeader.LF_SYMLINK;
            entry.Size = 0;
            entry.TarHeader.LinkName = LinuxFileUtils.LinuxFileInfo.Readlink(fullName);

            modifyEntry(entry);

            Entries[tarName] = (path, entry);
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

            if (info.IsLink)
            {
                entry.TarHeader.TypeFlag = TarHeader.LF_SYMLINK;
                entry.Size = 0;
                entry.TarHeader.LinkName = LinuxFileUtils.LinuxFileInfo.Readlink(fileInfo.FullName);
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
            using (var tarStream = new TarOutputStream(tarDigestStream, null))
            {
                foreach (var i in Entries)
                {
                    tarStream.PutNextEntry(i.Value.Item2);

                    // directories have null in source file, also exclude links
                    if (i.Value.Item1 != null && i.Value.Item2.TarHeader.TypeFlag != TarHeader.LF_SYMLINK)
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
            string desc = null;
            if (e.IsDirectory)
            {
                desc = " - D";
            }
            else if (e.TarHeader.TypeFlag == TarHeader.LF_SYMLINK)
            {
                desc = $" - L -> {e.TarHeader.LinkName}";
            }

            return $"{e.ModTime} {Convert.ToString(e.TarHeader.Mode, 8)} {e.UserId}/{e.UserName} {e.GroupId}/{e.GroupName} {e.Size} bytes - {e.Name} {e.File}{desc}";
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
