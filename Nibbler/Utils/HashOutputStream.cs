using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nibbler.Utils
{
    public class HashOutputStream : Stream
    {
        private readonly Stream _outputStream;
        private readonly HashAlgorithm _hashAlgorithm;

        public HashOutputStream(Stream outputStream, HashAlgorithm hashAlgorithm)
        {
            _outputStream = outputStream;
            _hashAlgorithm = hashAlgorithm;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => _outputStream.CanWrite;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            _outputStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _hashAlgorithm.TransformBlock(buffer, offset, count, null, 0);
            _outputStream.Write(buffer, offset, count);
        }
    }
}
