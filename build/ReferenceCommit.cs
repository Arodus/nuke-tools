using System.Linq;
using Nuke.Common.Tools;
using Nuke.Core;
using Nuke.Core.Tooling;

public static class ReferenceCommit
{
    static readonly string GitPath = ToolPathResolver.GetPathExecutable("git");

    public static bool CommitIfChanged(string message, string subDirectory = null)
    {
        ProcessTasks.StartProcess(GitPath, "add" + (subDirectory == null ? "" : $" {subDirectory}"))
            .AssertZeroExitCode();
        var countProcess = ProcessTasks.StartProcess(GitPath,
            "diff --cached --numstat" + (subDirectory == null ? "" : $" {subDirectory}"), redirectOutput: true);
        countProcess.AssertZeroExitCode();
        var changedFiles = countProcess.Output.Any(o => o.Type == OutputType.Std);
        if (!changedFiles)
        {
            Logger.Info("The references are already up to date.");
            return false;
        }
        ProcessTasks.StartProcess(GitPath, $"commit -m \"{message}\"").AssertZeroExitCode();
        return true;
    }
}

