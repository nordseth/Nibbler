using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler
{
    public interface IImageSource
    {
        string Name { get; }
        Task<Image> LoadImage();
        Task<Stream> GetBlob(string digest);
    }
}
