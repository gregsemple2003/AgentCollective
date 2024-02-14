using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;

namespace BizDevAgent.Flow
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("EvaluateResearchCustomization")]
    public class EvaluateResearchCustomization : AgentGoalCustomization
    {
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly VisualStudioService _visualStudioService;
        private readonly JobRunner _jobRunner;

        public EvaluateResearchCustomization(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner) 
        { 
            _codeAnalysisService = codeAnalysisService;
            _visualStudioService = visualStudioService;
            _jobRunner = jobRunner;
        }

        public override Task ProcessResponse(string response, AgentState agentState, IResponseParser languageModelParser)
        {
            return Task.CompletedTask;
        }

    }
}
