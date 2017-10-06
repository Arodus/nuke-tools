using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nuke.Common.Tools;
using Nuke.Core;
using Nuke.Core.Tooling;


public static class GitCommit
{
         static readonly string GitPath = ToolPathResolver.GetPathExecutable("git");

        public static void CommitIfChanged(string message, string subDirectory = null)
        {
            ProcessTasks.StartProcess(GitPath, "add" + (subDirectory == null ? "" : $" {subDirectory}")).AssertZeroExitCode();
            var countProcess = ProcessTasks.StartProcess(GitPath, "diff --cached --numstat" + (subDirectory == null ? "" : $" {subDirectory}"), redirectOutput: true);
            countProcess.AssertZeroExitCode();
            var changedFiles = countProcess.Output.Any(o => o.Type == OutputType.Std);
            if (!changedFiles)
            {
                Logger.Info("No files changed");
            }
            else
            {
                ProcessTasks.StartProcess(GitPath,$"commit -m \"{message}\"").AssertZeroExitCode();
            }

        }
    }

