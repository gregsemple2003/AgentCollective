using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;

namespace BizDevAgent.Flow
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("ModifyRepositoryAgentGoal")]
    public class ModifyRepositoryAgentGoal : AgentGoal
    {
        private readonly RepositoryQuerySession _repositoryQuerySession;

        public ModifyRepositoryAgentGoal(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner, IServiceProvider serviceProvider, AgentGoalSpec spec)
            : base(spec)
        { 
            _repositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
        }

        internal override Task PreTransition(AgentState agentState) 
        {
            // If all children are done, then we are done this coding step
            var allChildrenDone = !HasAnyChildren(child => !child.IsDone());
            if (allChildrenDone)
            {
                var programmerShortTermMemory = agentState.ShortTermMemory as ProgrammerShortTermMemory;
                programmerShortTermMemory.CodingTaskStep++;
                if (programmerShortTermMemory.CodingTaskStep > programmerShortTermMemory.CodingTasks.Steps.Count) // 1-based step index check
                {
                    MarkDone(); // we completed the last step
                }
            }

            return Task.CompletedTask;
        }
    }
}
