using BizDevAgent.Services;
using System.Threading.Tasks;

namespace BizDevAgent.Jobs
{
    public class CodeResearchJob_YourGuidHere : Job
    {
        private readonly CodeQueryService _codeQueryService;
        private readonly string _localRepoPath;

        public CodeResearchJob_YourGuidHere(CodeQueryService codeQueryService, string localRepoPath)
        {
            _codeQueryService = codeQueryService;
            _localRepoPath = localRepoPath;
        }

        public async override Task Run()
        {
            var codeQuerySession = _codeQueryService.CreateSession(_localRepoPath);

            // your queries go here
            await codeQuerySession.PrintFileSkeleton("SomeFile.cs");
        }
    }
}
