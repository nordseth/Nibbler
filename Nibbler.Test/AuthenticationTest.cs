using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Nibbler.Test
{
    [TestClass]
    public class AuthenticationTest
    {
        [TestMethod]
        public async Task BuildCommand_RegistryBaseAndDestValidate()
        {
            var args = new[] {
                "--base-image", "localhost:5000/dotnet/core/aspnet:3.1",
                "--destination", "localhost:5001/test/nibbler-test:unittest",
                "--insecure",
                "-v",
                "--dry-run"
            };

            int result = await Program.Main(args);
            Assert.AreNotEqual(0, result);
        }

        [TestMethod]
        [DataRow("realm=\"Sonatype Nexus Repository Manager\"")]
        [DataRow("realm=\"https://gitlab.com/jwt/auth\",service=\"container_registry\",scope=\"repository:nordseth/ci-tryout:pull\"")]
        [DataRow("realm=\"https://gitlab.com/jwt/auth\",service=\"container_registry\",scope=\"repository:nordseth/ci-tryout:pull,push\",error=\"insufficient_scope\"")]
        public void AuthParamParser_Run(string value)
        {
            var @params = AuthParamParser.Parse(value);
            foreach (var p in @params)
            {
                Console.WriteLine($"{p.Key} = \"{p.Value}\"");
            }
        }
    }
}
