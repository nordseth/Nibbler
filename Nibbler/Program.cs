using System;
using System.Reflection;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Nibbler.Command;
using Nibbler.Utils;

namespace Nibbler
{
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
            app.HelpOption();
            var cmd = new BuildCommand();
            cmd.AddOptions(app);
            app.OnValidate(cmd.Validate);
            app.OnExecuteAsync(cmd.ExecuteAsync);

            return await app.ExecuteAsync(args);
        }

        private static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyInfoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return assemblyInfoVersion?.InformationalVersion ?? "0.0.0";
        }
    }
}
