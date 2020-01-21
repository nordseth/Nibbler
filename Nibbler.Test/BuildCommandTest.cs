﻿using McMaster.Extensions.CommandLineUtils;
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
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--insecure",
            "--debug"})]
        public async Task BuilderCommand_Minimal_Args(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Add(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Add_User(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001:1001",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Add_User_Group(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001:0:777",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Add_User_Group_Mode(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:::777",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Add_Mode(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--add", @"../../../../tests/TestData/wwwroot/:/wwwroot",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Adds(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--addFolder", @"/app:1001:0:777",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Add_AddFolder(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--workdir", "/root",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_WorkDir(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--label", "test1=test2",
            "--label", "test4=test4",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Labels(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--user", "1000",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_User(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--env", "ENV_VAR_1=test2",
            "--env", "ENV_VAR_2=test4",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_Env(string[] args) => await Run(args);
    
        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--cmd", "dotnet TestData.dll",
            "--entrypoint", "dotnet TestData.dll",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_EntryPoint_Cmd(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--git-labels=../../../../",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_GitLabels(string[] args) => await Run(args);
      
        [TestMethod]
        [DataRow(new string[] {
            "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
            "--destination", "localhost:5000/test/nibbler-test:unittest",
            "--digest-file",
            "--insecure",
            "--debug" })]
        public async Task BuilderCommand_DigestFile(string[] args) => await Run(args);

        private static async Task Run(string[] args)
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
