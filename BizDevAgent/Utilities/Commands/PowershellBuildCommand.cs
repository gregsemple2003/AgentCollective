using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.Utilities.Commands
{
    /// <summary>
    /// An agent that runs powershell scripts.  Can be run as a build command.
    /// </summary>
    public class PowershellBuildCommand : IBuildCommand
    {
        public string ScriptPath { get; set; }

        public PowershellBuildCommand()
        {
        }

        public async Task<Result<BuildResult>> Build(string rootRepoPath)
        {
            var result = new BuildResult();
            await Task.Delay(1);
            return result;
        }

    }
}
