using System;
using System.IO;
using ICSharpCode.SharpZipLib.Checksum;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Nibbler.Utils
{
    /// <summary>
    /// This class is a copy of https://github.com/icsharpcode/SharpZipLib/blob/master/src/ICSharpCode.SharpZipLib/GZip/GzipOutputStream.cs
    /// The only difference is to prevent it from writing a timestamp in the header, so we can have the same file if the content is unchanged.
    /// </summary>
    public class ReproducableGzipStream : DeflaterOutputStream
    {
        private enum OutputState
        {
            Header,
            Footer,
            Finished,
            Closed,
        };

        protected Crc32 crc = new Crc32();

        private OutputState state_ = OutputState.Header;

        public ReproducableGzipStream(Stream baseOutputStream, int level)
            : this(baseOutputStream)
        {
            SetLevel(level);
        }

        public ReproducableGzipStream(Stream baseOutputStream)
            : base(baseOutputStream, new Deflater(Deflater.DEFAULT_COMPRESSION, true), 4096)
        {
        }

        public void SetLevel(int level)
        {
            if (level < Deflater.NO_COMPRESSION || level > Deflater.BEST_COMPRESSION)
                throw new ArgumentOutOfRangeException(nameof(level), "Compression level must be 0-9");

            deflater_.SetLevel(level);
        }

        public int GetLevel()
        {
            return deflater_.GetLevel();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (state_ == OutputState.Header)
            {
                WriteHeader();
            }

            if (state_ != OutputState.Footer)
            {
                throw new InvalidOperationException("Write not permitted in current state");
            }

            crc.Update(new ArraySegment<byte>(buffer, offset, count));
            base.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                Finish();
            }
            finally
            {
                if (state_ != OutputState.Closed)
                {
                    state_ = OutputState.Closed;
                    if (IsStreamOwner)
                    {
                        baseOutputStream_.Dispose();
                    }
                }
            }
        }

        public override void Finish()
        {
            // If no data has been written a header should be added.
            if (state_ == OutputState.Header)
            {
                WriteHeader();
            }

            if (state_ == OutputState.Footer)
            {
                state_ = OutputState.Finished;
                base.Finish();

                var totalin = (uint)(deflater_.TotalIn & 0xffffffff);
                var crcval = (uint)(crc.Value & 0xffffffff);

                byte[] gzipFooter;

                unchecked
                {
                    gzipFooter = new byte[] {
                    (byte) crcval, (byte) (crcval >> 8),
                    (byte) (crcval >> 16), (byte) (crcval >> 24),

                    (byte) totalin, (byte) (totalin >> 8),
                    (byte) (totalin >> 16), (byte) (totalin >> 24)
                };
                }

                baseOutputStream_.Write(gzipFooter, 0, gzipFooter.Length);
            }
        }

        private void WriteHeader()
        {
            if (state_ == OutputState.Header)
            {
                state_ = OutputState.Footer;

                var mod_time = (int)((new DateTime(2010, 1, 1).Ticks - new DateTime(1970, 1, 1).Ticks) / 10000000L);  // Ticks give back 100ns intervals
                byte[] gzipHeader = {
					// The two magic bytes
					 GZipConstants.GZIP_MAGIC >> 8,  GZipConstants.GZIP_MAGIC & 0xff,

					// The compression type
					 Deflater.DEFLATED,

					// The flags (not set)
					0,

					// The modification time
					(byte) mod_time, (byte) (mod_time >> 8),
                    (byte) (mod_time >> 16), (byte) (mod_time >> 24),

					// The extra flags
					0,

					// The OS type (unknown)
					 255
                };
                baseOutputStream_.Write(gzipHeader, 0, gzipHeader.Length);
            }
        }
    }
}