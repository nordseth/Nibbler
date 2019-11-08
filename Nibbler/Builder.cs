using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nibbler.Models;
using Nibbler.Utils;

namespace Nibbler
{
    public class Builder
    {
        public const string ProgramName = "nibbler";

        private readonly bool _debug;
        private readonly StateFolder _store;

        public Builder(bool debug)
        {
            _debug = debug;
            var tempFolder = System.IO.Path.GetFullPath(".nibbler");
            if (_debug)
            {
                Console.WriteLine($"debug: using state folder {tempFolder}");
            }

            _store = new StateFolder(tempFolder);
        }

        public async Task Init(string baseImage, string username, string password, bool useDockerConfigFile, string dockerConfigFile, bool? insecrure, bool? skipTlsVerify)
        {
            if (_debug)
            {
                var options = $"{(insecrure == true ? ", insecrure" : "")}{(skipTlsVerify == true ? ", skipTlsVerify" : "")}";
                if (useDockerConfigFile)
                {
                    Console.WriteLine($"debug: Init({baseImage}, useConf: {dockerConfigFile}{options})");
                }
                else
                {
                    Console.WriteLine($"debug: Init({baseImage}, u: {username}, p: {password}{options})");
                }
            }

            var deleted = _store.Recreate();
            if (deleted && _debug)
            {
                Console.WriteLine($"debug: recreateing tmp folder: {_store.FolderPath}");
            }

            var state = new BuilderState
            {
                BaseImage = baseImage,
                Insecure = insecrure ?? false,
                SkipTlsVerify = skipTlsVerify ?? false,
            };

            if (useDockerConfigFile)
            {
                state.Auth = ImageHelper.GetDockerConfigAuth(ImageHelper.GetRegistryName(baseImage), dockerConfigFile);
            }
            else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                state.Auth = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"))}";
            }

            _store.SaveState(state);

            var reg = state.GetRegistry();
            var imageName = ImageHelper.GetImageName(state.BaseImage);
            var imageRef = ImageHelper.GetImageReference(state.BaseImage);

            if (_debug)
            {
                Console.WriteLine($"debug: Using {reg.BaseUri}, {imageName}, {imageRef}");
            }

            var manifest = await reg.GetManifest(imageName, imageRef);
            _store.CreateManifest(manifest);

            var image = await reg.GetImage(ImageHelper.GetImageName(state.BaseImage), manifest.config.digest);
            _store.CreateImageConfig(image);
        }

        //todo add more options
        // owner, group (unsure if we use uid or username)
        // more options on directory?
        public void Add(string source, string dest)
        {
            if (_debug)
            {
                Console.WriteLine($"debug: Add({source}, {dest})");
            }

            var state = _store.GetBuilderState();
            var layer = new BuilderLayer
            {
                Source = source,
                Dest = dest,
                Name = $"layer{state.LayersAdded.Count():00}",
            };

            var archive = new Archive(
                System.IO.Path.Combine(_store.FolderPath, $"{layer.Name}.tar.gz"),
                System.IO.Path.Combine(_store.FolderPath, $"{layer.Name}.tar"),
                true);

            archive.CreateEntries(source, dest, false, null, null);

            if (_debug)
            {
                foreach (var e in archive.Entries)
                {
                    Console.WriteLine($"debug: {Archive.PrintEntry(e.Item2)}");
                }
            }

            archive.WriteFiles();

            layer.Digest = archive.CalculateDigest();
            layer.DiffId = archive.CalculateDiffId();
            layer.Size = archive.GetSize();
            state.LayersAdded.Add(layer);
            _store.SaveState(state);

            if (_debug)
            {
                Console.WriteLine($"debug: addedLayer {layer}");
            }

            _store.AddLayerToConfig(layer, $"add . {dest}");
        }

        public void Env(string var)
        {
            if (_debug)
            {
                Console.WriteLine($"debug: Env({var})");
            }

            var image = _store.GetImageConfig();
            if (image.config.Env == null)
            {
                image.config.Env = new List<string>();
            }

            image.config.Env.Add(var);
            image.history.Add(ImageV1History.Create($"env {var}", true));

            _store.UpdateImageConfig(image);
        }

        public void Cmd(IEnumerable<string> cmd)
        {
            var cmdString = string.Join(", ", cmd.Select(c => $"\"{c}\""));
            if (_debug)
            {
                Console.WriteLine($"debug: cmd({cmdString})");
            }

            var image = _store.GetImageConfig();
            image.config.Cmd = cmd.ToList();
            image.history.Add(ImageV1History.Create($"cmd {cmdString}", true));

            _store.UpdateImageConfig(image);
        }

        public void Entrypoint(IEnumerable<string> cmd)
        {
            var cmdString = string.Join(", ", cmd.Select(c => $"\"{c}\""));
            if (_debug)
            {
                Console.WriteLine($"debug: entrypoint({cmdString})");
            }

            var image = _store.GetImageConfig();
            image.config.Entrypoint = cmd.ToList();
            image.history.Add(ImageV1History.Create($"entrypoint {cmdString}", true));

            _store.UpdateImageConfig(image);
        }

        public void Label(string name, string value)
        {
            if (_debug)
            {
                Console.WriteLine($"debug: Label({name}, {value})");
            }

            var image = _store.GetImageConfig();
            if (image.config.Labels == null)
            {
                image.config.Labels = new Dictionary<string, string>();
            }

            image.config.Labels[name] = value;
            image.history.Add(ImageV1History.Create($"label {name}={value}", true));

            _store.UpdateImageConfig(image);
        }

        public void Workdir(string workdir)
        {
            if (_debug)
            {
                Console.WriteLine($"debug: Workdir({workdir})");
            }

            var image = _store.GetImageConfig();
            image.config.WorkingDir = workdir;
            image.history.Add(ImageV1History.Create($"workdir {workdir}", true));

            _store.UpdateImageConfig(image);
        }

        public void User(string user)
        {
            if (_debug)
            {
                Console.WriteLine($"debug: User({user})");
            }

            var image = _store.GetImageConfig();
            image.config.User = user;
            image.history.Add(ImageV1History.Create($"user {user}", true));

            _store.UpdateImageConfig(image);
        }

        public async Task Push(string dest, bool dryRun)
        {
            if (_debug)
            {
                Console.WriteLine($"debug: Push({dest}, dryRun: {dryRun})");
            }

            var pusher = new Pusher(_store.GetBuilderState(), dest, true, _debug);

            pusher.ValidateDest();
            var manifest = _store.GetManifest();

            bool configExists = await pusher.CheckConfigExists(manifest);
            await pusher.ValidateLayers(manifest);

            if (!dryRun)
            {
                if (!configExists)
                {
                    await pusher.PushConfig(manifest.config, _store.GetImageConfigStream);
                }

                await pusher.PushLayers(f => _store.GetReadStream(f));
                await pusher.PushManifest(_store.GetManifestStream);
            }

            string manifestDigest;
            using (var manifestStream = _store.GetManifestStream())
            {
                manifestDigest = FileHelper.Digest(manifestStream);
            }
            _store.WritePushedImageDigest(manifestDigest);
            Console.WriteLine(manifestDigest);
        }
    }
}
