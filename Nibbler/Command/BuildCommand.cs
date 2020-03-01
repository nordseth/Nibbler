using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Abstractions;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler.Command
{
    public class BuildCommand
    {
        private readonly ILogger _logger;
        private const string ManifestDigestFileName = "digest";
        private string _tempFolderPath;

        public BuildCommand()
        {
            _logger = new Logger("BUILD", false);
        }

        public CommandOption BaseImage { get; private set; }

        public CommandOption Destination { get; private set; }

        public CommandOption Add { get; private set; }
        public CommandOption AddFolder { get; private set; }

        public CommandOption Label { get; private set; }
        public CommandOption Env { get; private set; }
        public CommandOption GitLabels { get; private set; }
        public CommandOption WorkDir { get; private set; }
        public CommandOption User { get; private set; }
        public CommandOption Cmd { get; private set; }
        public CommandOption Entrypoint { get; private set; }

        public CommandOption Verbose { get; private set; }
        public CommandOption DryRun { get; private set; }

        public CommandOption DockerConfig { get; private set; }

        public CommandOption Username { get; private set; }
        public CommandOption Password { get; private set; }

        public CommandOption Insecure { get; private set; }
        public CommandOption SkipTlsVerify { get; private set; }
        public CommandOption InsecurePush { get; private set; }
        public CommandOption SkipTlsVerifyPush { get; private set; }
        public CommandOption InsecurePull { get; private set; }
        public CommandOption SkipTlsVerifyPull { get; private set; }

        public CommandOption TempFolder { get; private set; }
        public CommandOption DigestFile { get; private set; }

        public void AddOptions(CommandLineApplication app)
        {
            // init and push replacements, required arguments
            BaseImage = app.Option("--base-image", "Set base image (required)", CommandOptionType.SingleValue).IsRequired();
            Destination = app.Option("--destination", "Destination to push the modified image  (required)", CommandOptionType.SingleValue).IsRequired();

            // "commands"
            Add = app.Option("--add", "Add contents of a folder to the image 'sourceFolder:destFolder[:ownerId:groupId:permissions]'", CommandOptionType.MultipleValue);
            AddFolder = app.Option("--addFolder", "Add a folder to the image 'destFolder[:ownerId:groupId:permissions]'", CommandOptionType.MultipleValue);
            Label = app.Option("--label", "Add label to the image 'name=value'", CommandOptionType.MultipleValue);
            Env = app.Option("--env", "Add a environment variable to the image 'name=value'", CommandOptionType.MultipleValue);
            GitLabels = app.Option("--git-labels", "Add common git labels to image, optionally define the path to the git repo.", CommandOptionType.SingleOrNoValue);
            WorkDir = app.Option("--workdir", "Set the working directory in the image", CommandOptionType.SingleValue);
            User = app.Option("--user", "Set the user in the image", CommandOptionType.SingleValue);
            Cmd = app.Option("--cmd", "Set the image cmd", CommandOptionType.SingleValue);
            Entrypoint = app.Option("--entrypoint", "Set the image entrypoint", CommandOptionType.SingleValue);

            // options:
            Verbose = app.Option("-v|--verbose|--debug", "Verbose output", CommandOptionType.NoValue);
            DryRun = app.Option("--dry-run", "Does not push, only shows what would happen (use with -v)", CommandOptionType.NoValue);

            DockerConfig = app.Option("--docker-config", "Specify docker config file for authentication with registry. (default: ~/.docker/config.json)", CommandOptionType.SingleOrNoValue);

            Username = app.Option("--username", "Registry username (deprecated, use docker-config)", CommandOptionType.SingleValue);
            Password = app.Option("--password", "Registry password (deprecated, use docker-config)", CommandOptionType.SingleValue);
            Insecure = app.Option("--insecure", "Insecure registry (http). Only use if base image and destination is the same registry.", CommandOptionType.NoValue);
            SkipTlsVerify = app.Option("--skip-tls-verify", "Skip verifying registry TLS certificate. Only use if base image and destination is the same registry.", CommandOptionType.NoValue);

            InsecurePull = app.Option("--insecure-pull", "Insecure base registry (http)", CommandOptionType.NoValue);
            SkipTlsVerifyPull = app.Option("--skip-tls-verify-pull", "Skip verifying base registry TLS certificate", CommandOptionType.NoValue);

            InsecurePush = app.Option("--insecure-push", "Insecure destination registry (http)", CommandOptionType.NoValue);
            SkipTlsVerifyPush = app.Option("--skip-tls-verify-push", "Skip verifying destination registry TLS certificate", CommandOptionType.NoValue);

            TempFolder = app.Option("--temp-folder", "Set temp folder (default: ./.nibbler)", CommandOptionType.SingleValue);
            DigestFile = app.Option("--digest-file", "Output image digest to file, optionally specify file", CommandOptionType.SingleOrNoValue);
        }

        public ValidationResult Validate(ValidationContext context)
        {
            if (Username.HasValue() || Password.HasValue() || Insecure.HasValue() || SkipTlsVerify.HasValue())
            {
                var srcReg = ImageHelper.GetRegistryName(BaseImage.Value());
                var destReg = ImageHelper.GetRegistryName(Destination.Value());

                if (srcReg != destReg)
                {
                    var fields = new List<string>();
                    if (Username.HasValue())
                    {
                        fields.Add(Username.LongName);
                    }

                    if (Password.HasValue())
                    {
                        fields.Add(Password.LongName);
                    }

                    if (Insecure.HasValue())
                    {
                        fields.Add(Insecure.LongName);
                    }

                    if (SkipTlsVerify.HasValue())
                    {
                        fields.Add(SkipTlsVerify.LongName);
                    }

                    return new ValidationResult($"{string.Join(", ", fields)} can only be set if baseImage registry is the same as destination", fields);
                }
            }

            return ValidationResult.Success;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.SetEnable(Verbose.HasValue());

            try
            {
                var (baseRegistry, destRegistry) = CreateRegistries();
                var (manifest, image) = await LoadBaseImage(baseRegistry);
                UpdateImageConfig(image);
                UpdateConfigInManifest(manifest, image);

                var layersAdded = new List<BuilderLayer>();
                if (Add.HasValue())
                {
                    var layer = CreateLayer(Add.Values, AddFolder.Values, $"layer{layersAdded.Count():00}");
                    layersAdded.Add(layer);
                    AddLayerToConfigAndManifest(layer, manifest, image);
                }

                var pusher = new Pusher(BaseImage.Value(), Destination.Value(), destRegistry, layersAdded, CreateLogger("PUSHR"))
                {
                    FakePullAndRetryMount = true,
                };

                bool configExists = await pusher.CheckConfigExists(manifest);
                var missingLayers = await pusher.FindMissingLayers(manifest, !DryRun.HasValue() && destRegistry == baseRegistry);

                if (!DryRun.HasValue())
                {
                    if (!configExists)
                    {
                        await pusher.PushConfig(manifest.config, () => GetJsonStream(image));
                    }

                    await pusher.CopyLayers(baseRegistry, ImageHelper.GetImageName(BaseImage.Value()), missingLayers);
                    await pusher.PushLayers(f => File.OpenRead(Path.Combine(_tempFolderPath, f)));
                    await pusher.PushManifest(() => GetJsonStream(manifest));
                }

                string manifestDigest;
                using (var manifestStream = GetJsonStream(manifest))
                {
                    manifestDigest = FileHelper.Digest(manifestStream);
                }

                _logger.LogDebug($"Image digest: {manifestDigest}");

                if (DigestFile.HasValue())
                {
                    string digestFilepath;
                    if (!string.IsNullOrEmpty(DigestFile.Value()))
                    {
                        digestFilepath = DigestFile.Value();
                    }
                    else
                    {
                        EnsureTempFolder();
                        digestFilepath = Path.Combine(_tempFolderPath, ManifestDigestFileName);
                    }

                    File.WriteAllText(digestFilepath, manifestDigest);
                }

                _logger.LogDebug($"completed in {sw.ElapsedMilliseconds} ms");
                Console.WriteLine(manifestDigest);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                _logger.LogDebug(ex, "exception");
                _logger.LogDebug($"completed in {sw.ElapsedMilliseconds} ms");

                return 1;
            }
        }

        private ILogger CreateLogger(string name)
        {
            return new Logger(name, Verbose.HasValue());
        }

        private (Registry src, Registry dest) CreateRegistries()
        {
            var registryLogger = CreateLogger("REGRY");
            var credentialHelper = new CredentialHelper(DockerConfig.Value());

            var baseUri = ImageHelper.GetRegistryBaseUrl(BaseImage.Value(), Insecure.HasValue() || InsecurePull.HasValue());
            var destUri = ImageHelper.GetRegistryBaseUrl(Destination.Value(), Insecure.HasValue() || InsecurePush.HasValue());

            if (baseUri == destUri)
            {
                var authHandler = new AuthenticationHandler(
                    ImageHelper.GetRegistryName(BaseImage.Value()),
                    credentialHelper,
                    registryLogger);

                if (Username.HasValue() && Password.HasValue())
                {
                    credentialHelper.OverrideUsernamePassword(Username.Value(), Password.Value());
                }

                bool skipTlsVerify = SkipTlsVerify.HasValue() || SkipTlsVerifyPull.HasValue() || SkipTlsVerifyPush.HasValue();
                var registry = new Registry(baseUri, registryLogger, authHandler, skipTlsVerify);

                registryLogger.LogDebug($"using {baseUri} for pull and push{(skipTlsVerify ? ", skipTlsVerify" : "")}");
                return (registry, registry);
            }

            var baseRegAuthHandler = new AuthenticationHandler(
                ImageHelper.GetRegistryName(BaseImage.Value()),
                credentialHelper,
                registryLogger);

            var baseRegistry = new Registry(baseUri, registryLogger, baseRegAuthHandler, SkipTlsVerifyPull.HasValue());

            registryLogger.LogDebug($"using {baseUri} for pull{(SkipTlsVerifyPull.HasValue() ? ", skipTlsVerify" : "")}");

            var destRegAuthHandler = new AuthenticationHandler(
                ImageHelper.GetRegistryName(Destination.Value()),
                credentialHelper,
                registryLogger);

            var destRegistry = new Registry(destUri, registryLogger, destRegAuthHandler, SkipTlsVerifyPush.HasValue());

            registryLogger.LogDebug($"using {destUri} for push{(SkipTlsVerifyPush.HasValue() ? ", skipTlsVerify" : "")}");

            return (baseRegistry, destRegistry);
        }

        public async Task<(ManifestV2, ImageV1)> LoadBaseImage(Registry registry)
        {
            var imageName = ImageHelper.GetImageName(BaseImage.Value());
            var imageRef = ImageHelper.GetImageReference(BaseImage.Value());

            _logger.LogDebug($"--baseImage {registry.BaseUri}, {imageName}, {imageRef}");

            var manifest = await registry.GetManifest(imageName, imageRef);
            var image = await registry.GetImage(imageName, manifest.config.digest);

            return (manifest, image);
        }

        private void UpdateImageConfig(ImageV1 image)
        {
            var config = image.config;
            var historyStrings = new List<string>();

            // do git labels before other labels, to enable users to overwrite them
            if (GitLabels.HasValue())
            {
                IDictionary<string, string> gitLabels = null;
                try
                {
                    gitLabels = Utils.GitLabels.GetLabels(GitLabels.Value());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to read git labels: {ex.Message}");
                }

                if (gitLabels != null)
                {
                    foreach (var l in gitLabels)
                    {
                        config.Labels = config.Labels ?? new Dictionary<string, string>();
                        config.Labels[l.Key] = l.Value;
                        historyStrings.Add($"--gitLabels {l.Key}={l.Value}");
                    }
                }
            }

            foreach (var label in Label.Values)
            {
                config.Labels = config.Labels ?? new Dictionary<string, string>();
                var split = label.Split('=', 2);
                if (split.Length != 2)
                {
                    throw new Exception($"Invalid label {label}");
                }

                config.Labels[split[0]] = split[1];
                historyStrings.Add($"--label {split[0]}={split[1]}");
            }

            foreach (var var in Env.Values)
            {
                config.Env = config.Env ?? new List<string>();
                config.Env.Add(var);
                historyStrings.Add($"--env {var}");
            }

            if (WorkDir.HasValue())
            {
                config.WorkingDir = WorkDir.Value();
                historyStrings.Add($"--workdir {WorkDir.Value()}");
            }

            if (User.HasValue())
            {
                config.User = User.Value();
                historyStrings.Add($"--user {User.Value()}");
            }

            if (Cmd.HasValue())
            {
                config.Cmd = SplitCmd(Cmd.Value()).ToList();
                var cmdString = string.Join(", ", config.Cmd.Select(c => $"\"{c}\""));
                historyStrings.Add($"--cmd {cmdString}");
            }

            if (Entrypoint.HasValue())
            {
                config.Entrypoint = SplitCmd(Entrypoint.Value()).ToList();
                var epString = string.Join(", ", config.Entrypoint.Select(c => $"\"{c}\""));
                historyStrings.Add($"--entrypoint {epString}");
            }

            if (historyStrings.Any())
            {
                image.history.Add(ImageV1History.Create(string.Join(", ", historyStrings), true));
            }

            foreach (var h in historyStrings)
            {
                _logger.LogDebug(h);
            }
        }

        private void UpdateConfigInManifest(ManifestV2 manifest, ImageV1 image)
        {
            var (imageBytes, imageDigest) = ToJson(image);
            manifest.config.digest = imageDigest;
            manifest.config.size = imageBytes.Length;
        }

        private BuilderLayer CreateLayer(IEnumerable<string> adds, IEnumerable<string> addFolders, string layerName)
        {
            var description = new StringBuilder();
            EnsureTempFolder();
            var archive = new Archive(Path.Combine(_tempFolderPath, $"{layerName}.tar.gz"), true);

            foreach (var a in adds)
            {
                var arg = AddArgument.Parse(a, false);
                _logger.LogDebug($"--add {arg.Source} {arg.Dest} {arg.OwnerId} {arg.GroupId} {arg.Mode.AsOctalString()}");

                description.Append($"--add . {arg.Dest} {arg.OwnerId} {arg.GroupId} {arg.Mode.AsOctalString()} ");
                archive.CreateEntries(arg.Source, arg.Dest, arg.OwnerId, arg.GroupId, arg.Mode);
            }

            foreach (var a in addFolders ?? Enumerable.Empty<string>())
            {
                var arg = AddArgument.Parse(a, true);
                _logger.LogDebug($"--addFolder {arg.Dest} {arg.OwnerId} {arg.GroupId} {arg.Mode.AsOctalString()}");

                description.Append($"--addFolder {arg.Dest} {arg.OwnerId} {arg.GroupId} {arg.Mode.AsOctalString()} ");
                archive.CreateFolderEntry(arg.Dest, arg.OwnerId, arg.GroupId, arg.Mode);
            }


            var (digest, diffId) = archive.WriteFileAndCalcDigests();
            var layer = new BuilderLayer
            {
                Name = layerName,
                Digest = digest,
                DiffId = diffId,
                Size = archive.GetSize(),
                Description = description.ToString(),
            };

            var layerLogger = CreateLogger("LAYER");
            layerLogger.LogDebug($"{layer}");
            foreach (var e in archive.Entries)
            {
                layerLogger.LogDebug($"    {Archive.PrintEntry(e.Value.Item2)}");
            }

            return layer;
        }

        private void AddLayerToConfigAndManifest(BuilderLayer layer, ManifestV2 manifest, ImageV1 image)
        {
            image.rootfs.diff_ids.Add(layer.DiffId);
            image.history.Add(ImageV1History.Create(layer.Description, null));

            var (imageBytes, imageDigest) = ToJson(image);

            manifest.config.digest = imageDigest;
            manifest.config.size = imageBytes.Length;
            manifest.layers.Add(new ManifestV2Layer
            {
                digest = layer.Digest,
                size = layer.Size,
            });
        }

        private void EnsureTempFolder()
        {
            if (TempFolder.HasValue())
            {
                _tempFolderPath = System.IO.Path.GetFullPath(TempFolder.Value());
            }
            else
            {
                _tempFolderPath = Path.GetFullPath(".nibbler");
            }

            if (!Directory.Exists(_tempFolderPath))
            {
                Directory.CreateDirectory(_tempFolderPath);
            }
        }

        private IEnumerable<string> SplitCmd(string cmd)
        {
            return cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        }

        private (byte[], string) ToJson<T>(T obj)
        {
            var content = FileHelper.JsonSerialize(obj);
            var bytes = Encoding.UTF8.GetBytes(content);
            var digest = FileHelper.Digest(bytes);
            return (bytes, digest);
        }

        private Stream GetJsonStream<T>(T obj)
        {
            var (bytes, _) = ToJson(obj);
            return new MemoryStream(bytes);
        }

        private class AddArgument
        {
            public string Source { get; set; }
            public string Dest { get; set; }
            public int? OwnerId { get; set; }
            public int? GroupId { get; set; }
            public int? Mode { get; set; }

            public static AddArgument Parse(string s, bool isFolder)
            {
                var split = s.Split(new[] { ':', ';' });
                if (!isFolder && split.Length < 2)
                {
                    throw new Exception($"Invalid add {s}");
                }

                int i = 0;
                string source = null;
                if (!isFolder)
                {
                    source = split[i++];
                }

                string dest = split[i++];

                bool hasOwner = int.TryParse(split.Skip(i++).FirstOrDefault(), out int ownerId);
                bool hasGroup = int.TryParse(split.Skip(i++).FirstOrDefault(), out int groupId);
                string modeString = split.Skip(i++).FirstOrDefault();
                int? mode = null;
                if (!string.IsNullOrEmpty(modeString))
                {
                    mode = Convert.ToInt32(modeString, 8);
                }

                return new AddArgument
                {
                    Source = source,
                    Dest = dest,
                    OwnerId = hasOwner ? ownerId : (int?)null,
                    GroupId = hasGroup ? groupId : (int?)null,
                    Mode = mode,
                };
            }
        }
    }
}
