using McMaster.Extensions.CommandLineUtils;
using Nibbler.Command;
using Nibbler.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NibblerBuild;

public class BuildImage
{
    public string DestPath { get; set; } = "/app";

    public async Task<int> Run(BuildProject buildProject, string[] originalArgs)
    {
        var add = new AddArgument { Source = Path.GetRelativePath(Environment.CurrentDirectory, buildProject.PublishPath), Dest = DestPath };

        Console.Write("[BUILD] +nibbler");
        Console.Write($" --from-image {buildProject.FromImage}");
        Console.Write($" --to-image {GetToImage(originalArgs)}");
        Console.Write($" --add {add.Source}:{add.Dest}");
        Console.Write($" --workdir {DestPath}");
        Console.Write($" --entrypoint \"dotnet {buildProject.AssemblyName}\"");
        Console.WriteLine();

        var run = new BuildRun(true);

        run.SetRegistryImageSource(buildProject.FromImage, null, null, false, false, null);
        run.SetRegistoryImageDest(GetToImage(originalArgs), null, null, false, false, null);
        run.Add.Add(add);
        run.WorkingDir = DestPath;
        run.Entrypoint = new[] { "dotnet", buildProject.AssemblyName };
        run.DryRun = true;

        try
        {
            await run.LoadSourceImage();
            //Console.WriteLine(ToJson(run.ImageConfig.config));

            // Nibbler code used in build script:
            // -add "${artifactPath}:/opt/app-root/app" \
            // --addFolder "/opt/app-root:1001::777"

            await run.ExecuteAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[BUILD] error: {ex.Message}");
            return 1;
        }
    }

    private string ToJson<T>(T obj)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        };

        return System.Text.Json.JsonSerializer.Serialize<T>(obj, options);
    }

    private string GetToImage(string[] originalArgs)
    {
        return "example.com/image:latest";
    }
}
