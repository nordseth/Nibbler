using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler
{
    public class Registry
    {
        public Uri BaseUri { get; }

        private readonly ILogger _logger;
        private readonly HttpClient _client;

        public Registry(Uri baseUri, ILogger logger, bool skipTlsVerify = false)
        {
            BaseUri = baseUri;
            _logger = logger;
            var httpClientHandler = new HttpClientHandler();
            if (skipTlsVerify)
            {
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            }

            _client = new HttpClient(httpClientHandler);
            _client.BaseAddress = BaseUri;
        }

        public void UseBasicAuthentication(string username, string password)
        {
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
        }

        public void UseAuthorization(string header)
        {
            _client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(header);
        }

        public async Task<string> GetManifestFile(string name, string reference)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/{name}/manifests/{reference}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestV2.MimeType));

            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            var content = await response.Content.ReadAsStringAsync();

            return content;
        }

        public async Task<ManifestV2> GetManifest(string name, string reference)
        {
            return JsonConvert.DeserializeObject<ManifestV2>(await GetManifestFile(name, reference));
        }

        public async Task<(string content, string digest)> GetImageFile(string name, string digest)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/{name}/blobs/{digest}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ImageV1.MimeType));

            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            var byteContent = await response.Content.ReadAsByteArrayAsync();
            var calculatedDigest = FileHelper.Digest(byteContent);
            var content = Encoding.UTF8.GetString(byteContent);

            return (content, calculatedDigest);
        }

        public async Task<ImageV1> GetImage(string name, string digest)
        {
            var (json, _) = await GetImageFile(name, digest);
            return JsonConvert.DeserializeObject<ImageV1>(json);
        }

        public async Task<string> StartUpload(string name)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/{name}/blobs/uploads/");
            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            return response.Headers.Location.ToString();
        }

        public async Task<long?> BlobExists(string name, string digest)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, $"/v2/{name}/blobs/{digest}");
            var response = await _client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return response.Content.Headers.ContentLength.Value;
            }
            else
            {
                return null;
            }
        }

        public async Task<Stream> DownloadBlob(string name, string digest)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/{name}/blobs/{digest}");
            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task MountBlob(string name, string digest, string fromName)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/{name}/blobs/uploads/?mount={digest}&from={fromName}");
            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            if (response.StatusCode != System.Net.HttpStatusCode.Created)
            {
                throw new Exception($"Expected 201 Created, got {response.StatusCode}");
            }
        }

        public async Task UploadBlob(string uploadUrl, string digest, Stream stream, long lenght)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, AddUploadQuery(uploadUrl, $"digest={digest}"));
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentLength = lenght;
            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
        }

        public async Task UploadBlobChuncks(string uploadUrl, string digest, Stream stream, int chunckSize)
        {
            var buffer = new byte[chunckSize];
            int currentIndex = 0;
            int read;
            while ((read = stream.Read(buffer, 0, chunckSize)) > 0)
            {
                HttpRequestMessage request;
                // send put on last request
                if (read >= chunckSize)
                {
                    request = new HttpRequestMessage(new HttpMethod("Patch"), uploadUrl);
                }
                else
                {
                    request = new HttpRequestMessage(HttpMethod.Put, AddUploadQuery(uploadUrl, $"digest={digest}"));
                }

                request.Content = new ByteArrayContent(buffer);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Content.Headers.ContentLength = read;
                request.Content.Headers.TryAddWithoutValidation("Content-Range", $"{currentIndex}-{currentIndex + read - 1}");
                var response = await _client.SendAsync(request);

                _logger.LogDebug($"uploaded {currentIndex}-{currentIndex + read - 1}{(request.Method == HttpMethod.Put ? " (last)" : "")} - {(int)response.StatusCode}");

                await EnsureSuccessWithErrorContent(response);
                currentIndex += read;

                if (response.Headers.Location != null)
                {
                    uploadUrl = response.Headers.Location.ToString();
                }
            }
        }

        public async Task UploadManifest(string name, string reference, ManifestV2 manifest)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/v2/{name}/manifests/{reference}");
            request.Content = new StringContent(FileHelper.JsonSerialize(manifest));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(ManifestV2.MimeType);
            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
        }

        public async Task UploadManifest(string name, string reference, Stream manifest)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/v2/{name}/manifests/{reference}");
            request.Content = new StreamContent(manifest);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(ManifestV2.MimeType);
            var response = await _client.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
        }

        private async Task EnsureSuccessWithErrorContent(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Response not success: {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{content}");
            }
        }

        private string AddUploadQuery(string uploadUrl, string query)
        {
            if (uploadUrl.Contains("?"))
            {
                return $"{uploadUrl}&{query}";
            }
            else
            {
                return $"{uploadUrl}?{query}";
            }
        }
    }
}
