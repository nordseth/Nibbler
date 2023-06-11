using Ignore;
using Nibbler.Models;
using Nibbler.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nibbler.Command
{
    public class BuildRun
    {
        private const string ManifestDigestFileName = "digest";

        private readonly ILogger _httpClientLogger;
        private readonly HttpClientFactory _httpClientFactory;
        public Logger Logger { get; }

        private IImageSource _imageSource;
        private Func<IImageSource> _imageSourceFactory;
        private Func<IEnumerable<BuilderLayer>, IImageDestination> _imageDestinationFactory;
        private Image _fromImage;
        private Image _image;

        public bool DryRun { get; set; } = false;
        public string TempFolderPath { get; set; }
        public bool WriteDigestFile { get; set; }
        public string DigestFilepath { get; set; }

        public IList<AddArgument> Add { get; set; } = new List<AddArgument>();
        public IList<AddArgument> AddFolder { get; set; } = new List<AddArgument>();
        public bool Reproducible { get; set; } = true;
        public string IgnoreFile { get; set; }

        public bool AddGitLabels { get; set; } = false;
        public string GitRepoPath { get; set; }
        public string GitLabelsPrefix { get; set; }
        public IDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
        public string WorkingDir { get; set; }
        public string User { get; set; }
        public IList<string> Env { get; set; } = new List<string>();
        public IEnumerable<string> Cmd { get; set; } = Enumerable.Empty<string>();
        public IEnumerable<string> Entrypoint { get; set; } = Enumerable.Empty<string>();

        public string FromImageManifestDigest => _fromImage?.ManifestDigest;
        public ImageV1 FromImageConfig => _fromImage?.Config;
        public string ManifestDigest => _image?.ManifestDigest;
        public ImageV1 ImageConfig => _image?.Config;

        public BuildRun(bool debug = false, bool trace = false)
        {
            Logger = new Logger("BUILD", debug, trace);
            _httpClientLogger = new Logger("HTTP", debug, trace);
            _httpClientFactory = new HttpClientFactory(_httpClientLogger);
        }

        private ILogger CreateLogger(string name)
        {
            return new Logger(name, Logger.DebugEnabled, Logger.TraceEnabled);
        }

        public void SetRegistryImageSource(string image, string username, string password, bool insecure, bool skipTlsVerify, string dockerConfig)
        {
            _imageSourceFactory = () => RegistryImageSource.Create(
                image,
                username,
                password,
                insecure,
                skipTlsVerify,
                dockerConfig,
                CreateLogger("FROM-REG"),
                _httpClientFactory);
        }

        public void SetFileImageSource(string file)
        {
            _imageSourceFactory = () => new FileImageSource(file, CreateLogger("FILE"));
        }

        public void SetRegistoryImageDest(string image, string username, string password, bool insecure, bool skipTlsVerify, string dockerConfig)
        {
            _imageDestinationFactory = addedLayers =>
            {
                var logger = CreateLogger("TO-REG");

                var toUri = ImageHelper.GetRegistryBaseUrl(image, insecure);

                var dockerConfigCredentials = new DockerConfigCredentials(dockerConfig);
                var toRegAuthHandler = new AuthenticationHandler(
                    ImageHelper.GetRegistryName(image),
                    dockerConfigCredentials,
                    true,
                    logger,
                    _httpClientFactory.Create(null, false, null));

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    toRegAuthHandler.SetCredentials(username, password);
                }

                var toSkipTlsVerify = skipTlsVerify;
                var toRegistryClient = _httpClientFactory.Create(toUri, toSkipTlsVerify, toRegAuthHandler);
                var toRegistry = new Registry(logger, toRegistryClient);

                logger.LogDebug($"using {toUri} for push{(toSkipTlsVerify ? ", skipTlsVerify" : "")}");

                return new RegistryPusher(image, toRegistry, addedLayers, logger);
            };
        }

        public void SetFileImageDest(string file)
        {
            _imageDestinationFactory = addedLayers => new FileImageDestination(file, addedLayers, CreateLogger("FILE"));
        }

        public async Task LoadSourceImage()
        {
            // validate _imageSourceFactory not null?
            _imageSource = _imageSourceFactory();
            _fromImage = await _imageSource.LoadImage();
        }

        public async Task ExecuteAsync()
        {
            if (_fromImage == null)
            {
                await LoadSourceImage();
            }

            _image = _fromImage.Clone();

            var imageUpdated = UpdateImageConfig(_image.Config);
            if (imageUpdated)
            {
                _image.ConfigUpdated();
            }

            if (Add.Any() || AddFolder.Any())
            {
                var layer = CreateLayer(Add, AddFolder, $"layer{_image.LayersAdded.Count():00}");
                _image.AddLayer(layer);
            }

            if (!_image.ManifestUpdated)
            {
                Logger.LogDebug("No changes to image, will copy image.");
            }

            await WriteImage();

            Logger.LogDebug($"Image digest: {_image.ManifestDigest}");

            if (WriteDigestFile)
            {
                if (string.IsNullOrEmpty(DigestFilepath))
                {
                    EnsureTempFolder();
                    DigestFilepath = Path.Combine(TempFolderPath, ManifestDigestFileName);
                }

                File.WriteAllText(DigestFilepath, _image.ManifestDigest);
            }
        }

        private async Task WriteImage()
        {
            // validate _imageDestinationFactory not null?
            var pusher = _imageDestinationFactory(_image.LayersAdded);

            bool configExists = await pusher.CheckConfigExists(_image.Manifest);
            var missingLayers = await pusher.FindMissingLayers(_image.Manifest);

            if (!DryRun)
            {
                if (!configExists)
                {
                    await pusher.PushConfig(_image.Manifest.config, () => new MemoryStream(_image.ConfigBytes));
                }

                await pusher.CopyLayers(_imageSource, missingLayers);
                await pusher.PushLayers(f => File.OpenRead(Path.Combine(TempFolderPath, f)));
                await pusher.PushManifest(_image.Manifest.mediaType, () => new MemoryStream(_image.ManifestBytes));
            }
        }

        private bool UpdateImageConfig(ImageV1 image)
        {
            var config = image.config;
            var historyStrings = new List<string>();

            // do git labels before other labels, to enable users to overwrite them
            if (AddGitLabels)
            {
                IDictionary<string, string> gitLabels = null;
                try
                {
                    gitLabels = Utils.GitLabels.GetLabels(GitRepoPath, GitLabelsPrefix);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to read git labels: {ex.Message}");
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

            foreach (var label in Labels)
            {
                config.Labels = config.Labels ?? new Dictionary<string, string>();
                config.Labels[label.Key] = label.Value;
                historyStrings.Add($"--label {label.Key}={label.Value}");
            }

            foreach (var var in Env)
            {
                config.Env = config.Env ?? new List<string>();
                config.Env.Add(var);
                historyStrings.Add($"--env {var}");
            }

            if (!string.IsNullOrEmpty(WorkingDir))
            {
                config.WorkingDir = WorkingDir;
                historyStrings.Add($"--workdir {WorkingDir}");
            }

            if (!string.IsNullOrEmpty(User))
            {
                config.User = User;
                historyStrings.Add($"--user {User}");
            }

            if (Cmd.Any())
            {
                config.Cmd = Cmd.ToList();
                var cmdString = string.Join(", ", config.Cmd.Select(c => $"\"{c}\""));
                historyStrings.Add($"--cmd {cmdString}");
            }

            if (Entrypoint.Any())
            {
                config.Entrypoint = Entrypoint.ToList();
                var epString = string.Join(", ", config.Entrypoint.Select(c => $"\"{c}\""));
                historyStrings.Add($"--entrypoint {epString}");
            }

            if (historyStrings.Any())
            {
                image.history.Add(ImageV1History.Create(string.Join(", ", historyStrings), true));
            }

            foreach (var h in historyStrings)
            {
                Logger.LogDebug(h);
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

        private BuilderLayer CreateLayer(IEnumerable<AddArgument> adds, IEnumerable<AddArgument> addFolders, string layerName)
        {
            var description = new StringBuilder();
            EnsureTempFolder();
            var archive = new Archive(Path.Combine(TempFolderPath, $"{layerName}.tar.gz"), Reproducible, new[] { TempFolderPath }, IgnoreFile, Logger);

            foreach (var arg in adds)
            {
                Logger.LogDebug($"--add {arg.Source} {arg.Dest} {arg.OwnerId} {arg.GroupId} {arg.Mode.AsOctalString()}");

                description.Append($"--add . {arg.Dest} {arg.OwnerId} {arg.GroupId} {arg.Mode.AsOctalString()} ");
                archive.CreateEntries(arg.Source, arg.Dest, arg.OwnerId, arg.GroupId, arg.Mode);
            }

            foreach (var arg in addFolders)
            {
                Logger.LogDebug($"--addFolder {arg.Dest} {arg.OwnerId} {arg.GroupId} {arg.Mode.AsOctalString()}");

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
            if (!string.IsNullOrEmpty(TempFolderPath))
            {
                TempFolderPath = System.IO.Path.GetFullPath(TempFolderPath);
            }
            else
            {
                TempFolderPath = Path.GetFullPath(".nibbler");
            }

            if (!Directory.Exists(TempFolderPath))
            {
                Directory.CreateDirectory(TempFolderPath);
            }
        }
    }
}
