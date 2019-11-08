using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Nibbler.Test
{
    [TestClass]
    public class BuilderTest
    {
        private readonly IEnumerable<string> Cmd = new[] { "dotnet", "TestTemp.dll" };

        [TestMethod]
        [DataRow("mcr.microsoft.com/dotnet/core/aspnet:3.0", false, false)]
        public async Task Builder_Init(string baseImage, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);

            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0", @"../../../../TestTemp/publish/", "/app", true, false)]
        public async Task Builder_Add(string baseImage, string source, string dest, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);

            builder.Add(source, dest);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0", "test.env=test", true, false)]
        public async Task Builder_Env(string baseImage, string var, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);

            builder.Env(var);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0", true, false)]
        public async Task Builder_Cmd(string baseImage, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);

            builder.Cmd(Cmd);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0", true, false)]
        public async Task Builder_Entrypoint(string baseImage, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);

            builder.Entrypoint(Cmd);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0", "generator", "nibbler", true, false)]
        public async Task Builder_Label(string baseImage, string name, string value, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);

            builder.Label(name, value);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0", "/opt", true, false)]
        public async Task Builder_Workdir(string baseImage, string workdir, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);

            builder.Workdir(workdir);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0", "1001", true, false)]
        public async Task Builder_User(string baseImage, string user, bool insecure, bool useDockerConfig)
        {
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);

            builder.User(user);
        }

        [TestMethod]
        [DataRow("localhost:5000/dotnet/core/aspnet:3.0",
            @"../../../../TestTemp/publish/",
            "TestTemp",
            "localhost:5000/test/nibbler-test:latest",
            true,
            false)]
        public async Task Builder_Add_Config_Push(string baseImage, string artifacts, string dllName, string destImage, bool insecure, bool useDockerConfig)
        {
            string imageFolder = "/app";
            var builder = new Builder(true);
            await builder.Init(baseImage, null, null, useDockerConfig, null, insecure, null);
            builder.Workdir(imageFolder);
            builder.Entrypoint(new[] { "dotnet", $"{dllName}.dll" });
            builder.Label("com.custom.generator", "nibbler-1.1.0");
            builder.Env("secrettestenv=asd");
            builder.Add(artifacts, imageFolder);
            await builder.Push(destImage, false);
        }
    }
}
