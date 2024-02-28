using Agent.Core;
using FluentResults;

namespace Agent.Programmer
{
    [TypeId("ProgrammerAgentGoal")]
    public class ProgrammerAgentGoal : AgentGoal
    {
        public ProgrammerAgentGoal(AgentGoalSpec spec)
            : base(spec)
        {
        }

        protected override void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState)
        {
            if (!agentState.TryGetGoal(out var currentGoal)) return;
            var programmerAgentState = (agentState as ProgrammerAgentState);
            var programmerShortTermMemory = agentState.ShortTermMemory as ProgrammerShortTermMemory;

            // Refresh working set since repo file contents may have changed
            programmerAgentState.UpdateWorkingSet().GetAwaiter().ToResult();
            promptContext.AdditionalData["WorkingSet"] = programmerShortTermMemory.WorkingSet;
        }
    }
}
