using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Command;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Test
{
    [TestClass]
    public class BuildCommandTest
    {
        [TestMethod]
        public async Task BuildCommand_Build_Options_Show_Help()
        {
            var result = await RunProgram(new string[] { "--help" });
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public async Task BuildCommand_Error_No_Args()
        {
            int result = await RunProgram(new string[] { });
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.0", 
            "--destination", "localhost:5000/test/nibbler-test:latest", 
            "--insecure", 
            "--debug", "--dry-run" })]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.0",
            "--destination", "localhost:5000/test/nibbler-test:latest",
            "--add", @"../../../../TestTemp/publish/:/app",
            "--insecure",
            "--debug", "--dry-run" })]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.0",
            "--destination", "localhost:5000/test/nibbler-test:latest",
            "--workdir", "/root",
            "--label", "test1=test2",
            "--label", "test4=test4",
            "--insecure",
            "--debug", "--dry-run" })]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.0",
            "--destination", "localhost:5000/test/nibbler-test:latest",
            "--user", "1000",
            "--env", "ENV_VAR_1=test2",
            "--env", "ENV_VAR_2=test4",
            "--insecure",
            "--debug", "--dry-run" })]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.0",
            "--destination", "localhost:5000/test/nibbler-test:latest",
            "--cmd", "dotnet TestTemp.dll",
            "--entrypoint", "dotnet TestTemp.dll",
            "--insecure",
            "--debug", "--dry-run" })]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.0",
            "--destination", "localhost:5000/test/nibbler-test:latest",
            "--git-labels=../../../../",
            "--insecure",
            "--debug", "--dry-run" })]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.0",
            "--destination", "localhost:5000/test/nibbler-test:latest",
            "--digest-file",
            "--insecure",
            "--debug", "--dry-run" })]
        public async Task BuilderCommand_With_Args(string[] args)
        {
            int result = await RunProgram(args);
            Assert.AreEqual(0, result);
        }

        private static async Task<int> RunProgram(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "nibbler",
                Description = "Do simple changes to OCI images",
            };
            app.HelpOption();

            var cmd = new BuildCommand();
            cmd.AddOptions(app);
            app.OnExecuteAsync(cmd.ExecuteAsync);

            var result = await app.ExecuteAsync(args);
            return result;
        }
    }
}
