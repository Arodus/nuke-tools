﻿using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.MSBuild;
using Nuke.Core;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;
using static Nuke.Core.EnvironmentInfo;

class Build : NukeBuild
{
    // Auto-injection fields:
    //  - [GitVersion] must have 'GitVersion.CommandLine' referenced
    //  - [GitRepository] parses the origin from git config
    //  - [Parameter] retrieves its value from command-line arguments or environment variables
    //
    //[GitVersion] readonly GitVersion GitVersion;
    //[GitRepository] readonly GitRepository GitRepository;
    //[Parameter] readonly string MyGetApiKey;
    [GitRepository] readonly GitRepository GitRepository;

    [Parameter] string GitUsername;

    [Parameter] string GitEmail;
    // This is the application entry point for the build.
    // It also defines the default target to execute.
    public static int Main () => Execute<Build>(x => x.DownloadReferences);


    Target Clean => _ => _
            .Executes(() => EnsureCleanDirectory(SolutionDirectory/"references"))
            .Executes(() => EnsureCleanDirectory(OutputDirectory));


    Target DownloadReferences => _ => _
        .DependsOn(Clean)
        .Executes(() => ReferenceHelper.DownloadReferences(Instance.SolutionDirectory / "metadata",Instance.SolutionDirectory / "references"));

}
