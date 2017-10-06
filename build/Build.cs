using System;
using System.Linq;
using Nuke.Common.Tools;
using Nuke.Core;
using Nuke.Core.Tooling;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;
using static ReferenceDownload;

class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Commit);

    string MetadataDirectory => SolutionDirectory / "metadata";
    string ReferencesDirectory => (AbsolutePath) MetadataDirectory / "references";

    Target Clean => _ => _
        .Executes(() => EnsureCleanDirectory(ReferencesDirectory));

    Target References => _ => _
        .DependsOn(Clean)
        .Executes(() => DownloadReferences(MetadataDirectory, ReferencesDirectory));


   
    Target Commit => _ => _
        .DependsOn(Clean)
        .Executes(() => ProcessTasks.StartProcess(ToolPathResolver.GetPathExecutable("git"), "pull origin master").AssertZeroExitCode())
        .Executes(References)
        .Executes(() => GitCommit.CommitIfChanged("Update references.", ReferencesDirectory))
        .Executes(() => ProcessTasks.StartProcess(ToolPathResolver.GetPathExecutable("git"), "push origin").AssertZeroExitCode());
}