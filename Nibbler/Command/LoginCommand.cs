using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Nibbler.Command;

public class LoginCommand
{
    public const string Name = "login";

    public CommandArgument Registry { get; private set; }
    public CommandOption Username { get; private set; }
    public CommandOption Password { get; private set; }
    public CommandOption DockerConfig { get; private set; }

    public void Setup(CommandLineApplication app)
    {
        Registry = app.Argument("Registry", "Logon server for registry").IsRequired();
        Username = app.Option("-u|--username", "Registry username", CommandOptionType.SingleValue).IsRequired();
        Password = app.Option("-p|--password", "Registry password", CommandOptionType.SingleValue).IsRequired();
        DockerConfig = app.Option("--docker-config", "Specify docker config file for authentication with registry. (default: ~/.docker/config.json)", CommandOptionType.SingleOrNoValue);

        app.OnExecuteAsync(Execute);
    }

    private Task<int> Execute(CancellationToken token)
    {
        try
        {
            var configPath = DockerConfig.Value();
            if (string.IsNullOrEmpty(configPath))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configPath = $"{home}/.docker-test/config.json";
            }

            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username.Value()}:{Password.Value()}"));

            JsonNode config;
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                config = JsonNode.Parse(json);
            }
            else
            {
                if (!Directory.Exists(Path.GetDirectoryName(configPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                }

                config = new JsonObject();
            }

            var auths = config["auths"];
            if (auths == null)
            {
                auths = new JsonObject();
                config["auths"] = auths;
            }

            var login = new JsonObject();
            login["auth"] = JsonValue.Create(authToken);
            auths[Registry.Value] = login;

            File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return Task.FromResult(1);
        }
    }
}
