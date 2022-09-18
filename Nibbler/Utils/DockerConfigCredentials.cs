using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Nibbler.Utils
{
    public interface IDockerConfigCredentials
    {
        string GetEncodedCredentials(string registry);
    }

    public class DockerConfigCredentials : IDockerConfigCredentials
    {
        private readonly string _dockerConfigFile;

        public DockerConfigCredentials(string dockerConfigFile)
        {
            _dockerConfigFile = dockerConfigFile;
        }

        public string GetEncodedCredentials(string registry)
        {
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
                config = JsonSerializer.Deserialize<DockerConfig>(dockerConfigJson);
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
                    return JsonSerializer.Deserialize<CredentialHelperResult>(resp);
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
