using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using Mono.Unix;
using Nibbler.Utils;

namespace Nibbler
{
    public class Archive
    {
        private readonly string _outfile;
        private readonly string _tmpfile;
        private readonly bool _reproducable;
        private readonly List<(string, TarEntry)> _entries = new List<(string, TarEntry)>();
        private readonly bool _isLinux;

        public Archive(string outfile, string tmpfile, bool reproducable)
        {
            _outfile = outfile;
            _tmpfile = tmpfile;
            _reproducable = reproducable;

            _isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
        }

        public IEnumerable<(string, TarEntry)> Entries => _entries.AsReadOnly();

        // ref: https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples#createFull
        //      https://github.com/icsharpcode/SharpZipLib/tree/master/src/ICSharpCode.SharpZipLib/Tar
        public void CreateEntries(string source, string dest, bool addFolders, string owner, string group)
        {
            if (addFolders)
            {
                // add folders on dest
                throw new NotImplementedException();
            }

            foreach (var path in Directory.EnumerateFiles(source))
            {
                var fileInfo = new FileInfo(path);

                var tarName = Path.Combine(dest, fileInfo.Name);
                tarName = tarName.Replace('\\', '/');
                var entry = TarEntry.CreateTarEntry(tarName);
                entry.Size = fileInfo.Length;

                // entry.TarHeader.Mode = isDir ? 1003 : 33216;
                // not sure about the mode?

                FillEntry(entry, fileInfo, owner, group);

                _entries.Add((path, entry));
            }

            foreach (var path in Directory.EnumerateDirectories(source))
            {
                var dirInfo = new DirectoryInfo(path);
                var tarName = Path.Combine(dest, dirInfo.Name);

                // needs to end with "/" to be a dir
                //var entry = TarEntry.CreateTarEntry(tarName);
                //entry.Size = 0;
                // _entries.Add((path, entry));

                CreateEntries(path, Path.Combine(dest, dirInfo.Name), false, owner, group);
            }
        }

        public void WriteFiles()
        {
            using (var outStream = File.Create(_outfile))
            using (var gzipStream = new ReproducableGzipStream(outStream, 3))
            using (var tarStream = new TarOutputStream(gzipStream))
            using (var outTmpStream = File.Create(_tmpfile))
            using (var tarStreamTmp = new TarOutputStream(outTmpStream))
            {
                foreach (var i in _entries)
                {
                    tarStream.PutNextEntry(i.Item2);
                    tarStreamTmp.PutNextEntry(i.Item2);
                    using (var fileStream = File.OpenRead(i.Item1))
                    {
                        fileStream.CopyTo(tarStream);
                    }
                    using (var fileStream = File.OpenRead(i.Item1))
                    {
                        fileStream.CopyTo(tarStreamTmp);
                    }

                    tarStream.CloseEntry();
                    tarStreamTmp.CloseEntry();
                }
            }
        }

        public long GetSize()
        {
            var info = new FileInfo(_outfile);
            return info.Length;
        }

        public string CalculateDigest()
        {
            using (var fileStream = File.OpenRead(_outfile))
            {
                return FileHelper.Digest(fileStream);
            }
        }

        public string CalculateDiffId()
        {
            using (var fileStream = File.OpenRead(_tmpfile))
            {
                return FileHelper.Digest(fileStream);
            }
        }

        public static string PrintEntry(TarEntry e)
        {
            return $"{e.ModTime} {Convert.ToString(e.TarHeader.Mode, 8)} {e.UserId}/{e.UserName} {e.GroupId}/{e.GroupName} {e.Size} bytes - {e.Name} {e.File}{(e.IsDirectory ? " - D" : "")}";
        }

        private void FillEntry(TarEntry entry, FileInfo fileInfo, string owner, string group)
        {
            //todo fill inn other entry data, like mode, modified (if !_reproducable), owner, group
            if (_isLinux)
            {
                FillLinuxFileInfo(entry, fileInfo);
            }
            else
            {
                entry.ModTime = fileInfo.LastWriteTime;
                entry.TarHeader.Mode = Convert.ToInt32("755", 8);
            }

            if (_reproducable)
            {
                entry.ModTime = new DateTime(2000, 1, 1);
            }
        }

        // ref: https://github.com/mono/mono/tree/master/mcs/class/Mono.Posix/Mono.Unix
        private void FillLinuxFileInfo(TarEntry entry, FileInfo fileInfo)
        {
            var info = UnixFileSystemInfo.GetFileSystemEntry(fileInfo.FullName);
            entry.ModTime = info.LastWriteTime;
            entry.TarHeader.Mode = (int)info.FileAccessPermissions;
            entry.UserId = (int)info.OwnerUserId;
            entry.UserName = info.OwnerUser.UserName;
            entry.GroupId = (int)info.OwnerGroupId;
            entry.GroupName = info.OwnerGroup.GroupName;
        }
    }
}
