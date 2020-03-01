using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Nibbler.Utils
{
    public class CredentialHelper
    {
        private string _username;
        private string _password;
        private readonly string _dockerConfigFile;

        public CredentialHelper(string dockerConfigFile)
        {
            _dockerConfigFile = dockerConfigFile;
        }

        public void OverrideUsernamePassword(string username, string password)
        {
            _username = username;
            _password = password;
        }

        public string GetEncodedCredentials(string registry)
        {
            if (_username != null && _password != null)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
            }

            var authConfig = GetDockerConfigAuth(registry, _dockerConfigFile);

            if (authConfig?.auth != null)
            {
                return authConfig.auth;
            }

            if (authConfig?.username != null && authConfig?.password != null)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authConfig.username}:{authConfig.password}"));
            }

            return null;
        }

        private static DockerConfigAuth GetDockerConfigAuth(string registry, string dockerConfigFile)
        {
            var configPath = dockerConfigFile;
            if (string.IsNullOrEmpty(configPath))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configPath = $"{home}/.docker/config.json";
            }

            DockerConfig config;
            try
            {
                var dockerConfigJson = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<DockerConfig>(dockerConfigJson);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not load docker config \"{configPath}\"", ex);
            }

            if (config.auths != null && config.auths.TryGetValue(registry, out var auth))
            {
                if (auth.auth == null && auth.username == null && auth.password == null && config.credsStore != null)
                {
                    var creds = GetCredentialFromHelper(config.credsStore, registry);
                    if (creds != null)
                    {
                        return new DockerConfigAuth
                        {
                            username = creds.Username,
                            password = creds.Secret,
                        };
                    }
                }

                return auth;
            }

            throw new Exception($"Could find credentials for {registry} in \"{configPath}\"");
        }

        private static CredentialHelperResult GetCredentialFromHelper(string helper, string key)
        {
            var startInfo = new ProcessStartInfo($"docker-credential-{helper}", "get")
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process.StandardInput.WriteLine(key);
                    process.StandardInput.Close();
                    process.WaitForExit();

                    var resp = process.StandardOutput.ReadToEnd();
                    return JsonConvert.DeserializeObject<CredentialHelperResult>(resp);
                }
            }
            catch
            {
                return null;
            }
        }

        private class DockerConfig
        {
            public Dictionary<string, DockerConfigAuth> auths { get; set; }
            public string credsStore { get; set; }
        }

        private class DockerConfigAuth
        {
            public string username { get; set; }
            public string password { get; set; }
            public string auth { get; set; }
            public string email { get; set; }
        }

        private class CredentialHelperResult
        {
            public string ServerURL { get; set; }
            public string Username { get; set; }
            public string Secret { get; set; }
        }
    }
}
