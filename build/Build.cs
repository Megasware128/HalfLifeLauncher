using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Octokit;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.CompressionTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Git.GitTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
[GitHubActions("package", GitHubActionsImage.UbuntuLatest, On = new[] { GitHubActionsTrigger.Push, GitHubActionsTrigger.PullRequest }, InvokedTargets = new[] { nameof(Pack) })]
[GitHubActions("publish", GitHubActionsImage.UbuntuLatest, OnPushTags = new[] { "v*" }, InvokedTargets = new[] { nameof(Publish) }, EnableGitHubContext = true)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = true)] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;
    [GitVersion] readonly GitVersion GitVersion;
    [CI] readonly GitHubActions GitHubActions;

    AbsolutePath OutputDirectory => RootDirectory / "output";

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Produces(OutputDirectory / "*.nupkg", OutputDirectory / "*.tgz", OutputDirectory / "*.zip")
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetVersion(GitVersion.NuGetVersionV2)
                .EnableNoBuild()
                .SetOutputDirectory(OutputDirectory));

            var runtimes = Solution.Megasware128_HalfLifeLauncher.GetRuntimeIdentifiers().Where(r => !string.IsNullOrEmpty(r));

            DotNetPublish(s => s
                .SetProject(Solution.Megasware128_HalfLifeLauncher)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion)
                .SetVersion(GitVersion.NuGetVersionV2)
                .EnableNoRestore()
                .EnableSelfContained()
                .CombineWith(runtimes, (s, r) => s
                    .SetRuntime(r)
                    .SetOutput(TemporaryDirectory / "runtimes" / r)), 3);

            var windowsRuntimes = runtimes.Where(r => r.StartsWith("win")).ToArray();
            var otherRuntimes = runtimes.Except(windowsRuntimes).ToArray();

            var tasks = new List<Task>();

            foreach (var runtime in windowsRuntimes)
            {
                tasks.Add(Task.Run(() =>
                {
                    var runtimeDirectory = TemporaryDirectory / "runtimes" / runtime;
                    var runtimeDirectoryZip = OutputDirectory / $"{Solution.Megasware128_HalfLifeLauncher.Name}.{runtime}.zip";

                    Compress(runtimeDirectory, runtimeDirectoryZip);
                }));
            }

            foreach (var runtime in otherRuntimes)
            {
                tasks.Add(Task.Run(() =>
                {
                    var runtimeDirectory = TemporaryDirectory / "runtimes" / runtime;
                    var runtimeDirectoryTarGz = OutputDirectory / $"{Solution.Megasware128_HalfLifeLauncher.Name}.{runtime}.tgz";

                    Compress(runtimeDirectory, runtimeDirectoryTarGz);
                }));
            }

            return Task.WhenAll(tasks);
        });

    Target Publish => _ => _
        .DependsOn(Pack)
        .Executes(async () =>
        {
            GitHubTasks.GitHubClient = new GitHubClient(new ProductHeaderValue(Solution.Megasware128_HalfLifeLauncher.Name))
            {
                Credentials = new Credentials(GitHubActions.Token)
            };

            var tag = Git("describe --tags").First().Text;

            var release = await GitHubTasks.GitHubClient.Repository.Release.Get(GitRepository.GetGitHubOwner(), GitRepository.GetGitHubName(), tag);

            if (release is not null)
            {
                var output = new DirectoryInfo(OutputDirectory);

                var files = output.GetFiles("*.nupkg")
                    .Concat(output.GetFiles("*.tgz")
                    .Concat(output.GetFiles("*.zip")));

                foreach (var file in files)
                {
                    var contentType = file.Extension switch
                    {
                        ".nupkg" => "application/zip",
                        ".tgz" => "application/gzip",
                        ".zip" => "application/zip",
                        _ => throw new InvalidOperationException($"Unknown file extension: {file.Extension}")
                    };

                    using var stream = file.OpenRead();

                    var asset = new ReleaseAssetUpload(file.Name, contentType, stream, null);

                    await GitHubTasks.GitHubClient.Repository.Release.UploadAsset(release, asset);
                }
            }
        });

    Target Install => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            DotNetToolInstall(s => s
                .SetPackageName("Megasware128.HalfLifeLauncher")
                .EnableGlobal()
                .AddSources(OutputDirectory)
                .SetVersion(GitVersion.NuGetVersionV2));
        });

    Target Update => _ => _
        .DependsOn(Pack)
        .Executes(() =>
        {
            DotNetToolUpdate(s => s
                .SetPackageName("Megasware128.HalfLifeLauncher")
                .EnableGlobal()
                .AddSources(OutputDirectory)
                .SetVersion(GitVersion.NuGetVersionV2));
        });
}
