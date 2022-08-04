using Microsoft.Build.Locator;
using NibblerBuild;
using System.Reflection;

Console.WriteLine($"Nibbler-Build v{GetVersion()}");

// [project] #todo: --to-image <image> [--to-insecure] [--to-skip-tls-verify] [--dry-run] [--write-digest-file]

var sw = System.Diagnostics.Stopwatch.StartNew();

MSBuildLocator.RegisterDefaults();
var buildProject = new BuildProject();
int buildResult = await buildProject.Run(args);
if (buildResult != 0)
{
    Console.WriteLine($"[BUILD] completed in {sw.ElapsedMilliseconds} ms (error: {buildResult})");
    return buildResult;
}

int imageResult = await new BuildImage().Run(buildProject, args);
if (imageResult != 0)
{
    Console.WriteLine($"[BUILD] completed in {sw.ElapsedMilliseconds} ms (error: {imageResult})");
    return imageResult;
}

Console.WriteLine($"[BUILD] completed in {sw.ElapsedMilliseconds} ms");
return 0;

static string GetVersion()
{
    var assembly = Assembly.GetExecutingAssembly();
    var assemblyInfoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
    return assemblyInfoVersion?.InformationalVersion ?? "0.0.0";
}