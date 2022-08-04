using Microsoft.Build.Evaluation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NibblerBuild;

public class BuildProject
{
    private const string BuildLoggerName = "[BUILD]";

    public string BuildConfiguration { get; set; } = "Debug";
    public string RuntimeIdentifier { get; set; } = "linux-x64";
    public string PublishDir { get; set; } = ".nibbler-build";
    public bool SelfContained { get; set; } = false;
    public string ShortParamString => $"-p:Configuration={BuildConfiguration} -p:RuntimeIdentifier={RuntimeIdentifier}";

    public string? ProjectFile { get; private set; }
    public string? Framework { get; private set; }
    public string? AssemblyName { get; private set; }
    public string? FromImage { get; private set; }

    public string PublishPath { get; private set; } = ".";

    public async Task<int> Run(string[] args)
    {
        ProjectFile = FindProjectFile(args.FirstOrDefault() ?? "C:\\code\\_repos\\KubeTest\\KubeTest" /*"."*/);
        if (ProjectFile == null)
        {
            Console.Error.WriteLine($"{BuildLoggerName} Cannot find project file.");
            return -1;
        }

        Project project;
        try
        {
            project = LoadProject();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{BuildLoggerName} Error loading project: {ex.GetType().Name}: {ex.Message}");
            return -1;
        }

        Console.WriteLine($"{BuildLoggerName} Loaded: {ProjectFile}, AssemblyName: {AssemblyName}, TargetFramework: {Framework}, BaseImage: {FromImage}");
        if (Framework is null || AssemblyName is null || FromImage is null)
        {
            if (Framework is null)
            {
                Console.Error.WriteLine($"{BuildLoggerName} Cannot determine project target framework version.");
            }

            if (AssemblyName is null)
            {
                Console.Error.WriteLine($"{BuildLoggerName} Cannot determine application assembly name.");
            }

            if (FromImage is null)
            {
                Console.Error.WriteLine($"{BuildLoggerName} Cannot determine from image.");
            }

            return -1;
        }

        Console.WriteLine($"{BuildLoggerName} +msbuild -t:restore {ShortParamString}");
        var restoreResult = project.Build("restore", new[] { new SimpleLogger(BuildLoggerName, Console.Out) });

        if (!restoreResult)
        {
            Console.Error.WriteLine($"{BuildLoggerName} Restore failed.");
            return -3;
        }

        PublishPath = Path.Combine(project.DirectoryPath, ".nibbler-build");
        if (Directory.Exists(PublishPath))
        {
            Console.WriteLine($"{BuildLoggerName} +rm -r .nibbler-build");
            Directory.Delete(PublishPath, true);
        }

        Console.WriteLine($"{BuildLoggerName} +msbuild -t:publish {ShortParamString}");
        var publishResult = project.Build("publish", new[] { new SimpleLogger(BuildLoggerName, Console.Out) });

        if (!publishResult)
        {
            Console.Error.WriteLine($"{BuildLoggerName} Publish failed.");
            return -4;
        }

        return 0;
    }

    private Project LoadProject()
    {
        Project project = new(ProjectFile);
        project.SetGlobalProperty("Configuration", BuildConfiguration);
        project.SetGlobalProperty("PublishDir", PublishDir);
        project.SetGlobalProperty("RuntimeIdentifier", RuntimeIdentifier);
        project.SetGlobalProperty("SelfContained", SelfContained.ToString());

        Framework = GetProperty(project, "TargetFramework");
        AssemblyName = GetProperty(project, "AssemblyName");
        FromImage = GetProperty(project, "NibblerFromImage") ?? GetDefaultImage(Framework);
        return project;
    }

    private string GetDefaultImage(string? framework)
    {
        return "mcr.microsoft.com/dotnet/aspnet:6.0";
    }

    private string? GetProperty(Project project, string name)
    {
        return project.AllEvaluatedProperties.FirstOrDefault(prop => prop.Name == name)?.EvaluatedValue;
    }

    private string? FindProjectFile(string pathArg)
    {
        if (File.Exists(pathArg))
        {
            return Path.GetFullPath(pathArg);
        }
        else if (Directory.Exists(pathArg))
        {
            var projFiles = Directory.GetFiles(pathArg, "*.??proj");
            if (projFiles.Any())
            {
                return projFiles[0];
            }
        }

        return null;
    }
}
