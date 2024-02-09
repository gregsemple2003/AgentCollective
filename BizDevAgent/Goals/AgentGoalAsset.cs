using BizDevAgent.DataStore;

namespace BizDevAgent.Goals
{
    public class AgentGoalAsset : JsonAsset
    {
        public string Description { get; set; }
        public List<AgentGoalAsset> SubGoals { get; set; } = new List<AgentGoalAsset>();
        public PromptAsset PromptBuilder { get; set; }

        public bool Completed { get; set; }

        /// <summary>
        /// The minimal set of actions that are available to this agent when this is the immediate goal.
        /// </summary>
        public List<AgentActionAsset> BaselineActions { get; set; }

        public AgentGoalAsset(string description)
        {
            Description = description;
        }
    }
}
