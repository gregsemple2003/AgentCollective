using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;

namespace BizDevAgent.Flow
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("RefineStepPlanAgentGoal")]
    public class RefineStepPlanAgentGoal : ProgrammerAgentGoal
    {
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;

        public RefineStepPlanAgentGoal(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner, IServiceProvider serviceProvider, AgentGoalSpec spec)
            : base(spec)
        {
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
        }

        protected override bool ShouldRequestPromptCustom(AgentState agentState)
        {
            return true;
        }

        protected override void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState)
        {
            var programmerAgentState = (agentState as ProgrammerAgentState);

            var requiredMethodAttributes = new List<string>() { "AgentApi" };
            var agentApiSkeleton = programmerAgentState.GenerateAgentApiSkeleton(requiredMethodAttributes);
            var agentApiSample = _selfRepositoryQuerySession.FindFileInRepo($"{nameof(RepositoryQueryJob)}.cs");
            promptContext.AdditionalData["AgentApiSkeleton"] = agentApiSkeleton;
            promptContext.AdditionalData["AgentApiSample"] = agentApiSample.Contents;
        }
    }
}
