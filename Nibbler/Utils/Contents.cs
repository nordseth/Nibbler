using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Utils
{
    public interface IContents
    {
        Task<byte[]> ReadBytesAsync();
        Task<string> ReadStringAsync();
    }

    public class HttpContentWrapper : IContents
    {
        private readonly HttpContent _httpContent;

        public HttpContentWrapper(HttpContent httpContent)
        {
            _httpContent = httpContent;
        }

        public Task<byte[]> ReadBytesAsync() => _httpContent.ReadAsByteArrayAsync();
        public Task<string> ReadStringAsync() => _httpContent.ReadAsStringAsync();
    }
}
