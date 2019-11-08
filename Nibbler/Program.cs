using System;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Nibbler.Utils;

namespace Nibbler
{
    static class Program
    {
        static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "nibbler",
                Description = "Do simple changes to OCI images",
            };
            var debug = app.Option("--debug", "Output debug log", CommandOptionType.NoValue, true);
            app.HelpOption(inherited: true);
            app.OnExecute(() =>
            {
                Console.WriteLine("Specify a command");
                app.ShowHelp();
                return 1;
            });

            AddInit(app, debug);
            AddAdd(app, debug);
            AddEnv(app, debug);
            AddLabel(app, debug);
            AddLabels(app, debug);
            AddEntrypoint(app, debug);
            AddCmd(app, debug);
            AddUser(app, debug);
            AddWorkdir(app, debug);
            AddPush(app, debug);

            return await app.ExecuteAsync(args);
        }

        private static void AddAdd(CommandLineApplication app, CommandOption debug)
        {
            app.Command("add", c =>
            {
                c.Description = "Add a folder to the image";
                var source = c.Argument("source", "Source folder").IsRequired();
                var dest = c.Argument("dest", "Destination folder in image").IsRequired();

                Execute(c, debug, b => b.Add(source.Value, dest.Value));
            });
        }

        private static void AddPush(CommandLineApplication app, CommandOption debug)
        {
            app.Command("push", c =>
            {
                c.Description = "Push the modified image to the destination";
                var dest = c.Argument("dest", "Image desination (must be the same registry as source image)").IsRequired();
                var dryrun = c.Option("--dryrun", "Don't push, only show what would have been pushed", CommandOptionType.SingleValue);

                ExecuteAsync(c, debug, b => b.Push(dest.Value, dryrun.HasValue()));
            });
        }

        private static void AddWorkdir(CommandLineApplication app, CommandOption debug)
        {
            app.Command("workdir", c =>
            {
                c.Description = "Set the working directory in the image";
                var workdir = c.Argument("workdir", "Working directory in image").IsRequired();

                Execute(c, debug, b => b.Workdir(workdir.Value));
            });
        }

        private static void AddUser(CommandLineApplication app, CommandOption debug)
        {
            app.Command("user", c =>
            {
                c.Description = "Set the user in the image";
                var user = c.Argument("user", "User in image").IsRequired();

                Execute(c, debug, b => b.User(user.Value));
            });
        }

        private static void AddCmd(CommandLineApplication app, CommandOption debug)
        {
            app.Command("cmd", c =>
            {
                c.Description = "Set the cmd on the image";
                var cmd = c.Argument("cmd", "Command args", true).IsRequired();

                Execute(c, debug, b => b.Cmd(cmd.Values));
            });
        }

        private static void AddEntrypoint(CommandLineApplication app, CommandOption debug)
        {
            app.Command("entrypoint", c =>
            {
                c.Description = "Set the entrypoint on the image";
                var cmd = c.Argument("cmd", "Entrypoint command args", true).IsRequired();

                Execute(c, debug, b => b.Entrypoint(cmd.Values));
            });
        }

        private static void AddLabel(CommandLineApplication app, CommandOption debug)
        {
            app.Command("labels", labelsCmd =>
            {
                labelsCmd.Description = "Add predefined labels";
                app.OnExecute(() =>
                {
                    Console.WriteLine("Specify a label type");
                    app.ShowHelp();
                    return 1;
                });

                labelsCmd.Command("git", gitCmd =>
                {
                    gitCmd.Description = "add common git labels to image";
                    var path = gitCmd.Argument("gitPath", "path to git repo");

                    Execute(gitCmd, debug, b => GitLabels.AddLabels(path.Value, b, debug.HasValue()));
                });
            });
        }

        private static void AddLabels(CommandLineApplication app, CommandOption debug)
        {
            app.Command("label", c =>
            {
                c.Description = "Set a label on the image";
                var name = c.Argument("name", "Label name").IsRequired();
                var value = c.Argument("value", "Label value").IsRequired();

                Execute(c, debug, b => b.Label(name.Value, value.Value));
            });
        }

        private static void AddEnv(CommandLineApplication app, CommandOption debug)
        {
            app.Command("env", c =>
            {
                c.Description = "Add a environment variable to the image";
                var var = c.Argument("var", "Environment variable to set. example: name=value").IsRequired();

                Execute(c, debug, b => b.Env(var.Value));
            });
        }

        private static void AddInit(CommandLineApplication app, CommandOption debug)
        {
            app.Command("init", c =>
            {
                c.Description = "Init builder with a base image";
                var baseImage = c.Argument("baseImage", "Base image to modify").IsRequired();
                var username = c.Option("--username", "Registry username", CommandOptionType.SingleValue);
                var password = c.Option("--password", "Registry password", CommandOptionType.SingleValue);
                var dockerConfig = c.Option("--dockerConfig", "Docker config file to use for authentication", CommandOptionType.SingleOrNoValue);
                var insecure = c.Option("--insecure", "Insecure registry (http)", CommandOptionType.NoValue);
                var skipTlsVerify = c.Option("--skip-tls-verify", "Skip verifying TLS server cert", CommandOptionType.NoValue);

                ExecuteAsync(
                    c,
                    debug,
                    builder => builder.Init(baseImage.Value, username.Value(), password.Value(), dockerConfig.HasValue(), dockerConfig.Value(), insecure.HasValue(), skipTlsVerify.HasValue()));
            });
        }

        private static void ExecuteAsync(CommandLineApplication c, CommandOption debugOption, Func<Builder, Task> action)
        {
            c.OnExecuteAsync(async cancellationToken =>
            {
                bool debug = debugOption.HasValue();
                try
                {
                    var builder = new Builder(debug);
                    await action(builder);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    if (debug)
                    {
                        Console.Error.WriteLine(ex);
                    }

                    return 1;
                }
            });
        }

        private static void Execute(CommandLineApplication c, CommandOption debugOption, Action<Builder> action)
        {
            c.OnExecute(() =>
            {
                bool debug = debugOption.HasValue();
                try
                {
                    var builder = new Builder(debug);
                    action(builder);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    if (debug)
                    {
                        Console.Error.WriteLine(ex);
                    }

                    return 1;
                }
            });
        }
    }
}
