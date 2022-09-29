using Nibbler.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nibbler.Writer
{
    public interface IImageWriter
    {
        Task WriteImage(Image image, Func<string, Task<Stream>> layerSource);
    }
}