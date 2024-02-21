using BizDevAgent.Services;
using BizDevAgent.DataStore;
using BizDevAgent.Agents;
using BizDevAgent.Utilities;
using FluentResults;
using BizDevAgent.Flow;

namespace BizDevAgent.Jobs
{
    // TODO gsemple: Can we pass this information using scoped service provider?  Specific to the implementation job.
    // This would require also overriding the service provider used in our TypedJsonConverter, or allowing it to be 
    // overridden by the asset data store.
    public class ProgrammerContext
    {
        public RepositoryQuerySession SelfRepositoryQuerySession { get; set; }
        public RepositoryQuerySession TargetRepositoryQuerySession { get; set; }
        public ProgrammerImplementFeatureJob ImplementFeatureJob { get; set; }

        private static AsyncLocal<ProgrammerContext> _current = new AsyncLocal<ProgrammerContext>();

        public static ProgrammerContext Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }
    }

    /// <summary>
    /// Implement a feature on a remote Git repo given the feature specification in natural language.
    /// </summary>
    [TypeId("ProgrammerImplementFeatureJob")]
    public class ProgrammerImplementFeatureJob : Job
    {
        public string GitRepoUrl { get; set; }
        public string LocalRepoPath { get; set; }
        public string FeatureSpecification { get; set; }
        public IBuildCommand BuildCommand { get; set; }

        private readonly GitService _gitService;
        private readonly RepositoryQueryService _repositoryQueryService;
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly AssetDataStore _assetDataStore;
        private readonly LanguageModelService _languageModelService;
        private readonly IResponseParser _languageModelParser;
        private readonly VisualStudioService _visualStudioService;
        private readonly IServiceProvider _serviceProvider;
        private readonly AgentGoalSpec _doneGoal;
        private readonly PromptAsset _goalPrompt;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;
        private readonly JobRunner _jobRunner;
        private readonly ILogger _logger;

        private RepositoryQuerySession _targetRepositoryQuerySession;

        public ProgrammerImplementFeatureJob(GitService gitService, RepositoryQueryService repositoryQueryService, CodeAnalysisService codeAnalysisService, AssetDataStore assetDataStore, LanguageModelService languageModelService, VisualStudioService visualStudioService, IServiceProvider serviceProvider, JobRunner jobRunner, LoggerFactory loggerFactory)
        {
            _gitService = gitService;
            _repositoryQueryService = repositoryQueryService;
            _codeAnalysisService = codeAnalysisService;
            _assetDataStore = assetDataStore;
            _languageModelService = languageModelService;
            _visualStudioService = visualStudioService;
            _serviceProvider = serviceProvider;
            _jobRunner = jobRunner;
            _logger = loggerFactory.CreateLogger("Agent");

            _languageModelParser = _languageModelService.CreateResponseParser();
            _selfRepositoryQuerySession = _repositoryQueryService.CreateSession(Paths.GetSourceControlRootPath());
            _goalPrompt = _assetDataStore.GetHardRef<PromptAsset>("Default_Goal");
            _doneGoal = _assetDataStore.GetHardRef<AgentGoalSpec>("DoneGoal");
        }

        public AgentState CreateAgent(string targetLocalRepoPath)
        {
            AgentState agentState = null;
            try
            {
                _targetRepositoryQuerySession = _repositoryQueryService.CreateSession(targetLocalRepoPath);
                ProgrammerContext.Current = new ProgrammerContext()
                {
                    ImplementFeatureJob = this,
                    SelfRepositoryQuerySession = _selfRepositoryQuerySession,
                    TargetRepositoryQuerySession = _targetRepositoryQuerySession,
                };
                var goalTreeSpec = _assetDataStore.GetHardRef<AgentGoalSpec>("ImplementFeatureGoal");

                // instnatiate graph
                var goalTree = goalTreeSpec.InstantiateGraph(_serviceProvider);

                // TODO gsemple: Jump-start the agent into a specified state, not always possible
                agentState = _assetDataStore.GetHardRef<ProgrammerAgentState>("RefinedImplementationPlanModified");                
                agentState.SetCurrentGoal(goalTree.Children[1]);
            }
            finally
            {
                ProgrammerContext.Current = null;
            }

            return agentState;
        }


        public async override Task Run()
        {            
            // Clone the repository
            if (!Directory.Exists(LocalRepoPath))
            {
                var cloneResult = await _gitService.CloneRepository(GitRepoUrl, LocalRepoPath);
                if (cloneResult.IsFailed)
                {
                    throw new InvalidOperationException("Failed to clone repository");
                }
            }

            // todo gsemple: testing only
            // Revert any local changes in the repository.
            var revertResult = await _gitService.RevertAllChanges(LocalRepoPath);
            if (revertResult.IsFailed)
            {
                throw new InvalidOperationException("Failed to revert all local changes");
            }

            // Define workflow
            var agentState = CreateAgent(LocalRepoPath);

            // Initialize agent
            //var agentState = new ProgrammerAgentState()
            //{ 
            //    ShortTermMemory = new ProgrammerShortTermMemory()
            //    {
            //        CodingTasks = new CodingTasks()
            //        {
            //            Steps = new List<ImplementationStep>
            //            { 
            //                new ImplementationStep { Step = 1, Description = "Example step, feel free to modify." }
            //            }
            //        }
            //    }
            //};

            // Run the machine on the goal hierarchy until done
            await StateMachineLoop(agentState);

            _logger.Log($"Agent execution complete.");

            //// Build and report errors
            //var repositoryQuerySession = _repositoryQueryService.CreateSession(LocalRepoPath);
            //var buildResult = await BuildRepository(repositoryQuerySession);
            //if (buildResult.IsFailed)
            //{
            //    throw new NotImplementedException("The build failed, we don't deal with that yet.");
            //}
        }

        public struct TransitionInfo
        {
            /// <summary>
            /// The set of possible transitions within the prompt context.
            /// </summary>
            public AgentPromptContext PromptContext { get; set; }

            /// <summary>
            /// The transition choice by the agent.
            /// </summary>
            public List<string> ResponseTokens {  get; set; }
        }

        private async Task StateMachineLoop(AgentState agentState)
        {
            while(agentState.HasGoals()) 
            {
                agentState.TryGetGoal(out var currentGoal);

                await currentGoal.PreCompletion(agentState);

                var transitionInfo = new TransitionInfo();
                if (currentGoal.ShouldRequestCompletion(agentState))
                {
                    currentGoal.IncrementCompletionCount(1);

                    var generatePromptResult = await GeneratePrompt(agentState);
                    var prompt = generatePromptResult.Prompt;
                    var chatResult = await _languageModelService.ChatCompletion(prompt);
                    if (chatResult.ChatResult.Choices.Count == 0)
                    {
                        throw new InvalidOperationException("The chat API call failed to return a choice.");
                    }

                    // Sensory memory is cleared prior to generating more observations in the response step.
                    // Anything important must be synthesized to short-term memory.
                    agentState.Observations.Clear();

                    // Figure out which action it is taking.
                    var response = chatResult.ChatResult.Choices[0].Message.TextContent;
                    var processResult = await ProcessResponse(prompt, response, agentState);

                    transitionInfo.ResponseTokens = processResult.ResponseTokens;
                    transitionInfo.PromptContext = generatePromptResult.PromptContext;
                }

                await currentGoal.PreTransition(agentState);

                // Transition to next step (push or pop)
                CheckTransition(agentState, transitionInfo);
            }
        }

        private void CheckTransition(AgentState agentState, TransitionInfo transitionInfo)
        {
            agentState.TryGetGoal(out var currentGoal);

            // Complete any optional subgoals
            if (currentGoal.Spec.OptionalSubgoals.Count > 0 && !currentGoal.IsDone())
            {
                // Find the new goal, as selected by the agent (LLM)
                var chosenGoal = FindChosenGoal(transitionInfo.PromptContext, transitionInfo.ResponseTokens);
                if (chosenGoal == _doneGoal)
                {
                    _logger.Log($"[{currentGoal.Spec.Title}]: popping goal");

                    currentGoal.MarkDone();
                }
                else
                {
                    _logger.Log($"[{currentGoal.Spec.Title}]: pushing goal [{chosenGoal.Title}]");

                    agentState.InsertGoal(chosenGoal, parent: currentGoal);
                }
            }

            // Check for completion, and pop
            if (currentGoal.Spec.CompletionMethod == CompletionMethod.WhenChildrenComplete)
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

        private class ProcessResponseResult
        { 
            public List<string> ResponseTokens { get; set; }
        }
        private async Task<ProcessResponseResult> ProcessResponse(string prompt, string response, AgentState agentState)
        {
            var result = new ProcessResponseResult();
            var responseTokens = _languageModelParser.ExtractResponseTokens(response);
            agentState.TryGetGoal(out var currentGoal);

            bool hasResponseToken = responseTokens != null && responseTokens.Count == 1;
            if (!currentGoal.Spec.IsAutoComplete && !hasResponseToken)
            {
                throw new InvalidOperationException($"Invalid response tokens: {string.Join(",", responseTokens)}");
            }

            _logger.Log($"[{currentGoal.Spec.Title}]: processing response {string.Join(", ", responseTokens)}");

            await currentGoal.ProcessResponse(prompt, response, agentState, _languageModelParser);

            result.ResponseTokens = responseTokens;
            return result;
        }

        private AgentGoalSpec FindChosenGoal(AgentPromptContext promptContext, List<string> responseTokens)
        {
            AgentGoalSpec chosenGoal = null;
            foreach (var possibleGoal in promptContext.OptionalSubgoals)
            {
                if (_goalPrompt == null)
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
                if (responseTokens[0] == _doneGoal.OptionDescription.Key)
                {
                    chosenGoal = _doneGoal;
                }
            }

            if (chosenGoal == null)
            {
                throw new InvalidOperationException($"Agent chose an option that was not currently available {responseTokens[0]}");
            }

            return chosenGoal;
        }

        public class GeneratePromptResult
        {
            public string Prompt { get; set; }
            public AgentPromptContext PromptContext { get; set; }
        }
        private async Task<GeneratePromptResult> GeneratePrompt(AgentState agentState)
        {
            agentState.TryGetGoal(out var currentGoal);
            _logger.Log($"[{currentGoal.Spec.Title}]: generating prompt");

            // Fill-in prompt context from current agent state
            var promptContext = new AgentPromptContext();
            promptContext.ShortTermMemoryJson = agentState.ShortTermMemory.ToJson();
            promptContext.Observations = agentState.Observations;
            promptContext.Goals = agentState.Goals.Reverse().ToList(); // From high level to low level goals
            promptContext.FeatureSpecification = FeatureSpecification;

            // Construct a special "done" goal.
            if (currentGoal.Spec.CompletionMethod != CompletionMethod.WhenMarkedDone)
            {
                promptContext.OptionalSubgoals.Add(_doneGoal);
                if (currentGoal.Spec.DoneDescription == null && currentGoal.RequiresDoneDescription())
                {
                    throw new InvalidDataException($"The goal '{currentGoal.Spec.Title}' must have a {nameof(currentGoal.Spec.DoneDescription)}");
                }
                _doneGoal.OptionDescription = currentGoal.Spec.DoneDescription;
            }

            // Run template substitution on goal stack
            foreach (var goal in agentState.Goals)
            {
                goal.CustomizePrompt(promptContext, agentState);
                goal.Spec.StackDescription.Bind(promptContext);

                var reminderDescription = goal.Spec.ReminderDescription;                
                if (reminderDescription != null)
                {
                    reminderDescription.Bind(promptContext);
                    promptContext.Reminders.Add(new AgentReminder { Description = reminderDescription.Text });
                }
            }

            // Run template substitution for optional goals
            foreach (var optionalSubgoal in currentGoal.Spec.OptionalSubgoals)
            {
                promptContext.OptionalSubgoals.Add(optionalSubgoal);
            }

            foreach(var optionalSubgoal in promptContext.OptionalSubgoals)
            {
                optionalSubgoal.OptionDescription.Bind(promptContext);
                optionalSubgoal.StackDescription.Bind(promptContext);
            }
            promptContext.ShouldDisplayActions = promptContext.OptionalSubgoals.Count > 0;
            promptContext.ShouldDisplayObservations = promptContext.Observations.Count > 0;
            
            // Generate final prompt
            var prompt = _goalPrompt.Evaluate(promptContext);

            return new GeneratePromptResult
            {
                Prompt = prompt,
                PromptContext = promptContext
            };
        }

        // TODO gsemple: remove, need to implement asset references in json
        private TAsset AssetRef<TAsset>(string assetName) where TAsset : Asset
        {
            var asset = _assetDataStore.Get(assetName);
            return (TAsset) asset;
        }

        private async Task<Result<BuildResult>> BuildRepository(RepositoryQuerySession repositoryQuerySession)
        {
            var buildResult = await BuildCommand.Build(LocalRepoPath);
            if (buildResult.IsFailed)
            {
                _logger.Log("ERRORS:");
                foreach (var error in buildResult.Errors)
                {
                    if (error is BuildError buildError)
                    {
                        _logger.Log(buildError.RawMessage);
                    }
                }
                _logger.Log();

                _logger.Log("SOURCE:");
                foreach (var error in buildResult.Errors)
                {
                    if (error is BuildError buildError)
                    {
                        await repositoryQuerySession.PrintFileContentsAroundLine(buildError.FilePath, buildError.LineNumber, 5); // Example: 5 lines around each error
                    }
                }
                _logger.Log();
            }

            return buildResult;
        }
    }
}
