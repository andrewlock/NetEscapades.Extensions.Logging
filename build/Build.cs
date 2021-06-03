using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
[GitHubActions("BuildAndPack",
    GitHubActionsImage.UbuntuLatest,
    GitHubActionsImage.WindowsLatest,
    GitHubActionsImage.MacOsLatest,
    ImportGitHubTokenAs = nameof(GithubToken),
    OnPushBranches = new[] {"master", "main"},
    OnPullRequestBranches = new[] {"*"},
    ImportSecrets = new[] {nameof(NuGetToken)},
    InvokedTargets = new[] {nameof(Test), nameof(Pack), nameof(PushToGitHubPackages), nameof(PushToNuGet)}
)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Test, x => x.Pack);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "test";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    readonly string GithubToken;
    readonly string NuGetToken;
    const string GithubPackagesUrl = "https://nuget.pkg.github.com/andrewlock/index.json";
    bool IsTag => GitHubActions.Instance?.GitHubRef?.StartsWith("refs/tags/") ?? false;
    bool IsPullRequest => !string.IsNullOrEmpty(GitHubActions.Instance?.GitHubHeadRef);

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
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
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .After(Test)
        .Produces(ArtifactsDirectory)
        .Executes(() =>
        {
            var projects = SourceDirectory.GlobFiles("**/*.csproj");
            DotNetPack(s => s
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableNoBuild()
                .EnableNoRestore()
                .CombineWith(projects, (x, project) => x
                    .SetProject(Solution.GetProject(project))));
        });

    Target PushToGitHubPackages => _ => _
        .DependsOn(Compile)
        .OnlyWhenStatic(() => !IsPullRequest && IsServerBuild && IsWin)
        .Requires(() => GithubToken)
        .After(Pack)
        .Executes(() =>
        {
            var githubSource = "github";
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");

            DotNetNuGetAddSource(s => s
                .SetSource(GithubPackagesUrl)
                .SetUsername("andrewlock")
                .SetPassword(GithubToken)
                .EnableStorePasswordInClearText()
                .SetName(githubSource));

            DotNetNuGetPush(s => s
                .SetSource(githubSource)
                .EnableSkipDuplicate()
                .CombineWith(packages, (x, package) => x
                    .SetTargetPath(package)));
        });

    Target PushToNuGet => _ => _
        .DependsOn(Compile)
        .OnlyWhenStatic(() => IsTag && IsServerBuild && IsWin)
        .Requires(() => NuGetToken)
        .After(Pack)
        .Executes(() =>
        {
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");
            DotNetNuGetPush(s => s
                .SetApiKey(NuGetToken)
                .EnableSkipDuplicate()
                .CombineWith(packages, (x, package) => x
                    .SetTargetPath(package)));
        });
}