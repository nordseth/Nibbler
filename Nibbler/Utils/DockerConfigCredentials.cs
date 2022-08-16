using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Nibbler.Utils
{
    public interface IDockerConfigCredentials
    {
        AuthConfig GetCredentials(string registry);
    }

    public partial class DockerConfigCredentials : IDockerConfigCredentials
    {
        private const string TokenUserName = "<token>";

        private readonly string _dockerConfigFile;

        public DockerConfigCredentials(string dockerConfigFile)
        {
            _dockerConfigFile = dockerConfigFile;
        }

        public AuthConfig GetCredentials(string registry)
        {
            return GetDockerConfigAuth(registry, _dockerConfigFile);
        }

        private static AuthConfig GetDockerConfigAuth(string registry, string dockerConfigFile)
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
                if (auth.EmptyCreds() && config.credsStore != null)
                {
                    var creds = GetCredentialFromHelper(config.credsStore, registry);
                    if (creds != null)
                    {
                        if (creds.Username.Equals(TokenUserName))
                        {
                            return new AuthConfig
                            {
                                identityToken = creds.Secret,
                            };
                        }
                        else
                        {
                            return new AuthConfig
                            {
                                username = creds.Username,
                                password = creds.Secret,
                            };
                        }
                    }
                }

                return auth;
            }
            else if (config.auths != null && registry == "registry.hub.docker.com")
            {
                return GetDockerConfigAuth("https://index.docker.io/v1/", dockerConfigFile);
            }

            return null;
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
            public Dictionary<string, AuthConfig> auths { get; set; }
            public string credsStore { get; set; }
        }

        private class CredentialHelperResult
        {
            public string ServerURL { get; set; }
            public string Username { get; set; }
            public string Secret { get; set; }
        }
    }
}
