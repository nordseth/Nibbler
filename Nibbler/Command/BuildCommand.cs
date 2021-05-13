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

        public CommandOption FromImage { get; private set; }
        public CommandOption FromInsecure { get; private set; }
        public CommandOption FromSkipTlsVerify { get; private set; }
        public CommandOption FromUsername { get; private set; }
        public CommandOption FromPassword { get; private set; }

        public CommandOption ToImage { get; private set; }
        public CommandOption ToInsecure { get; private set; }
        public CommandOption ToSkipTlsVerify { get; private set; }
        public CommandOption ToUsername { get; private set; }
        public CommandOption ToPassword { get; private set; }

        public CommandOption FromFile { get; private set; }
        public CommandOption ToFile { get; private set; }

        public CommandOption Add { get; private set; }
        public CommandOption AddFolder { get; private set; }

        public CommandOption Label { get; private set; }
        public CommandOption Env { get; private set; }
        public CommandOption GitLabels { get; private set; }
        public CommandOption GitLabelsPrefix { get; private set; }
        public CommandOption WorkDir { get; private set; }
        public CommandOption User { get; private set; }
        public CommandOption Cmd { get; private set; }
        public CommandOption Entrypoint { get; private set; }

        public CommandOption Verbose { get; private set; }
        public CommandOption DryRun { get; private set; }

        public CommandOption DockerConfig { get; private set; }

        public CommandOption Insecure { get; private set; }
        public CommandOption SkipTlsVerify { get; private set; }

        public CommandOption TempFolder { get; private set; }
        public CommandOption DigestFile { get; private set; }

        public void AddOptions(CommandLineApplication app)
        {
            // from and to registries. From and to image are required arguments
            FromImage = app.Option("--from-image", "Set from image (required)", CommandOptionType.SingleValue);
            FromInsecure = app.Option("--from-insecure", "Insecure from registry (http)", CommandOptionType.NoValue);
            FromSkipTlsVerify = app.Option("--from-skip-tls-verify", "Skip verifying from registry TLS certificate", CommandOptionType.NoValue);
            FromUsername = app.Option("--from-username", "From registry username", CommandOptionType.SingleValue);
            FromPassword = app.Option("--from-password", "From registry password", CommandOptionType.SingleValue);

            ToImage = app.Option("--to-image", "To image (required)", CommandOptionType.SingleValue);
            ToInsecure = app.Option("--to-insecure", "Insecure to registry (http)", CommandOptionType.NoValue);
            ToSkipTlsVerify = app.Option("--to-skip-tls-verify", "Skip verifying to registry TLS certificate", CommandOptionType.NoValue);
            ToUsername = app.Option("--to-username", "To registry username", CommandOptionType.SingleValue);
            ToPassword = app.Option("--to-password", "To registry password", CommandOptionType.SingleValue);

            // alternative to --from-image and --to-image
            FromFile = app.Option("--from-file", "Read from image from file (alternative to --from-image)", CommandOptionType.SingleValue);
            ToFile = app.Option("--to-file", "Write image to file (alternative to --to-image)", CommandOptionType.SingleValue);

            // "commands"
            Add = app.Option("--add", "Add contents of a folder to the image 'sourceFolder:destFolder[:ownerId:groupId:permissions]'", CommandOptionType.MultipleValue);
            AddFolder = app.Option("--addFolder", "Add a folder to the image 'destFolder[:ownerId:groupId:permissions]'", CommandOptionType.MultipleValue);
            Label = app.Option("--label", "Add label to the image 'name=value'", CommandOptionType.MultipleValue);
            Env = app.Option("--env", "Add a environment variable to the image 'name=value'", CommandOptionType.MultipleValue);
            GitLabels = app.Option("--git-labels", "Add common git labels to image, optionally define the path to the git repo.", CommandOptionType.SingleOrNoValue);
            GitLabelsPrefix = app.Option("--git-labels-prefix", "Specify the prefix of the git labels. (default: 'nibbler.git')", CommandOptionType.SingleValue);
            WorkDir = app.Option("--workdir", "Set the working directory in the image", CommandOptionType.SingleValue);
            User = app.Option("--user", "Set the user in the image", CommandOptionType.SingleValue);
            Cmd = app.Option("--cmd", "Set the image cmd", CommandOptionType.SingleValue);
            Entrypoint = app.Option("--entrypoint", "Set the image entrypoint", CommandOptionType.SingleValue);

            // options:
            Verbose = app.Option("-v|--debug", "Verbose output", CommandOptionType.NoValue);
            DryRun = app.Option("--dry-run", "Does not push, only shows what would happen (use with -v)", CommandOptionType.NoValue);

            DockerConfig = app.Option("--docker-config", "Specify docker config file for authentication with registry. (default: ~/.docker/config.json)", CommandOptionType.SingleOrNoValue);

            Insecure = app.Option("--insecure", "Insecure registry (http). Only use if base image and destination is the same registry.", CommandOptionType.NoValue);
            SkipTlsVerify = app.Option("--skip-tls-verify", "Skip verifying registry TLS certificate. Only use if base image and destination is the same registry.", CommandOptionType.NoValue);

            TempFolder = app.Option("--temp-folder", "Set temp folder (default: ./.nibbler)", CommandOptionType.SingleValue);
            DigestFile = app.Option("--digest-file", "Output image digest to file, optionally specify file", CommandOptionType.SingleOrNoValue);
        }

        public ValidationResult Validate(ValidationContext context)
        {
            var validationErrors = new List<(string, IEnumerable<string>)>();

            if (!FromImage.HasValue() && !FromFile.HasValue())
            {
                validationErrors.Add(($"--{FromImage.LongName} or --{FromFile.LongName} is required.", new[] { FromImage.LongName, FromFile.LongName }));
            }

            if (!ToImage.HasValue() && !ToFile.HasValue())
            {
                validationErrors.Add(($"--{ToImage.LongName} or --{ToFile.LongName}  is required.", new[] { ToImage.LongName, ToFile.LongName }));
            }

            if (!validationErrors.Any() &&
                (Insecure.HasValue() || SkipTlsVerify.HasValue()))
            {

                var srcReg = ImageHelper.GetRegistryName(FromImage.Value());
                var destReg = ImageHelper.GetRegistryName(ToImage.Value());

                if (srcReg != destReg)
                {
                    var fields = new List<string>();

                    if (Insecure.HasValue())
                    {
                        fields.Add(Insecure.LongName);
                    }

                    if (SkipTlsVerify.HasValue())
                    {
                        fields.Add(SkipTlsVerify.LongName);
                    }

                    validationErrors.Add(($"{string.Join(", ", fields)} can only be set if from image registry is the same as destination (to)", fields));
                }
            }

            if (validationErrors.Any())
            {
                return new ValidationResult(
                    string.Join(", ", validationErrors.Select(e => e.Item1)),
                    validationErrors.SelectMany(e => e.Item2));
            }

            // todo: if --from-file or --to-file is set, print warning if other From* or To* args are used, as they are ignored

            return ValidationResult.Success;
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _logger.SetDebugEnable(Verbose.HasValue());

            try
            {
                var imageSource = CreateImageSource();
                var image = await imageSource.LoadImage();
                var imageUpdated = UpdateImageConfig(image.Config);

                if (imageUpdated)
                {
                    image.ConfigUpdated();
                }

                if (Add.HasValue() || AddFolder.HasValue())
                {
                    var layer = CreateLayer(Add.Values, AddFolder.Values, $"layer{image.LayersAdded.Count():00}");
                    image.AddLayer(layer);
                }

                if (!image.ManifestUpdated)
                {
                    _logger.LogDebug("No changes to image, will copy image.");
                }

                var pusher = CreateImageDest(image.LayersAdded);

                bool configExists = await pusher.CheckConfigExists(image.Manifest);
                var missingLayers = await pusher.FindMissingLayers(image.Manifest);

                if (!DryRun.HasValue())
                {
                    if (!configExists)
                    {
                        await pusher.PushConfig(image.Manifest.config, () => new MemoryStream(image.ConfigBytes));
                    }

                    await pusher.CopyLayers(imageSource, missingLayers);
                    await pusher.PushLayers(f => File.OpenRead(Path.Combine(_tempFolderPath, f)));
                    await pusher.PushManifest(() => new MemoryStream(image.ManifestBytes));
                }

                _logger.LogDebug($"Image digest: {image.ManifestDigest}");

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

                    File.WriteAllText(digestFilepath, image.ManifestDigest);
                }

                _logger.LogDebug($"completed in {sw.ElapsedMilliseconds} ms");
                Console.WriteLine(image.ManifestDigest);

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

        internal IImageSource CreateImageSource()
        {
            if (FromImage.HasValue())
            {
                return RegistryImageSource.Create(
                    FromImage.Value(),
                    FromUsername.Value(),
                    FromPassword.Value(),
                    Insecure.HasValue() || FromInsecure.HasValue(),
                    FromSkipTlsVerify.HasValue() || SkipTlsVerify.HasValue(),
                    DockerConfig.Value(),
                    CreateLogger("FROM-REG"));
            }
            else
            {
                return new FileImageSource(FromFile.Value(), CreateLogger("FILE"));
            }
        }

        internal IImageDestination CreateImageDest(IEnumerable<BuilderLayer> addedLayers)
        {
            if (ToImage.HasValue())
            {
                var logger = CreateLogger("TO-REG");

                var toUri = ImageHelper.GetRegistryBaseUrl(ToImage.Value(), Insecure.HasValue() || ToInsecure.HasValue());

                var dockerConfigCredentials = new DockerConfigCredentials(DockerConfig.Value());
                var toRegAuthHandler = new AuthenticationHandler(
                    ImageHelper.GetRegistryName(ToImage.Value()),
                    dockerConfigCredentials,
                    logger);

                if (ToUsername.HasValue() && ToPassword.HasValue())
                {
                    toRegAuthHandler.SetCredentials(ToUsername.Value(), ToPassword.Value());
                }

                var toSkipTlsVerify = ToSkipTlsVerify.HasValue() || SkipTlsVerify.HasValue();
                var toRegistry = new Registry(toUri, logger, toRegAuthHandler, toSkipTlsVerify);

                logger.LogDebug($"using {toUri} for push{(toSkipTlsVerify ? ", skipTlsVerify" : "")}");

                return new RegistryPusher(ToImage.Value(), toRegistry, addedLayers, logger);
            }
            else
            {
                return new FileImageDestination(ToFile.Value(), addedLayers, CreateLogger("FILE"));
            }
        }

        private bool UpdateImageConfig(ImageV1 image)
        {
            var config = image.config;
            var historyStrings = new List<string>();

            // do git labels before other labels, to enable users to overwrite them
            if (GitLabels.HasValue())
            {
                IDictionary<string, string> gitLabels = null;
                try
                {
                    gitLabels = Utils.GitLabels.GetLabels(GitLabels.Value(), GitLabelsPrefix.Value());
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

            if (historyStrings.Any())
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private BuilderLayer CreateLayer(IEnumerable<string> adds, IEnumerable<string> addFolders, string layerName)
        {
            var description = new StringBuilder();
            EnsureTempFolder();
            var archive = new Archive(Path.Combine(_tempFolderPath, $"{layerName}.tar.gz"), true, new[] { _tempFolderPath }, _logger);

            foreach (var a in adds ?? Enumerable.Empty<string>())
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
