using Nibbler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Nibbler.Utils
{
    [JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(ManifestV2))]
    [JsonSerializable(typeof(ImageV1))]
    [JsonSerializable(typeof(AuthenticationHandler.TokenResponse))]
    [JsonSerializable(typeof(DockerConfigCredentials.DockerConfig))]
    [JsonSerializable(typeof(DockerConfigCredentials.CredentialHelperResult))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
