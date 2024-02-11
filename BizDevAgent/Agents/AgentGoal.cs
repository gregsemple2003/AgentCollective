using BizDevAgent.DataStore;

namespace BizDevAgent.Agents
{
    [TypeId("AgentGoal")]
    public class AgentGoal : JsonAsset
    {
        public string Title { get; set; }
        public List<AgentGoal> SubGoals { get; set; } = new List<AgentGoal>();
        public PromptAsset PromptBuilder { get; set; }
        public string PromptTemplatePath { get; set; }

        /// <summary>
        /// The minimal set of actions that are available to this agent when this is the immediate goal.
        /// </summary>
        public List<AgentAction> BaselineActions { get; set; }

        public AgentGoal()
        {
        }

        public AgentGoal(string title)
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
