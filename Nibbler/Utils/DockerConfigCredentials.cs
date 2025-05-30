﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Nibbler.Utils
{
    public class DockerConfigCredentials 
    {
        public const string UsernameKey = "username";
        public const string PasswordKey = "password";
        public const string AuthKey = "auth";
        public const string IdentityTokenKey = "identitytoken";

        private const string TokenUserName = "<token>";

        private readonly string _dockerConfigFile;

        protected DockerConfigCredentials()
        {
            // for test purposes
            _dockerConfigFile = null;
        }

        public DockerConfigCredentials(string dockerConfigFile)
        {
            _dockerConfigFile = dockerConfigFile;
        }

        public virtual Dictionary<string, string> GetCredentials(string registry)
        {
            return GetDockerConfigAuth(registry, _dockerConfigFile);
        }

        private static Dictionary<string, string> GetDockerConfigAuth(string registry, string dockerConfigFile)
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
                config = JsonSerializer.Deserialize(dockerConfigJson, JsonContext.Default.DockerConfig);
            }
            catch (Exception ex)
            {
                throw new Exception($"Could not load docker config \"{configPath}\"", ex);
            }

            if (config.auths != null && config.auths.TryGetValue(registry, out var auth))
            {
                if (IsEmptyAuth(auth) && config.credsStore != null)
                {
                    var creds = GetCredentialFromHelper(config.credsStore, registry);
                    if (creds != null)
                    {
                        if (creds.Username.Equals(TokenUserName))
                        {
                            return new()
                            {
                                [DockerConfigCredentials.IdentityTokenKey] = creds.Secret,
                            };
                        }
                        else
                        {
                            return new()
                            {
                                [DockerConfigCredentials.UsernameKey] = creds.Username,
                                [DockerConfigCredentials.PasswordKey] = creds.Secret,
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

        private static bool IsEmptyAuth(Dictionary<string, string> auth)
        {
            return !auth.ContainsKey("auth") && !auth.ContainsKey("username") && !auth.ContainsKey("password") && !auth.ContainsKey("identitytoken");
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
                    return JsonSerializer.Deserialize(resp, JsonContext.Default.CredentialHelperResult);
                }
            }
            catch
            {
                return null;
            }
        }

        public class DockerConfig
        {
            public Dictionary<string, Dictionary<string, string>> auths { get; set; }
            public string credsStore { get; set; }
        }

        public class CredentialHelperResult
        {
            public string ServerURL { get; set; }
            public string Username { get; set; }
            public string Secret { get; set; }
        }
    }
}
