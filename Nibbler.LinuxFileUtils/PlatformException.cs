using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Tmds.Linux;

namespace Nibbler.LinuxFileUtils
{
    public class PlatformException : Exception
    {
        public PlatformException(int errno) : base(GetErrorMessage(errno))
        {
            HResult = errno;
        }

        public PlatformException() : this(LibC.errno)
        {
        }

        private unsafe static string GetErrorMessage(int errno)
        {
            int bufferLength = 1024;
            byte* buffer = stackalloc byte[bufferLength];

            int rv = LibC.strerror_r(errno, buffer, bufferLength);

            return rv == 0 ? Marshal.PtrToStringAnsi((IntPtr)buffer) : $"errno {errno}";
        }

        public static void Throw() => throw new PlatformException();
    }
}
