using BizDevAgent.Agents;
using System.Threading.Tasks;

namespace BizDevAgent.Jobs
{
    public class CodeResearchJob_YourGuidHere : Job
    {
        private readonly CodeQueryAgent _codeQueryAgent;

        public CodeResearchJob_YourGuidHere(CodeQueryAgent codeQueryAgent)
        {
            _codeQueryAgent = codeQueryAgent;
        }

        public async override Task Run()
        {
            // Your calls to _codeQueryAgent go here
        }
    }
}
