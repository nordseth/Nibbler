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
            var result = await Program.Main(new string[] { "--help" });
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public async Task BuildCommand_Error_No_Args()
        {
            int result = await Program.Main(new string[] { });
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--insecure",
            "-v"})]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--insecure",
            "--debug"})]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--insecure",
            "-v"})]
        public async Task BuilderCommand_Minimal_Args(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Add(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Add_User(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001:1001",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Add_User_Group(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001:0:777",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Add_User_Group_Mode(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:::777",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Add_Mode(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--add", @"../../../../tests/TestData/wwwroot/:/wwwroot",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Adds(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--addFolder", @"/app:1001:0:777",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Add_AddFolder(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--workdir", "/root",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_WorkDir(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--label", "test1=test2",
            "--label", "test4=test4",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Labels(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--user", "1000",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_User(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--env", "ENV_VAR_1=test2",
            "--env", "ENV_VAR_2=test4",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Env(string[] args) => await Run(args);
    
        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--cmd", "dotnet TestData.dll",
            "--entrypoint", "dotnet TestData.dll",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_EntryPoint_Cmd(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--git-labels=../../../../",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_GitLabels(string[] args) => await Run(args);
      
        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--digest-file",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_DigestFile(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "registry.hub.docker.com/library/hello-world:latest",
            "--destination", "localhost:5000/hello-world:latest",
            "--insecure-push",
            "-v" })]
        [DataRow(new string[] {
            "--base-image", "registry.hub.docker.com/library/ubuntu:xenial",
            "--destination", "localhost:5000/ubuntu:xenial",
            "--insecure-push",
            "-v" })]
        [DataRow(new string[] {
            "--base-image", "registry.hub.docker.com/library/ubuntu:bionic",
            "--destination", "localhost:5000/ubuntu:bionic",
            "--insecure-push",
            "-v" })]
        public async Task BuilderCommand_Copy_Image(string[] args) => await Run(args);

        public static async Task Run(string[] args)
        {
            int result = await Program.Main(args);
            Assert.AreEqual(0, result);
        }
    }
}
