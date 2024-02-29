namespace Agent.Core
{
    public struct TransitionInfo
    {
        /// <summary>
        /// The set of possible transitions within the prompt context.
        /// </summary>
        public AgentPromptContext PromptContext { get; set; }

        /// <summary>
        /// The transition choice by the agent.
        /// </summary>
        public List<string> ResponseTokens { get; set; }
    }

    /// <summary>
    /// Manages the core execution loop of the agent, traversing the goal hierarchy and using a language
    /// model to "think" about the situation, then transitioning appropriately based on the completion response.
    /// </summary>
    public class AgentExecutor
    {
        private readonly AgentState _agentState;
        private readonly AgentOptions _agentOptions;
        private readonly ILanguageModel _languageModel;
        private readonly ILanguageParser _languageParser;
        private readonly ILogger _logger;
        private readonly IServiceProvider _serviceProvider;

        public AgentExecutor(AgentState agentState, AgentOptions agentOptions, ILanguageModel languageModel, ILogger logger, IServiceProvider serviceProvider)
        {
            _agentState = agentState;
            _agentOptions = agentOptions;
            _languageModel = languageModel;
            _languageParser = _languageModel.CreateLanguageParser();
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task Run()
        {
            await StateMachineLoop(_agentState);
        }

        private async Task StateMachineLoop(AgentState agentState)
        {
            try
            {
                while (agentState.HasGoals())
                {
                    agentState.TryGetGoal(out var currentGoal);

                    await currentGoal.PrePrompt(agentState);

                    var transitionInfo = new TransitionInfo();
                    if (currentGoal.ShouldRequestPrompt(agentState))
                    {
                        currentGoal.IncrementPromptCount(1);

                        var generatePromptResult = await GeneratePrompt(agentState);
                        var prompt = generatePromptResult.Prompt;
                        var chatResult = await _languageModel.ChatCompletion(prompt);
                        if (chatResult.Choices.Count == 0)
                        {
                            throw new InvalidOperationException("The chat API call failed to return a choice.");
                        }

                        // Sensory memory is cleared prior to generating more observations in the response step.
                        // Anything important must be synthesized to short-term memory.
                        agentState.Observations.Clear();

                        // Figure out which action it is taking.
                        var response = chatResult.Choices[0].Message.TextContent;
                        var processResult = await ProcessResponse(prompt, response, agentState);

                        transitionInfo.ResponseTokens = processResult.ResponseTokens;
                        transitionInfo.PromptContext = generatePromptResult.PromptContext;
                    }

                    await currentGoal.PreTransition(agentState);

                    // Transition to next step (push or pop)
                    CheckTransition(agentState, transitionInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception: {ex}");
                // Consider logging the exception to a file or logging system
            }

            _logger.Log($"Agent execution complete.");
        }

        private void CheckTransition(AgentState agentState, TransitionInfo transitionInfo)
        {
            agentState.TryGetGoal(out var currentGoal);

            // Complete any optional subgoals
            if (currentGoal.Spec.OptionalSubgoals.Count > 0 && !currentGoal.IsDone())
            {
                // Find the new goal, as selected by the agent (LLM)
                var chosenGoal = FindChosenGoal(transitionInfo.PromptContext, transitionInfo.ResponseTokens);
                if (chosenGoal == _agentOptions.DoneGoal)
                {
                    _logger.Log($"[{currentGoal.Spec.Title}]: popping goal");

                    currentGoal.MarkDone();
                }
                else
                {
                    _logger.Log($"[{currentGoal.Spec.Title}]: pushing goal [{chosenGoal.Title}]");

                    agentState.InsertGoal(chosenGoal, _serviceProvider, parent: currentGoal, forceCurrent: true);
                }
            }

            // Check for completion, and pop
            if (currentGoal.Spec.CompletionMethod == CompletionMethod.WhenChildrenDone)
            {
                if (currentGoal.Children.Count == 0)
                {
                    throw new InvalidOperationException($"Goal {currentGoal.Spec.Title} completes when children complete but has no children.");
                }

                if (!currentGoal.HasAnyChildren(c => !c.IsDone()))
                {
                    currentGoal.MarkDone();
                }
            }

            // Automatic transition for nodes with no optional children
            if (currentGoal.Spec.OptionalSubgoals.Count == 0 && currentGoal.Children.Count > 0)
            {
                var childGoal = currentGoal.Children[0];
                if (!childGoal.IsDone())
                {
                    agentState.SetCurrentGoal(childGoal);
                }
            }

            if (currentGoal.IsDone())
            {
                agentState.NextGoal();
            }
        }

        public class GeneratePromptResult
        {
            public string Prompt { get; set; }
            public AgentPromptContext PromptContext { get; set; }
        }

        public delegate void CustomizePromptContextDelegate(AgentPromptContext context, AgentState agentState);

        public event CustomizePromptContextDelegate CustomizePromptContext;

        private async Task<GeneratePromptResult> GeneratePrompt(AgentState agentState)
        {
            agentState.TryGetGoal(out var currentGoal);
            _logger.Log($"[{currentGoal.Spec.Title}]: generating prompt");

            // Fill-in prompt context from current agent state
            var promptContext = new AgentPromptContext();
            promptContext.ShortTermMemoryJson = agentState.ShortTermMemory.ToJson();
            promptContext.Observations = agentState.Observations;
            promptContext.Goals = agentState.Goals.Reverse().ToList(); // From high level to low level goals

            CustomizePromptContext?.Invoke(promptContext, agentState);

            //promptContext.FeatureSpecification = FeatureSpecification;

            // Construct a special "done" goal.
            if (currentGoal.Spec.CompletionMethod != CompletionMethod.WhenMarkedDone)
            {
                promptContext.OptionalSubgoals.Add(_agentOptions.DoneGoal);
                if (currentGoal.Spec.DoneDescription == null && currentGoal.RequiresDoneDescription())
                {
                    throw new InvalidDataException($"The goal '{currentGoal.Spec.Title}' must have a {nameof(currentGoal.Spec.DoneDescription)}");
                }
                _agentOptions.DoneGoal.OptionDescription = currentGoal.Spec.DoneDescription;
            }

            // Run template substitution on goal stack
            foreach (var goal in agentState.Goals)
            {
                goal.PopulatePrompt(promptContext, agentState);
                goal.Spec.StackDescription.Bind(promptContext);

                //var reminderDescription = goal.Spec.ReminderDescription;                
                //if (reminderDescription != null)
                //{
                //    reminderDescription.Bind(promptContext);
                //    promptContext.Reminders.Add(new AgentReminder { Description = reminderDescription.Text });
                //}
            }

            // Run template substitution for optional goals
            foreach (var optionalSubgoal in currentGoal.Spec.OptionalSubgoals)
            {
                promptContext.OptionalSubgoals.Add(optionalSubgoal);
            }

            foreach (var optionalSubgoal in promptContext.OptionalSubgoals)
            {
                optionalSubgoal.OptionDescription.Bind(promptContext);
                optionalSubgoal.StackDescription.Bind(promptContext);
            }
            promptContext.ShouldDisplayActions = promptContext.OptionalSubgoals.Count > 0;
            promptContext.ShouldDisplayObservations = promptContext.Observations.Count > 0;

            // Generate final prompt
            var prompt = _agentOptions.GoalPrompt.Evaluate(promptContext);

            return new GeneratePromptResult
            {
                Prompt = prompt,
                PromptContext = promptContext
            };
        }

        private class ProcessResponseResult
        {
            public List<string> ResponseTokens { get; set; }
        }
        private async Task<ProcessResponseResult> ProcessResponse(string prompt, string response, AgentState agentState)
        {
            var result = new ProcessResponseResult();
            var responseTokens = _languageParser.ExtractResponseTokens(response);
            agentState.TryGetGoal(out var currentGoal);

            bool hasResponseToken = responseTokens != null && responseTokens.Count == 1;
            if (!currentGoal.Spec.IsAutoComplete && !hasResponseToken)
            {
                throw new InvalidOperationException($"Invalid response tokens: {string.Join(",", responseTokens)}");
            }

            _logger.Log($"[{currentGoal.Spec.Title}]: processing response {string.Join(", ", responseTokens)}");

            await currentGoal.ProcessResponse(prompt, response, agentState, _languageParser);

            result.ResponseTokens = responseTokens;
            return result;
        }

        private AgentGoalSpec FindChosenGoal(AgentPromptContext promptContext, List<string> responseTokens)
        {
            AgentGoalSpec chosenGoal = null;
            foreach (var possibleGoal in promptContext.OptionalSubgoals)
            {
                if (_agentOptions.GoalPrompt == null)
                {
                    throw new InvalidDataException($"Default goal prompt is invalid.");
                }

                if (responseTokens[0] == possibleGoal.OptionDescription.Key)
                {
                    chosenGoal = possibleGoal;
                }
            }

            if (chosenGoal == null)
            {
                if (responseTokens[0] == _agentOptions.DoneGoal.OptionDescription.Key)
                {
                    chosenGoal = _agentOptions.DoneGoal;
                }
            }

            if (chosenGoal == null)
            {
                throw new InvalidOperationException($"Agent chose an option that was not currently available {responseTokens[0]}");
            }

            return chosenGoal;
        }
    }
}
