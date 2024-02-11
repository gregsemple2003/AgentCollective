﻿using BizDevAgent.Agents;
using System.Threading.Tasks;

namespace BizDevAgent.Jobs
{
    public class CodeResearchJob_YourGuidHere : Job
    {
        private readonly CodeQueryAgent _codeQueryAgent;
        private readonly string _localRepoPath;

        public CodeResearchJob_YourGuidHere(CodeQueryAgent codeQueryAgent, string localRepoPath)
        {
            _codeQueryAgent = codeQueryAgent;
            _localRepoPath = localRepoPath;
        }

        public async override Task Run()
        {
            var codeQuerySession = _codeQueryAgent.CreateSession(_localRepoPath);

            // your queries go here
        }
    }
}
