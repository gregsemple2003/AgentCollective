using BizDevAgent.DataStore;
using System.Runtime.Serialization;

namespace BizDevAgent.Agents
{
    [TypeId("AgentGoal")]
    public class AgentGoal : JsonAsset
    {
        public string Title { get; set; }

        /// <summary>
        /// When this goal is on the agent's goal stack, a short description indicating the present activity the agent should be focused on.
        /// The goals will be printed from top down.
        /// </summary>
        public string StackDescription { get; set; }
        public PromptAsset StackBuilder { get; set; }

        /// <summary>
        /// Subgoals which must be compled in order for the parent to be complete.
        /// </summary>
        public List<AgentGoal> RequiredSubgoals { get; set; } = new List<AgentGoal>();

        /// <summary>
        /// Subgoals which are offered as an "action" choice in the prompt.  At least one must be selected.
        /// </summary>
        public List<AgentGoal> OptionalSubgoals { get; set; } = new List<AgentGoal>();

        /// <summary>
        /// The option which will be displayed when this goal is listed as an optional subgoal.
        /// </summary>
        public string OptionDescription { get; set; }
        public PromptAsset OptionBuilder { get; set; }

        public string TokenPrefix => "@";

        public AgentGoal()
        {
        }

        public AgentGoal(string title)
        {
            Title = title;
        }
    }
}
