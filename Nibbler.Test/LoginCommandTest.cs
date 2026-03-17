using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nibbler.Test;

[TestClass]
public class LoginCommandTest
{
    private string _testConfigDir;
    private string _testConfigPath;

    [TestInitialize]
    public void Setup()
    {
        // Create a temporary directory for test config files
        _testConfigDir = Path.Combine(Path.GetTempPath(), $"nibbler-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testConfigDir);
        _testConfigPath = Path.Combine(_testConfigDir, "config.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up temporary directory
        if (Directory.Exists(_testConfigDir))
        {
            Directory.Delete(_testConfigDir, true);
        }
    }

    [TestMethod]
    public async Task LoginCommand_Show_Help()
    {
        // Note: The login command doesn't currently support --help due to the command parsing hack
        // in Program.cs. This test documents the current behavior.
        var args = new[] { "login", "--help" };
        int result = await Program.Main(args);
        // Currently returns error code 2 (UnrecognizedCommandParsingException)
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public async Task LoginCommand_Error_Missing_Registry()
    {
        var args = new[] { "login", "--username", "user", "--password", "pass" };
        int result = await Program.Main(args);
        Assert.AreNotEqual(0, result);
    }

    [TestMethod]
    public async Task LoginCommand_Error_Missing_Username()
    {
        var args = new[] { "login", "registry.example.com", "--password", "pass" };
        int result = await Program.Main(args);
        Assert.AreNotEqual(0, result);
    }

    [TestMethod]
    public async Task LoginCommand_Error_Missing_Password()
    {
        var args = new[] { "login", "registry.example.com", "--username", "user" };
        int result = await Program.Main(args);
        Assert.AreNotEqual(0, result);
    }

    [TestMethod]
    public async Task LoginCommand_Success_Creates_New_Config()
    {
        var args = new[] {
            "login",
            "registry.example.com",
            "--username", "testuser",
            "--password", "testpass",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify config file was created
        Assert.IsTrue(File.Exists(_testConfigPath));

        // Verify config content
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonNode.Parse(json);
        Assert.IsNotNull(config);

        var auths = config["auths"];
        Assert.IsNotNull(auths);

        var registryAuth = auths["registry.example.com"];
        Assert.IsNotNull(registryAuth);

        var authToken = registryAuth["auth"]?.ToString();
        Assert.IsNotNull(authToken);

        // Verify the auth token is base64 encoded username:password
        var expectedAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("testuser:testpass"));
        Assert.AreEqual(expectedAuth, authToken);
    }

    [TestMethod]
    public async Task LoginCommand_Success_Updates_Existing_Config()
    {
        // Create initial config with one registry
        var initialConfig = new JsonObject
        {
            ["auths"] = new JsonObject
            {
                ["existing.registry.com"] = new JsonObject
                {
                    ["auth"] = JsonValue.Create("existingtoken")
                }
            }
        };
        File.WriteAllText(_testConfigPath, initialConfig.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // Login to a second registry
        var args = new[] {
            "login",
            "registry.example.com",
            "--username", "newuser",
            "--password", "newpass",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify both registries are in config
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonNode.Parse(json);
        var auths = config["auths"];

        // Old registry should still exist
        Assert.IsNotNull(auths["existing.registry.com"]);
        Assert.AreEqual("existingtoken", auths["existing.registry.com"]["auth"]?.ToString());

        // New registry should be added
        Assert.IsNotNull(auths["registry.example.com"]);
        var expectedAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("newuser:newpass"));
        Assert.AreEqual(expectedAuth, auths["registry.example.com"]["auth"]?.ToString());
    }

    [TestMethod]
    public async Task LoginCommand_Success_Overwrites_Existing_Registry()
    {
        // Create initial config
        var initialConfig = new JsonObject
        {
            ["auths"] = new JsonObject
            {
                ["registry.example.com"] = new JsonObject
                {
                    ["auth"] = JsonValue.Create("oldtoken")
                }
            }
        };
        File.WriteAllText(_testConfigPath, initialConfig.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // Login to same registry with new credentials
        var args = new[] {
            "login",
            "registry.example.com",
            "--username", "newuser",
            "--password", "newpass",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify credentials were updated
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonNode.Parse(json);
        var auths = config["auths"];

        var expectedAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("newuser:newpass"));
        Assert.AreEqual(expectedAuth, auths["registry.example.com"]["auth"]?.ToString());
    }

    [TestMethod]
    public async Task LoginCommand_Success_Multiple_Registries()
    {
        // Login to first registry
        var args1 = new[] {
            "login",
            "registry1.example.com",
            "--username", "user1",
            "--password", "pass1",
            $"--docker-config={_testConfigPath}"
        };
        int result1 = await Program.Main(args1);
        Assert.AreEqual(0, result1);

        // Login to second registry
        var args2 = new[] {
            "login",
            "registry2.example.com",
            "--username", "user2",
            "--password", "pass2",
            $"--docker-config={_testConfigPath}"
        };
        int result2 = await Program.Main(args2);
        Assert.AreEqual(0, result2);

        // Login to third registry
        var args3 = new[] {
            "login",
            "registry3.example.com",
            "--username", "user3",
            "--password", "pass3",
            $"--docker-config={_testConfigPath}"
        };
        int result3 = await Program.Main(args3);
        Assert.AreEqual(0, result3);

        // Verify all three registries are in config
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonNode.Parse(json);
        var auths = config["auths"];

        Assert.IsNotNull(auths["registry1.example.com"]);
        Assert.IsNotNull(auths["registry2.example.com"]);
        Assert.IsNotNull(auths["registry3.example.com"]);

        var expectedAuth1 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user1:pass1"));
        var expectedAuth2 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user2:pass2"));
        var expectedAuth3 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user3:pass3"));

        Assert.AreEqual(expectedAuth1, auths["registry1.example.com"]["auth"]?.ToString());
        Assert.AreEqual(expectedAuth2, auths["registry2.example.com"]["auth"]?.ToString());
        Assert.AreEqual(expectedAuth3, auths["registry3.example.com"]["auth"]?.ToString());
    }

    [TestMethod]
    public async Task LoginCommand_Success_Username_With_Special_Characters()
    {
        var args = new[] {
            "login",
            "registry.example.com",
            "--username", "user@example.com",
            "--password", "pass!@#$%",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify auth token is correctly encoded
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonNode.Parse(json);
        var authToken = config["auths"]["registry.example.com"]["auth"]?.ToString();

        var expectedAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("user@example.com:pass!@#$%"));
        Assert.AreEqual(expectedAuth, authToken);
    }

    [TestMethod]
    public async Task LoginCommand_Error_Empty_Password()
    {
        // Empty password is treated as missing required field
        var args = new[] {
            "login",
            "registry.example.com",
            "--username", "testuser",
            "--password", "",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        // Password is required, empty string doesn't satisfy the requirement
        Assert.AreNotEqual(0, result);
    }

    [TestMethod]
    public async Task LoginCommand_Success_Registry_With_Port()
    {
        var args = new[] {
            "login",
            "registry.example.com:5000",
            "--username", "testuser",
            "--password", "testpass",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify config file has the registry with port
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonNode.Parse(json);
        Assert.IsNotNull(config["auths"]["registry.example.com:5000"]);
    }

    [TestMethod]
    public async Task LoginCommand_Success_Registry_With_Protocol()
    {
        var args = new[] {
            "login",
            "https://registry.example.com",
            "--username", "testuser",
            "--password", "testpass",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify config file has the registry with protocol
        var json = File.ReadAllText(_testConfigPath);
        var config = JsonNode.Parse(json);
        Assert.IsNotNull(config["auths"]["https://registry.example.com"]);
    }

    [TestMethod]
    public async Task LoginCommand_Config_File_Has_Proper_JSON_Format()
    {
        var args = new[] {
            "login",
            "registry.example.com",
            "--username", "testuser",
            "--password", "testpass",
            $"--docker-config={_testConfigPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify JSON is properly formatted (indented)
        var json = File.ReadAllText(_testConfigPath);
        Assert.IsTrue(json.Contains("\n")); // Should be indented, not minified
        Assert.IsTrue(json.Contains("  ")); // Should have indentation

        // Verify it can be parsed as valid JSON
        var config = JsonNode.Parse(json);
        Assert.IsNotNull(config);
    }

    [TestMethod]
    public async Task LoginCommand_Creates_Docker_Config_Directory_If_Not_Exists()
    {
        var nonExistentDir = Path.Combine(_testConfigDir, "subdir", ".docker");
        var configPath = Path.Combine(nonExistentDir, "config.json");

        var args = new[] {
            "login",
            "registry.example.com",
            "--username", "testuser",
            "--password", "testpass",
            $"--docker-config={configPath}"
        };

        int result = await Program.Main(args);
        Assert.AreEqual(0, result);

        // Verify directory and file were created
        Assert.IsTrue(Directory.Exists(nonExistentDir));
        Assert.IsTrue(File.Exists(configPath));
    }
}
