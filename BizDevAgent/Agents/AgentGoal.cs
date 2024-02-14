using BizDevAgent.DataStore;
using BizDevAgent.Services;

namespace BizDevAgent.Agents
{
    /// <summary>
    /// Allows either a direct description, or can use the template substitution engine based on context from a file.
    /// </summary>
    public class OverridableText
    { 
        public string Text { get; set; }
        public PromptAsset Template { get; set; }

        public string Key
        {
            get
            {
                if (Template == null) throw new InvalidOperationException("Use of a key on text requires a template.");

                return Template.Key;
            }
        }

        public void Bind(AgentPromptContext promptContext)
        {
            if (Template != null)
            {
                Text = Template.Evaluate(promptContext);
            }
            else if (Text == null)
            {
                throw new InvalidOperationException("Text description must either have a template or valid text.");
            }
        }
    }

    /// <summary>
    /// Represents a task in the goal hierarchy of an agent.  Helps to define agent state and behavior in natural language:
    ///     - The subgoals of this goal, some or all of which can be chosen to complete this goal.
    ///     - The goal stack, which is a top-down series of descriptions about the task the agent is currently focused on.
    ///     - The completion conditions, which must be true before this goal can be popped from the stack.
    /// </summary>
    [TypeId("AgentGoal")]
    public class AgentGoal : JsonAsset
    {
        /// <summary>
        /// A short title describing what this goal is supposed to accomplish.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// After executing this state's prompt and response logic, pop it return control to parent goal.
        /// </summary>
        public bool AutoComplete {  get; set; }

        /// <summary>
        /// Subgoals which must be compled in order for the parent to be complete.
        /// </summary>
        public List<AgentGoal> RequiredSubgoals { get; set; } = new List<AgentGoal>();

        /// <summary>
        /// Subgoals which are offered as an "action" choice in the prompt.  At least one must be selected.
        /// </summary>
        public List<AgentGoal> OptionalSubgoals { get; set; } = new List<AgentGoal>();

        /// <summary>
        /// The text which will be displayed when this goal is listed as an optional subgoal.
        /// </summary>
        public OverridableText OptionDescription { get; set; }

        /// <summary>
        /// When this goal is on the agent's goal stack, a short description indicating the present activity the agent should be focused on.
        /// The goals will be printed from top down.
        /// </summary>
        public OverridableText StackDescription { get; set; }

        /// <summary>
        /// A short description indicating when this goal is considered finished.
        /// </summary>
        public OverridableText DoneDescription { get; set; }

        public AgentGoalCustomization Customization { get; set; }

        public string TokenPrefix => "@";

        public bool RequiresDoneDescription()
        {
            if (AutoComplete) return false;

            return true;
        }

        public void ProcessResponse(string response, AgentState agentState, IResponseParser languageModelParser)
        {
            Customization?.ProcessResponse(response, agentState, languageModelParser);
        }

        public AgentGoal()
        {
        }

        public AgentGoal(string title)
        {
            Title = title;
        }
    }
}
