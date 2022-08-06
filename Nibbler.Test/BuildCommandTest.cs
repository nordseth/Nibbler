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
        public async Task BuildCommand_Error_Unrecognized_Args()
        {
            int result = await Program.Main(new string[] { "--from-image", "x", "--to-image", "x", "--asd" });
            Assert.AreEqual(2, result);
        }

        [TestMethod]
        public async Task BuildCommand_Error_Invalid_Args()
        {
            int result = await Program.Main(new string[] { "--from-image" });
            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public async Task BuildCommand_Deprecated_Error_Unrecognized_Args()
        {
            int result = await Program.Main(new string[] { "--from-image", "x", "--to-image", "x", "--username", "y" });
            Assert.AreEqual(2, result);
        }

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--insecure",
            "-v",
            "--trace"})]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--insecure",
            "--debug"})]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--insecure",
            "-v",
            "--trace"})]
        public async Task BuilderCommand_Minimal_Args(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Add(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Add_User(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001:1001",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Add_User_Group(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:1001:0:777",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Add_User_Group_Mode(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app:::777",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Add_Mode(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--add", @"../../../../tests/TestData/wwwroot/:/wwwroot",
            "--insecure",
            "-v" })]
        public async Task BuilderCommand_Adds(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "--addFolder", @"/app:1001:0:777",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Add_AddFolder(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--addFolder", @"/app:1001:0:777",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_AddFolder(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--workdir", "/root",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_WorkDir(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--label", "test1=test2",
            "--label", "test4=test4",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Labels(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--user", "1000",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_User(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--env", "ENV_VAR_1=test2",
            "--env", "ENV_VAR_2=test4",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_Env(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--cmd", "dotnet TestData.dll",
            "--entrypoint", "dotnet TestData.dll",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_EntryPoint_Cmd(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--git-labels=../../../../",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_GitLabels(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--to-image", "localhost:5000/test/nibbler-test:unittest",
            "--digest-file",
            "--insecure",
            "-v",
            "--trace" })]
        public async Task BuilderCommand_DigestFile(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "registry.hub.docker.com/library/hello-world:latest",
            "--to-image", "localhost:5000/hello-world:latest",
            "--to-insecure",
            "-v",
            "--trace" })]
        [DataRow(new string[] {
            "--from-image", "registry.hub.docker.com/library/ubuntu:xenial",
            "--to-image", "localhost:5000/ubuntu:xenial",
            "--to-insecure",
            "-v" })]
        [DataRow(new string[] {
            "--from-image", "registry.hub.docker.com/library/ubuntu:bionic",
            "--to-image", "localhost:5000/ubuntu:bionic",
            "--to-insecure",
            "-v",
            "--trace"})]
        public async Task BuilderCommand_Copy_Image(string[] args) => await Run(args);

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--from-insecure",
            "--to-file", "../../../../tests/TestData/test-image",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "-v" },
            "../../../../tests/TestData/test-image")]
        public async Task BuilderCommand_Add_Write_To_File(string[] args, string folder)
        {
            Assert.IsFalse(System.IO.Directory.Exists(folder), $"Folder {folder} already exists");
            await Run(args);
            Assert.IsTrue(System.IO.Directory.Exists(folder));
            System.IO.Directory.Delete(folder, true);
        }

        [TestMethod]
        [DataRow(new string[] {
            "--from-image", "localhost:5000/dotnet/aspnet:6.0",
            "--from-insecure",
            "--to-archive", "../../../../tests/TestData/test-image.tar",
            "--add", @"../../../../tests/TestData/publish/:/app",
            "-v" },
            "../../../../tests/TestData/test-image.tar")]
        public async Task BuilderCommand_Add_Write_To_Archive(string[] args, string file)
        {
            await Run(args);
            Assert.IsTrue(System.IO.File.Exists(file));
        }

        [TestMethod]
        [DataRow(
            new string[] {
                "--from-image", "localhost:5000/dotnet/aspnet:6.0",
                "--from-insecure",
                "--to-file", "../../../../tests/TestData/test-image-1",
                "-v",
                "--trace"
            },
            new string[] {
                "--from-file", "../../../../tests/TestData/test-image-1",
                "--to-file", "../../../../tests/TestData/test-image-2",
                "--add", @"../../../../tests/TestData/publish/:/app",
                "-v",
                "--trace"
            },
            new string[] {
                "--from-file", "../../../../tests/TestData/test-image-2",
                "--to-image", "localhost:5000/test/nibbler-test:unittest",
                "--to-insecure",
                "-v",
                "--trace"
            },
            new string[] {
                "../../../../tests/TestData/test-image-1",
                "../../../../tests/TestData/test-image-2"
            })]
        public async Task BuilderCommand_Add_Via_File(string[] args1, string[] args2, string[] args3, string[] folders)
        {
            foreach (var folder in folders)
            {
                Assert.IsFalse(System.IO.Directory.Exists(folder), $"Folder {folder} already exists");
            }

            await Run(args1);
            await Run(args2);
            await Run(args3);

            foreach (var folder in folders)
            {
                System.IO.Directory.Delete(folder, true);
            }
        }


        public static async Task Run(string[] args)
        {
            int result = await Program.Main(args);
            Assert.AreEqual(0, result);
        }
    }
}
