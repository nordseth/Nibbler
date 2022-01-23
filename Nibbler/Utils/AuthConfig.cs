namespace Nibbler.Utils
{
    /// <summary>
    /// https://github.com/docker/cli/blob/6e2838e18645e06f3e4b6c5143898ccc44063e3b/cli/config/types/authconfig.go
    /// </summary>
    public class AuthConfig
    {
        public string username { get; set; }
        public string password { get; set; }
        public string auth { get; set; }
        public string email { get; set; }
        public string identityToken { get; set; }

        public bool EmptyCreds()
        {
            return auth == null && username == null && password == null && identityToken == null;
        }

        public bool HasUsernamePassword()
        {
            return username != null && password != null;
        }
    }
}
