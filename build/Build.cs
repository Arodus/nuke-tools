using System;
using System.Linq;
using Nuke.Common.Git;
using Nuke.Common.Tools;
using Nuke.Core;
using Nuke.Core.Tooling;
using Nuke.Git;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;
using static ReferenceDownload;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.References);

    string MetadataDirectory => SolutionDirectory / "metadata";
    string ReferencesDirectory => (AbsolutePath) MetadataDirectory / "references";
    [GitRepository] readonly GitRepository GitRepository;

    [Parameter("GitHub access token. Can be obtained at https://github.com/settings/tokens. Required OAuth Scope: Repo")]
    readonly string GitHubAccessToken;


    Target Clean => _ => _
        .Executes(() => EnsureCleanDirectory(ReferencesDirectory));


    Target References => _ => _
        .DependsOn(Clean)
        .Executes(() => DownloadReferences(MetadataDirectory, ReferencesDirectory));

    //Todo rename
    Target PullRequest => _ => _
        .Requires(() => GitHubAccessToken)
        .Requires(() => GitRepository)
        .Executes(References)
        .Executes(() => ProcessTasks.StartProcess(ToolPathResolver.GetPathExecutable("git"), "status"))
        .Executes(() =>
        {
            if (!ReferenceCommit.CommitIfChanged("Update references.", ReferencesDirectory)) return;
            GitTasks.GitPush();
            ReferencePullRequest.CreatePullRequestIfNonExists(GitRepository.Owner,
                GitRepository.Name, GitRepository.Branch,GitHubAccessToken);
        });
}