using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Nibbler.Command;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
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
                "--from-image", "localhost:5000/dotnet/aspnet:5.0",
                "--to-image", "localhost:5001/test/nibbler-test:unittest",
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

        [TestMethod]
        public async Task AuthHandler_Selects_UsernamePassword()
        {
            string username = "username1";
            string password = "password1";
            var expectedAuth = AuthenticationHandler.EncodeBasicCredentials(username, password);

            var handler = new AuthenticationHandler(null, null, false, NullLogger.Instance, null);
            handler.SetCredentials(username, password);

            var fakeHandler = new FakeRequireAuthHandler("Basic", null, expectedAuth);
            handler.InnerHandler = fakeHandler;

            var invoker = new HttpMessageInvoker(handler);
            var response = await invoker.SendAsync(new HttpRequestMessage(), CancellationToken.None);
            response.EnsureSuccessStatusCode();
        }

        [TestMethod]
        public async Task AuthHandler_Selects_DockerConfig()
        {
            var authConfig = new AuthConfig
            {
                auth = "token",
            };

            var dockerConfigMock = new Mock<IDockerConfigCredentials>();
            dockerConfigMock.Setup(c => c.GetCredentials(null)).Returns(authConfig);

            var handler = new AuthenticationHandler(null, dockerConfigMock.Object, false, NullLogger.Instance, null, false, null);

            var fakeHandler = new FakeRequireAuthHandler("Basic", null, authConfig.auth);
            handler.InnerHandler = fakeHandler;

            var invoker = new HttpMessageInvoker(handler);
            var response = await invoker.SendAsync(new HttpRequestMessage(), CancellationToken.None);
            response.EnsureSuccessStatusCode();
        }

        [TestMethod]
        [DataRow("--from-image|docker.io|--to-image|docker.io", false, false)]
        [DataRow("--from-image|docker.io|--to-image|docker.io|--from-username|a|--from-password|b", true, false)]
        [DataRow("--from-image|docker.io|--to-image|docker.io|--to-username|a|--to-password|b", false, true)]
        public async Task Selects_Correct_Credentials(string args, bool fromCreds, bool toCreds)
        {
            var app = new CommandLineApplication();
            var cmd = new BuildCommand();
            cmd.AddOptions(app);
            app.OnValidate(cmd.Validate);

            var splitArgs = args.Split('|');
            var result = await app.ExecuteAsync(splitArgs);

            var regImageSrc = cmd.CreateImageSource() as RegistryImageSource;
            Assert.IsNotNull(regImageSrc);

            Assert.Fail("Selects_Correct_Credentials not implemented because of refactor of HttpClientFactory");

            //var fromAuthHandler = regImageSrc.Registry.Handler as AuthenticationHandler;
            //Assert.IsNotNull(fromAuthHandler);
            //Assert.AreEqual(fromCreds, fromAuthHandler.HasCredentials());

            //var regImageDest = cmd.CreateImageDest(null) as RegistryPusher;
            //Assert.IsNotNull(regImageDest);

            //var toAuthHandler = regImageDest.Registry.Handler as AuthenticationHandler;
            //Assert.IsNotNull(toAuthHandler);
            //Assert.AreEqual(toCreds, toAuthHandler.HasCredentials());
        }

        [TestMethod]
        [DataRow("repository:library/hello-world:pull")]
        [DataRow("repository:dchub-cicd/azure-jenkins-agents-go-test:pull,push")]
        [DataRow("repository:dchub-cicd/azure-jenkins-agents-go-test:")]
        [DataRow(":dchub-cicd/azure-jenkins-agents-go-test:")]
        [DataRow("::")]
        [DataRow("repository::")]
        [DataRow("repository:https://test.com:8080/azure-jenkins-agents-go-test:pull")]
        public void ResourceScope_TryParse_Valid(string scope)
        {
            var resourceScope = ResourceScope.TryParse(scope);
            Assert.IsNotNull(resourceScope);
            Console.Write(resourceScope);
            Console.WriteLine($" ({resourceScope.Actions.Count()})");
        }

        [TestMethod]
        [DataRow("repository:library/hello-world:pull")]
        [DataRow("repository:https://test.com:8080/azure-jenkins-agents-go-test:pull")]
        public void ResourceScope_TryParse_IsPullOnly(string scope)
        {
            var resourceScope = ResourceScope.TryParse(scope);
            Assert.IsNotNull(resourceScope);
            Console.Write(resourceScope);
            Console.WriteLine($" ({resourceScope.Actions.Count()})");
            Assert.IsTrue(resourceScope.IsPullOnly());
        }

        [TestMethod]
        [DataRow("repository:dchub-cicd/azure-jenkins-agents-go-test:pull,push")]
        [DataRow("repository:dchub-cicd/azure-jenkins-agents-go-test:")]
        [DataRow(":dchub-cicd/azure-jenkins-agents-go-test:")]
        [DataRow("::")]
        [DataRow("repository::")]
        public void ResourceScope_TryParse_Not_IsPullOnly(string scope)
        {
            var resourceScope = ResourceScope.TryParse(scope);
            Assert.IsNotNull(resourceScope);
            Console.Write(resourceScope);
            Console.WriteLine($" ({resourceScope.Actions.Count()})");
            Assert.IsFalse(resourceScope.IsPullOnly());
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("repository:dchub-cicd/azure-jenkins-agents-go-test")]
        public void ResourceScope_TryParse_InvalidValid(string scope)
        {
            var resourceScope = ResourceScope.TryParse(scope);
            Assert.IsNull(resourceScope);
        }

        public class FakeRequireAuthHandler : DelegatingHandler
        {
            private string _scheme;
            private string _wwwAuthParameter;
            private string _auth;

            public FakeRequireAuthHandler(string scheme, string wwwAuthParameter, string auth)
            {
                _scheme = scheme;
                _wwwAuthParameter = wwwAuthParameter;
                _auth = auth;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var authHeader = request.Headers.Authorization;
                if (authHeader == null)
                {
                    var unauthorized = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                    unauthorized.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(_scheme, _wwwAuthParameter));
                    return Task.FromResult(unauthorized);
                }

                Assert.AreEqual(_scheme, authHeader.Scheme);
                Assert.AreEqual(_auth, authHeader.Parameter);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }
    }
}
