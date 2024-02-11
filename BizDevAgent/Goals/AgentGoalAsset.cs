using BizDevAgent.DataStore;

namespace BizDevAgent.Goals
{
    [TypeId("AgentGoal")]
    public class AgentGoalAsset : JsonAsset
    {
        public string Title { get; set; }
        public List<AgentGoalAsset> SubGoals { get; set; } = new List<AgentGoalAsset>();
        public PromptAsset PromptBuilder { get; set; }
        public string PromptTemplatePath { get; set; }

        /// <summary>
        /// The minimal set of actions that are available to this agent when this is the immediate goal.
        /// </summary>
        public List<AgentActionAsset> BaselineActions { get; set; }

        public AgentGoalAsset()
        {
        }

        public AgentGoalAsset(string title)
        {
            Title = title;
        }

        internal override void PostLoad(AssetDataStore assetDataStore)
        {
            if (!string.IsNullOrEmpty(PromptTemplatePath))
            {
                PromptBuilder = assetDataStore.GetHardRef<PromptAsset>(PromptTemplatePath);
            }
        }
    }
}
