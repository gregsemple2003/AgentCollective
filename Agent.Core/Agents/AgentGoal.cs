using Microsoft.Extensions.DependencyInjection;

namespace Agent.Core
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
    public class AgentGoal
    {
        /// <summary>
        /// How many times we have requested a completion from this state, not including children.
        /// </summary>
        public int PromptCount { get; private set; }

        public AgentGoalSpec Spec { get; }
        public List<AgentGoal> Children { get; private set; }
        public AgentGoal Parent { get; private set; }

        private bool _done;

        public AgentGoal(AgentGoalSpec spec)
        {
            Children = new List<AgentGoal>();

            Spec = spec;
        }

        /// <summary>
        /// Whether this state is considered complete.
        /// </summary>
        /// <returns></returns>
        public bool IsDone()
        {
            return _done;
        }

        public bool HasAnyChildren(Func<AgentGoal, bool> predicate)
        {
            foreach (var child in Children)
            {
                if (predicate(child))
                {
                    return true;
                }
            }

            return false;
        }

        public void AddChild(AgentGoal goal)
        {
            if (goal == null) throw new ArgumentNullException(nameof(goal), "Child goal cannot be null.");

            goal.SetParent(this);
            Children.Add(goal);
        }

        public void RemoveChildren(Func<AgentGoal, bool> predicate)
        {
            for (int childIndex = Children.Count - 1; childIndex >= 0; childIndex--)
            {
                AgentGoal child = Children[childIndex];
                if (predicate(child))
                {
                    Children.RemoveAt(childIndex);
                }
            }
        }

        public void MarkDone()
        {
            if (!_done)
            {
                _done = true;
            }
        }

        public void SetParent(AgentGoal parent)
        {
            Parent = parent;
        }

        /// <summary>
        /// Whether this goal needs a completion, which is a prompt and response from the LLM.
        /// </summary>
        /// <returns></returns>
        public bool ShouldRequestPrompt(AgentState agentState)
        {
            if (PromptCount >= Spec.PromptCountLimit) return false;

            bool customizationWantsPrompt = Spec.Customization != null && Spec.Customization.ShouldRequestPrompt(agentState);
            customizationWantsPrompt |= ShouldRequestPromptCustom(agentState);
            bool shouldPromptOptions = !IsDone() && Spec.OptionalSubgoals.Count > 0;
            return customizationWantsPrompt || shouldPromptOptions;
        }

        public void IncrementPromptCount(int delta)
        {
            PromptCount += delta;

            if (PromptCount >= Spec.PromptCountLimit)
            {
                MarkDone();
            }
        }

        public async Task PrePrompt(AgentState agentState)
        {
            PrePromptCustom(agentState);
            if (Spec.Customization != null)
            {
                await Spec.Customization.PrePrompt(agentState);
            }
        }

        public void PopulatePrompt(AgentPromptContext promptContext, AgentState agentState)
        {
            Spec.Customization?.PopulatePrompt(promptContext, agentState);
            PopulatePromptCustom(promptContext, agentState);
        }

        public bool RequiresDoneDescription()
        {
            if (Spec.IsAutoComplete) return false;

            return true;
        }

        public async Task ProcessResponse(string prompt, string response, AgentState agentState, ILanguageParser languageParser)
        {
            if (Spec.Customization != null)
            {
                await Spec.Customization.ProcessResponse(prompt, response, agentState, languageParser);
            }
            await ProcessResponseCustom(prompt, response, agentState, languageParser);
        }

        protected virtual bool ShouldRequestPromptCustom(AgentState agentState) { return false; }
        protected virtual Task PrePromptCustom(AgentState agentState) { return Task.CompletedTask; }
        protected virtual void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState) { }
        protected virtual Task ProcessResponseCustom(string prompt, string response, AgentState agentState, ILanguageParser languageModelParser) { return Task.CompletedTask; }
        public virtual void OnEnter() { }
        public virtual async Task PreTransition(AgentState agentState) { }
    }

    /// <summary>
    /// When the goal determines it is complete.
    /// </summary>
    public enum CompletionMethod
    {
        WhenChildrenDone,     // Goal is done when all children are done.
        WhenMarkedDone            // Goal is done when manually marked done.
    }

    /// <summary>
    /// Represents a task in the goal hierarchy of an agent.  Helps to define agent state and behavior in natural language:
    ///     - The subgoals of this goal, some or all of which can be chosen to complete this goal.
    ///     - The goal stack, which is a top-down series of descriptions about the task the agent is currently focused on.
    ///     - The completion conditions, which must be true before this goal can be popped from the stack.
    /// </summary>
    [TypeId("AgentGoalSpec")]
    public class AgentGoalSpec : JsonAsset
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
        public List<AgentGoalSpec> RequiredSubgoals { get; set; } = new List<AgentGoalSpec>();

        /// <summary>
        /// Subgoals which are offered as an "action" choice in the prompt.  At least one must be selected.
        /// </summary>
        public List<AgentGoalSpec> OptionalSubgoals { get; set; } = new List<AgentGoalSpec>();

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
        /// Information added to the end of the prompt, since information at the beginning and end of the prompt is prioritized.
        /// Text in the middle of the prompt may be ignored, due to multi-head attention not properly prioritizing it.
        /// </summary>
        public OverridableText ReminderDescription { get; set; }

        /// <summary>
        /// A short description indicating when this goal is considered finished.
        /// </summary>
        public OverridableText DoneDescription { get; set; }

        /// <summary>
        /// The type id to use if a subclass of AgentGoal is desired.
        /// </summary>
        public string InstanceTypeId {  get; set; }

        // TODO gsemple: remove this and migrate to InstanceTypeId
        public AgentGoalCustomization Customization { get; set; }

        public CompletionMethod CompletionMethod { get; set; }

        public string TokenPrefix => "@";
        public int PromptCountLimit => 3;

        public AgentGoalSpec()
        {
            CompletionMethod = CompletionMethod.WhenChildrenDone;
        }

        public AgentGoalSpec(string title)
        {
            Title = title;
        }

        public AgentGoal InstantiateGraph(IServiceProvider serviceProvider)
        {
            var rootGoal = InstantiateNode(serviceProvider);
            InstantiateChildren(rootGoal, this, serviceProvider);
            return rootGoal;
        }

        public AgentGoal InstantiateNode(IServiceProvider serviceProvider)
        {
            if (InstanceTypeId?.Length > 0)
            {
                var instanceType = TypedJsonConverter.GetType(InstanceTypeId);
                var instance = (AgentGoal)ActivatorUtilities.CreateInstance(serviceProvider, instanceType, this);
                return instance;
            }

            return new AgentGoal(this);
        }

        private void InstantiateChildren(AgentGoal parentGoal, AgentGoalSpec spec, IServiceProvider serviceProvider)
        {
            foreach (var childSpec in spec.RequiredSubgoals)
            {
                // Create a new AgentGoal from each child spec.
                var childGoal = childSpec.InstantiateNode(serviceProvider);
                childGoal.SetParent(parentGoal);
                parentGoal.Children.Add(childGoal);
                InstantiateChildren(childGoal, childSpec, serviceProvider);
            }
        }
    }
}
