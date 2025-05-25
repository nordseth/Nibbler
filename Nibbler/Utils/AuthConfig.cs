using System.Text;

namespace Nibbler.Utils;

/// <summary>
/// https://github.com/docker/cli/blob/master/cli/config/types/authconfig.go
/// </summary>
public class AuthConfig
{
    public string username { get; set; }
    public string password { get; set; }
    public string auth { get; set; }

    public string serverAddress { get; set; }

    // IdentityToken is used to authenticate the user and get
    // an access token for the registry.
    public string identityToken { get; set; }

    // RegistryToken is a bearer token to be sent to a registry
    public string registryToken { get; set; }

    public bool EmptyCreds()
    {
        return auth == null && username == null && password == null && identityToken == null;
    }

    public bool HasUsernamePassword()
    {
        return username != null && password != null;
    }

    public string Describe()
    {
        var sb = new StringBuilder();
        sb.Append($"username: {string.IsNullOrEmpty(username)}");
        sb.Append($", password: {string.IsNullOrEmpty(password)}");
        sb.Append($", auth: {string.IsNullOrEmpty(auth)}");
        sb.Append($", serverAddress: {string.IsNullOrEmpty(serverAddress)}");
        sb.Append($", identityToken: {string.IsNullOrEmpty(identityToken)}");
        sb.Append($", registryToken: {string.IsNullOrEmpty(registryToken)}");
        return sb.ToString();
    }
}