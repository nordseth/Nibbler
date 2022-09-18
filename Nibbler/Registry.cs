﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler
{
    public class Registry 
    {
        public Uri BaseUri => HttpClient.BaseAddress;
        public HttpClient HttpClient { get; }

        private readonly ILogger _logger;

        public Registry(ILogger logger, HttpClient httpClient)
        {
            HttpClient = httpClient;
            _logger = logger;
        }

        public async Task<HttpContent> GetManifest(string name, string reference)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/{name}/manifests/{reference}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ManifestV2.MimeType));

            var response = await HttpClient.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            return response.Content;
        }

        public async Task<HttpContent> GetImageConfig(string name, string digest)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/{name}/blobs/{digest}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(ImageV1.MimeType));

            var response = await HttpClient.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            return response.Content;
        }

        public async Task<string> StartUpload(string name)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/{name}/blobs/uploads/");
            var response = await HttpClient.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            return response.Headers.Location.ToString();
        }

        public async Task<long?> BlobExists(string name, string digest)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, $"/v2/{name}/blobs/{digest}");
            var response = await HttpClient.SendAsync(request);
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
            var response = await HttpClient.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task MountBlob(string name, string digest, string fromName)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/{name}/blobs/uploads/?mount={digest}&from={fromName}");
            var response = await HttpClient.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
        }

        public async Task UploadBlob(string uploadUrl, string digest, Stream stream, long lenght)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, AddUploadQuery(uploadUrl, $"digest={digest}"));
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentLength = lenght;
            var response = await HttpClient.SendAsync(request);
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
                var response = await HttpClient.SendAsync(request);

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
            var response = await HttpClient.SendAsync(request);
            await EnsureSuccessWithErrorContent(response);
        }

        public async Task UploadManifest(string name, string reference, Stream manifest)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/v2/{name}/manifests/{reference}");
            request.Content = new StreamContent(manifest);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(ManifestV2.MimeType);
            var response = await HttpClient.SendAsync(request);
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
