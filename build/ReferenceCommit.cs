using System.Collections.Generic;
using System.Linq;
using Nuke.Common.Tools;
using Nuke.Core.Tooling;

public static class ReferenceCommit
{
    static readonly string GitPath = ToolPathResolver.GetPathExecutable("git");


    public static void Add(string subDirectory = null)
    {
        ProcessTasks.StartProcess(GitPath, "add" + (subDirectory == null ? "" : $" {subDirectory}"))
            .AssertZeroExitCode();
    }

    public static void Commit(string message, string subDirectory = null)
    {
        ProcessTasks.StartProcess(GitPath, $"git commit{subDirectory ?? ""} -m {message}")
            .AssertZeroExitCode();
    }

    public static IEnumerable<string> GetChangedFiles(string subDirectory = null)
    {
        var countProcess = ProcessTasks.StartProcess(GitPath,
            "diff  --name-only" + (subDirectory == null ? "" : $" {subDirectory}"), redirectOutput: true);
        countProcess.AssertZeroExitCode();
        return countProcess.Output.Where(o => o.Type == OutputType.Std).Select(o => o.Text);
    }
}