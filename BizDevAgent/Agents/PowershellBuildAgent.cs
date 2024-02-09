using BizDevAgent.Agents;
using FluentResults;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.Agents
{
    /// <summary>
    /// An agent that runs powershell scripts.  Can be run as a build command.
    /// </summary>
    public class PowershellBuildAgent : IBuildAgent
    {
        public string ScriptPath { get; set; }

        public PowershellBuildAgent() 
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
