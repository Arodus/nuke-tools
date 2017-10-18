using System;
using System.Linq;
using Nuke.Common.Git;
using Nuke.Core;
using static Nuke.Git.GitTasks;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;
using static ReferenceDownload;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.References);

    string MetadataDirectory => SolutionDirectory / "metadata";
    string ReferencesDirectory => (AbsolutePath) MetadataDirectory / "references";
    [GitRepository] readonly GitRepository GitRepository;

    [Parameter(
        "GitHub access token. Can be obtained at https://github.com/settings/tokens. Required OAuth Scope: Repo.")]
    readonly string GitHubAccessToken;


    Target Clean => _ => _
        .Executes(() => EnsureCleanDirectory(ReferencesDirectory));

    Target References => _ => _
        .DependsOn(Clean)
        .Executes(() => DownloadReferences(MetadataDirectory, ReferencesDirectory));

    Target PullRequest => _ => _
        .Requires(() => GitHubAccessToken != null, () => GitRepository != null)
        .DependsOn(References)
        .Executes(() =>
        {
            var changedReferences = ReferenceCommit.GetChangedFiles(ReferencesDirectory);
            if (!changedReferences.Any())
            {
                Logger.Info("The references are already up to date.");
                return;
            }

            ReferenceCommit.Add(ReferencesDirectory);
            ReferenceCommit.Commit("Update references.", ReferencesDirectory);
            GitPush();
            ReferencePullRequest.CreatePullRequestIfNonExists(GitRepository.Owner,
                GitRepository.Name, GitRepository.Branch, GitHubAccessToken);
        });
}