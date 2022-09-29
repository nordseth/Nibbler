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
        public CommandOption ToArchive { get; private set; }
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
        public CommandOption Trace { get; private set; }
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
            ToArchive = app.Option("--to-archive", "Exprimental: Write image to docker archive (alternative to --to-image)", CommandOptionType.SingleValue);

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
            Trace = app.Option("--trace", "Trace log. INSECURE! Exposes authentication headers", CommandOptionType.NoValue);
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

            if (!ToImage.HasValue() && !ToFile.HasValue() && !ToArchive.HasValue())
            {
                validationErrors.Add(($"--{ToImage.LongName}, --{ToFile.LongName} or --{ToArchive.LongName}  is required.", new[] { ToImage.LongName, ToFile.LongName, ToArchive.LongName }));
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
            var run = new BuildRun(Verbose.HasValue(), Trace.HasValue());

            try
            {
                SetImageSource(run);
                SetImageDest(run);
                SetConfig(run);

                foreach (var a in Add.Values ?? Enumerable.Empty<string>())
                {
                    run.Add.Add(AddArgument.Parse(a, false));
                }

                foreach (var a in AddFolder.Values ?? Enumerable.Empty<string>())
                {
                    run.AddFolder.Add(AddArgument.Parse(a, true));
                }

                run.DryRun = DryRun.HasValue();
                run.TempFolderPath = TempFolder.Value();
                run.WriteDigestFile = DigestFile.HasValue();
                run.DigestFilepath = DigestFile.Value();

                await run.ExecuteAsync();

                run.Logger.LogDebug($"completed in {sw.ElapsedMilliseconds} ms");
                Console.WriteLine(run.ManifestDigest);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                run.Logger.LogDebug(ex, "exception");
                run.Logger.LogDebug($"completed in {sw.ElapsedMilliseconds} ms");

                return 1;
            }
        }

        private void SetImageSource(BuildRun run)
        {
            if (FromImage.HasValue())
            {
                run.SetRegistryImageSource(
                    FromImage.Value(),
                    FromUsername.Value(),
                    FromPassword.Value(),
                    Insecure.HasValue() || FromInsecure.HasValue(),
                    FromSkipTlsVerify.HasValue() || SkipTlsVerify.HasValue(),
                    DockerConfig.Value());
            }
            else
            {
                run.SetFileImageSource(FromFile.Value());
            }
        }

        private void SetImageDest(BuildRun run)
        {
            if (ToImage.HasValue())
            {
                run.SetRegistoryImageDest(
                    ToImage.Value(),
                    ToUsername.Value(),
                    ToPassword.Value(),
                    Insecure.HasValue() || ToInsecure.HasValue(),
                    ToSkipTlsVerify.HasValue() || SkipTlsVerify.HasValue(),
                    DockerConfig.Value());
            }
            else if (ToArchive.HasValue())
            {
                run.SetDockerArchiveDest(ToArchive.Value());
            }
            else
            {
                run.SetFileImageDest(ToFile.Value());
            }
        }

        private void SetConfig(BuildRun run)
        {
            if (GitLabels.HasValue())
            {
                run.AddGitLabels = true;
                run.GitLabelsPrefix = GitLabelsPrefix.Value();
                run.GitRepoPath = GitLabels.Value();
            }

            foreach (var label in Label.Values)
            {
                var split = label.Split('=', 2);
                if (split.Length != 2)
                {
                    throw new Exception($"Invalid label {label}");
                }

                run.Labels.Add(split[0], split[1]);
            }

            foreach (var var in Env.Values)
            {
                run.Env.Add(var);
            }

            if (WorkDir.HasValue())
            {
                run.WorkingDir = WorkDir.Value();
            }

            if (User.HasValue())
            {
                run.User = User.Value();
            }

            if (Cmd.HasValue())
            {
                run.Cmd = SplitCmd(Cmd.Value()).ToList();
            }

            if (Entrypoint.HasValue())
            {
                run.Entrypoint = SplitCmd(Entrypoint.Value()).ToList();
            }
        }

        private IEnumerable<string> SplitCmd(string cmd)
        {
            return cmd.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
