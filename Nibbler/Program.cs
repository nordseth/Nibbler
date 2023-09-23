using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Nibbler.Command;

namespace Nibbler;

public static class Program
{
    public const string ProgramName = "nibbler";

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine($"Nibbler v{GetVersion()}");
        var app = new CommandLineApplication
        {
            Name = ProgramName,
            Description = "Do simple changes to OCI images",
        };
        app.HelpOption(inherited: true);
        var cmd = new BuildCommand();
        cmd.AddOptions(app);
        app.OnValidate(cmd.Validate);
        app.OnExecuteAsync(cmd.ExecuteAsync);
        app.Command(LoginCommand.Name, x => new LoginCommand().Setup(x));

        try
        {
            // hack to avoid validation of root command
            if (args.Length >= 1 && args[0] == LoginCommand.Name)
            {
                var loginCmd = new CommandLineApplication
                {
                    Name = ProgramName,
                    Description = "Do simple changes to OCI images",
                };
                app.HelpOption();
                new LoginCommand().Setup(loginCmd);
                return await loginCmd.ExecuteAsync(args.Skip(1).ToArray());
            }
            else
            {
                return await app.ExecuteAsync(args);
            }
        }
        catch (UnrecognizedCommandParsingException uex)
        {
            Console.Error.WriteLine(uex.Message);
            return 2;
        }
        catch (CommandParsingException pex)
        {
            Console.Error.WriteLine(pex.Message);
            return 3;
        }
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyInfoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return assemblyInfoVersion?.InformationalVersion ?? "0.0.0";
    }
}
