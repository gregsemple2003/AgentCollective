using System.Linq;
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
    public class ModifyRepositoryAgentGoal : ProgrammerAgentGoal
    {
        private readonly RepositoryQuerySession _repositoryQuerySession;
        private readonly IServiceProvider _serviceProvider;

        public ModifyRepositoryAgentGoal(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner, IServiceProvider serviceProvider, AgentGoalSpec spec)
            : base(spec)
        { 
            _repositoryQuerySession = ProgrammerContext.Current?.TargetRepositoryQuerySession;
            _serviceProvider = serviceProvider;
        }

        internal override Task PreTransition(AgentState agentState) 
        {
            // If all children are done, then we are done this coding step
            var allChildrenDone = !HasAnyChildren(child => !child.IsDone());
            if (allChildrenDone)
            {
                // Increment coding step
                var programmerShortTermMemory = agentState.ShortTermMemory as ProgrammerShortTermMemory;
                programmerShortTermMemory.CodingTaskStep++;
                if (programmerShortTermMemory.CodingTaskStep > programmerShortTermMemory.CodingTasks.Steps.Count) // 1-based step index check
                {
                    MarkDone(); // we completed the last step
                }

                // Re-create children
                RemoveChildren(child => true);
                var newModifyRepositoryGoal = Spec.InstantiateGraph(_serviceProvider);
                foreach(var newChild in newModifyRepositoryGoal.Children)
                {
                    AddChild(newChild);
                }
            }

            
            return Task.CompletedTask;
        }

        protected override void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState)
        {
            base.PopulatePromptCustom(promptContext, agentState);

            if (!agentState.TryGetGoal(out var currentGoal)) return;
            var programmerShortTermMemory = agentState.ShortTermMemory as ProgrammerShortTermMemory;
            promptContext.AdditionalData["CodingTaskStep"] = programmerShortTermMemory.CodingTaskStep;
        }
    }
}
