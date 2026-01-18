using Nibbler.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nibbler.Utils
{
    [JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(ManifestV2))]
    [JsonSerializable(typeof(ImageV1))]
    [JsonSerializable(typeof(IndexV1))]
    [JsonSerializable(typeof(AuthenticationHandler.TokenResponse))]
    [JsonSerializable(typeof(DockerConfigCredentials.DockerConfig))]
    [JsonSerializable(typeof(DockerConfigCredentials.CredentialHelperResult))]
    [JsonSerializable(typeof(JsonElement))]
    internal partial class JsonContext : JsonSerializerContext
    {
    }
}
