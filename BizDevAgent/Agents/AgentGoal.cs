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
    /// Holds non-static data about a goal.  This is embedded in the AgentGoal to keep the graph instantiation
    /// logic simple.  If we ever need non-static subgoals, we might consider revising this approach.
    /// </summary>
    internal class AgentGoalState
    { 
        /// <summary>
        /// Whether this goal is complete, and can be popped off the goal stack.
        /// </summary>
        public bool IsDone { get; set; }

        /// <summary>
        /// How many times we have requested a completion from this state, not including children.
        /// </summary>
        public int CompletionCount { get; set; }

        public void Reset()
        {
            IsDone = false;
            CompletionCount = 0;
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
        public bool IsAutoComplete 
        {
            get => OptionalSubgoals.Count == 0;
        }

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

        private readonly AgentGoalState _state;

        public string TokenPrefix => "@";
        public int CompletionsLimit => 3;

        public AgentGoal()
        {
            _state = new AgentGoalState();
        }

        public AgentGoal(string title)
        {
            Title = title;
        }

        /// <summary>
        /// Whether this state is considered complete.
        /// </summary>
        /// <returns></returns>
        public bool IsDone()
        {
            return _state.IsDone;
        }

        public void MarkDone()
        {
            if (!_state.IsDone)
            {
                _state.IsDone = true;
            }
        }

        public void Reset()
        {
            _state.Reset();
        }

        public void IncrementCompletionCount(int delta)
        {
            _state.CompletionCount += delta;

            if (_state.CompletionCount >= CompletionsLimit)
            {
                _state.IsDone = true;
            }
        }

        /// <summary>
        /// Whether this goal needs a completion, which is a "brain tick" from the LLM.
        /// </summary>
        /// <returns></returns>
        public bool ShouldRequestCompletion()
        {
            if (_state.CompletionCount >= CompletionsLimit) return false;

            bool customizationWantsCompletion = Customization != null && Customization.ShouldRequestCompletion();
            return customizationWantsCompletion || OptionalSubgoals.Count > 0;
        }

        public bool RequiresDoneDescription()
        {
            if (IsAutoComplete) return false;

            return true;
        }

        public async Task ProcessResponse(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            if (Customization != null)
            {
                await Customization.ProcessResponse(prompt, response, agentState, languageModelParser);
            }
        }
    }
}
