using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler.Command
{
    public class BuildCommand
    {
        private const string ManifestDigestFileName = "digest";
        private string _tempFolderPath;

        public BuildCommand()
        {
        }

        public CommandOption BaseImage { get; private set; }
        public CommandOption Destination { get; private set; }

        public CommandOption Add { get; private set; }

        public CommandOption Label { get; private set; }
        public CommandOption Env { get; private set; }
        public CommandOption GitLabels { get; private set; }
        public CommandOption WorkDir { get; private set; }
        public CommandOption User { get; private set; }
        public CommandOption Cmd { get; private set; }
        public CommandOption Entrypoint { get; private set; }

        public CommandOption Debug { get; private set; }
        public CommandOption DryRun { get; private set; }

        public CommandOption Username { get; private set; }
        public CommandOption Password { get; private set; }
        public CommandOption DockerConfig { get; private set; }
        public CommandOption Insecure { get; private set; }
        public CommandOption SkipTlsVerify { get; private set; }
        public CommandOption TempFolder { get; private set; }
        public CommandOption DigestFile { get; private set; }

        private bool _debug => Debug.HasValue();

        public void AddOptions(CommandLineApplication app)
        {
            // init and push replacements, required arguments
            BaseImage = app.Option("--base-image", "Set base image (required)", CommandOptionType.SingleValue).IsRequired();
            Destination = app.Option("--destination", "Destination to push the modified image  (required)", CommandOptionType.SingleValue).IsRequired();

            // "commands"
            Add = app.Option("--add", "Add a folder to the image 'sourceFolder:destFolder'", CommandOptionType.MultipleValue);
            Label = app.Option("--label", "Add label to the image 'name=value'", CommandOptionType.MultipleValue);
            Env = app.Option("--env", "Add a environment variable to the image 'name=value'", CommandOptionType.MultipleValue);
            GitLabels = app.Option("--git-labels", "Add common git labels to image, optionally define the path to the git repo.", CommandOptionType.SingleOrNoValue);
            WorkDir = app.Option("--workdir", "Set the working directory in the image", CommandOptionType.SingleValue);
            User = app.Option("--user", "Set the user in the image", CommandOptionType.SingleValue);
            Cmd = app.Option("--cmd", "Set the image cmd", CommandOptionType.SingleValue);
            Entrypoint = app.Option("--entrypoint", "Set the image entrypoint", CommandOptionType.SingleValue);

            // options:
            Debug = app.Option("--debug", "Output debug log", CommandOptionType.NoValue);
            DryRun = app.Option("--dry-run", "Does not push, only shows what would happen (use with --debug)", CommandOptionType.NoValue);
            Username = app.Option("--username", "Registry username", CommandOptionType.SingleValue);
            Password = app.Option("--password", "Registry password", CommandOptionType.SingleValue);
            DockerConfig = app.Option("--docker-config", "Use docker config file to authenticate with registry, optionally specify file path", CommandOptionType.SingleOrNoValue);
            Insecure = app.Option("--insecure", "Insecure registry (http)", CommandOptionType.NoValue);
            SkipTlsVerify = app.Option("--skip-tls-verify", "Skip verifying registry TLS certificate", CommandOptionType.NoValue);

            TempFolder = app.Option("--temp-folder", "Set temp folder (default is ./.nibbler)", CommandOptionType.SingleValue);
            DigestFile = app.Option("--digest-file", "Output image digest to file, optionally specify file", CommandOptionType.SingleOrNoValue);
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                var registry = CreateRegistry();
                var (manifest, image) = await LoadBaseImage(registry);
                UpdateImageConfig(image);
                UpdateConfigInManifest(manifest, image);

                var layersAdded = new List<BuilderLayer>();
                foreach (var folder in Add.Values)
                {
                    var layer = AddFolder(folder, $"layer{layersAdded.Count():00}");
                    layersAdded.Add(layer);
                    AddLayerToConfigAndManifest(layer, manifest, image);
                }

                var pusher = new Pusher(BaseImage.Value(), Destination.Value(), layersAdded, registry, _debug);
                pusher.ValidateDest();
                bool configExists = await pusher.CheckConfigExists(manifest);
                await pusher.ValidateLayers(manifest, !DryRun.HasValue());

                if (!DryRun.HasValue())
                {
                    if (!configExists)
                    {
                        await pusher.PushConfig(manifest.config, () => GetJsonStream(image));
                    }

                    await pusher.PushLayers(f => File.OpenRead(Path.Combine(_tempFolderPath, f)));
                    await pusher.PushManifest(() => GetJsonStream(manifest));
                }

                string manifestDigest;
                using (var manifestStream = GetJsonStream(manifest))
                {
                    manifestDigest = FileHelper.Digest(manifestStream);
                }

                Console.WriteLine(manifestDigest);

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

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (_debug)
                {
                    Console.Error.WriteLine(ex);
                }

                return 1;
            }
        }

        private Registry CreateRegistry()
        {
            var baseUri = ImageHelper.GetRegistryBaseUrl(BaseImage.Value(), Insecure.HasValue());
            if (_debug)
            {
                var options = $"{(SkipTlsVerify.HasValue() ? ", skipTlsVerify" : "")}";
                if (DockerConfig.HasValue())
                {
                    Console.WriteLine($"debug: using registry: {baseUri}, useConf: {DockerConfig.Value()}{options})");
                }
                else if (!string.IsNullOrEmpty(Username.Value()) && !string.IsNullOrEmpty(Password.Value()))
                {
                    Console.WriteLine($"debug: using registry: {baseUri}, u: {Username.HasValue()}, p: {Password.HasValue()}{options})");
                }
                else
                {
                    Console.WriteLine($"debug: using registry: {baseUri}{options})");
                }
            }

            var registry = new Registry(baseUri, SkipTlsVerify.HasValue());

            if (DockerConfig.HasValue())
            {
                var auth = ImageHelper.GetDockerConfigAuth(ImageHelper.GetRegistryName(BaseImage.Value()), DockerConfig.Value());
                registry.UseAuthorization(auth);
            }
            else if (!string.IsNullOrEmpty(Username.Value()) && !string.IsNullOrEmpty(Password.Value()))
            {
                var auth = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username.Value()}:{Password.Value()}"))}";
                registry.UseAuthorization(auth);
            }

            return registry;
        }

        public async Task<(ManifestV2, ImageV1)> LoadBaseImage(Registry registry)
        {
            var imageName = ImageHelper.GetImageName(BaseImage.Value());
            var imageRef = ImageHelper.GetImageReference(BaseImage.Value());

            if (_debug)
            {
                Console.WriteLine($"debug: --baseImage {registry.BaseUri}, {imageName}, {imageRef}");
            }

            var manifest = await registry.GetManifest(imageName, imageRef);
            var image = await registry.GetImage(imageName, manifest.config.digest);

            return (manifest, image);
        }

        private void UpdateImageConfig(ImageV1 image)
        {
            var historyStrings = new List<string>();

            // do git labels before labels, to enable users to overwrite them
            if (GitLabels.HasValue())
            {
                image.config.Labels = image.config.Labels ?? new Dictionary<string, string>();
                var gitLabels = Utils.GitLabels.GetLabels(GitLabels.Value());

                foreach (var l in gitLabels)
                {
                    image.config.Labels[l.Key] = l.Value;
                    historyStrings.Add($"--gitLabels {l.Key}={l.Value}");
                }
            }

            foreach (var label in Label.Values)
            {
                image.config.Labels = image.config.Labels ?? new Dictionary<string, string>();
                var split = label.Split('=', 2);
                if (split.Length != 2)
                {
                    throw new Exception($"Invalid label {label}");
                }

                image.config.Labels[split[0]] = split[1];
                historyStrings.Add($"--label {split[0]}={split[1]}");
            }

            foreach (var var in Env.Values)
            {
                image.config.Env = image.config.Env ?? new List<string>();
                image.config.Env.Add(var);
                historyStrings.Add($"--env {var}");
            }

            if (WorkDir.HasValue())
            {
                image.config.WorkingDir = WorkDir.Value();
                historyStrings.Add($"--workdir {WorkDir.Value()}");
            }

            if (User.HasValue())
            {
                image.config.User = User.Value();
                historyStrings.Add($"--user {User.Value()}");
            }

            if (Cmd.HasValue())
            {
                image.config.Cmd = SplitCmd(Cmd.Value()).ToList();
                var cmdString = string.Join(", ", image.config.Cmd.Select(c => $"\"{c}\""));
                historyStrings.Add($"--cmd {cmdString}");
            }

            if (Entrypoint.HasValue())
            {
                image.config.Entrypoint = SplitCmd(Entrypoint.Value()).ToList();
                var epString = string.Join(", ", image.config.Entrypoint.Select(c => $"\"{c}\""));
                historyStrings.Add($"--entrypoint {epString}");
            }

            image.history.Add(ImageV1History.Create(string.Join(", ", historyStrings), true));
            if (_debug)
            {
                foreach (var h in historyStrings)
                {
                    Console.WriteLine($"debug: {h}");
                }
            }
        }

        private void UpdateConfigInManifest(ManifestV2 manifest, ImageV1 image)
        {
            var (imageBytes, imageDigest) = ToJson(image);
            manifest.config.digest = imageDigest;
            manifest.config.size = imageBytes.Length;
        }

        private BuilderLayer AddFolder(string folder, string layerName)
        {
            // todo: windows path will not work as we will get more than one ':'
            // should we use a different seperator, or find the last ':'?
            var split = folder.Split(':', 2);
            if (split.Length != 2)
            {
                throw new Exception($"Invalid add {folder}");
            }

            var source = split[0];
            var dest = split[1];

            if (_debug)
            {
                Console.WriteLine($"debug: --add {source} {dest}");
            }

            var layer = new BuilderLayer
            {
                Source = source,
                Dest = dest,
                Name = layerName,
            };

            EnsureTempFolder();
            var archive = new Archive(Path.Combine(_tempFolderPath, $"{layerName}.tar.gz"), true);

            archive.CreateEntries(source, dest, false, null, null);

            if (_debug)
            {
                foreach (var e in archive.Entries)
                {
                    Console.WriteLine($"debug:  (file) {Archive.PrintEntry(e.Item2)}");
                }
            }

            var (digest, diffId) = archive.WriteFileAndCalcDigests();

            layer.Digest = digest;
            layer.DiffId = diffId;
            layer.Size = archive.GetSize();

            if (_debug)
            {
                Console.WriteLine($"debug:  (layer) {layer}");
            }

            return layer;
        }

        private void AddLayerToConfigAndManifest(BuilderLayer layer, ManifestV2 manifest, ImageV1 image)
        {
            image.rootfs.diff_ids.Add(layer.DiffId);
            image.history.Add(ImageV1History.Create($"--add {layer.Dest}", null));

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
    }
}
