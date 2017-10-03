using System;
using System.Linq;
using Nuke.Common.Git;
using Nuke.Core;
using static Nuke.Common.Tools.Git.GitTasks;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;
using static ReferenceDownload;
using static ReferencePullRequest;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.References);
    
    [GitRepository] readonly GitRepository GitRepository;
    [Parameter("GitHub access token.")] readonly string GitHubAccessToken;

    string MetadataDirectory => SolutionDirectory / "metadata";
    string ReferencesDirectory => (AbsolutePath) MetadataDirectory / "references";

    Target Clean => _ => _
        .Executes(() => EnsureCleanDirectory(ReferencesDirectory));

    Target References => _ => _
        .DependsOn(Clean)
        .Executes(() => DownloadReferences(MetadataDirectory, ReferencesDirectory));

    Target PullRequest => _ => _
        .DependsOn(References)
        .OnlyWhen(GitHasUncommitedChanges)
        .Executes(() =>
        {
            Git($"add {ReferencesDirectory}");
            Git($"commit -m \"Update references.\"");
            Git($"push");
            
            CreatePullRequestIfNeeded(GitRepository, GitHubAccessToken);
        });
}