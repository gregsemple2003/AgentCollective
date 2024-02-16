using BizDevAgent.Services;
using BizDevAgent.DataStore;
using BizDevAgent.Agents;
using BizDevAgent.Utilities;
using FluentResults;
using BizDevAgent.Flow;

namespace BizDevAgent.Jobs
{
    public class GoalTreeContext
    {
        public RepositoryQuerySession RepositoryQuerySession { get; set; }

        private static AsyncLocal<GoalTreeContext> _current = new AsyncLocal<GoalTreeContext>();

        public static GoalTreeContext Current
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
        public IBuildCommand BuildAgent { get; set; }

        private readonly GitService _gitService;
        private readonly RepositoryQueryService _repositoryQueryService;
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly AssetDataStore _assetDataStore;
        private readonly LanguageModelService _languageModelService;
        private readonly IResponseParser _languageModelParser;
        private readonly VisualStudioService _visualStudioService;
        private readonly IServiceProvider _serviceProvider;
        private readonly AgentGoal _doneGoal;
        private readonly PromptAsset _goalPrompt;
        private readonly RepositoryQuerySession _repositoryQuerySession;
        private readonly JobRunner _jobRunner;
        private readonly ILogger _logger;

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
            _repositoryQuerySession = _repositoryQueryService.CreateSession(Paths.GetSourceControlRootPath());
            _goalPrompt = _assetDataStore.GetHardRef<PromptAsset>("Default_Goal");
            _doneGoal = _assetDataStore.GetHardRef<AgentGoal>("DoneGoal");
        }

        public AgentGoal CreateGoalTree()
        {
            AgentGoal agentGoal = null;
            try
            {
                GoalTreeContext.Current = new GoalTreeContext()
                {
                    RepositoryQuerySession = _repositoryQuerySession
                };
                agentGoal = _assetDataStore.GetHardRef<AgentGoal>("ImplementFeatureGoal");
            }
            finally
            {
                GoalTreeContext.Current = null;
            }

            return agentGoal;
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

            // Define workflow
            var goalTree = CreateGoalTree();

            // Initialize agent
            var agentState = new ProgrammerAgentState()
            { 
                ShortTermMemory = new ProgrammerShortTermMemory()
                {
                    CodingTasks = new CodingTasks()
                    {
                        Steps = new List<ImplementationStep>
                        { 
                            new ImplementationStep { Step = 1, Description = "Example step, feel free to modify." }
                        }
                    }
                }
            };
            agentState.Goals.Push(goalTree);

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
            while(agentState.Goals.Count > 0) 
            {
                // Get language model to select an option
                var currentGoal = agentState.Goals.Peek();
                if (currentGoal.IsDone())
                {
                    agentState.Goals.Pop();
                    if (agentState.Goals.Count == 0)
                    {
                        return;
                    }
                    currentGoal = agentState.Goals.Peek();
                }

                var transitionInfo = new TransitionInfo();
                if (currentGoal.ShouldRequestCompletion())
                {
                    currentGoal.IncrementCompletionCount(1);

                    var generatePromptResult = await GeneratePrompt(agentState);
                    var chatResult = await _languageModelService.ChatCompletion(generatePromptResult.Prompt);
                    if (chatResult.ChatResult.Choices.Count == 0)
                    {
                        throw new InvalidOperationException("The chat API call failed to return a choice.");
                    }

                    // Sensory memory is cleared prior to generating more observations in the response step.
                    // Anything important must be synthesized to short-term memory.
                    agentState.Observations.Clear();

                    // Figure out which action it is taking.
                    var response = chatResult.ChatResult.Choices[0].Message.TextContent;
                    var processResult = await ProcessResponse(generatePromptResult.Prompt, response, agentState);

                    transitionInfo.ResponseTokens = processResult.ResponseTokens;
                    transitionInfo.PromptContext = generatePromptResult.PromptContext;
                }

                // Transition to next step (push or pop)
                Transition(agentState, transitionInfo);
            }
        }

        private void Transition(AgentState agentState, TransitionInfo transitionInfo)
        {
            // If you push a state, you must Reset it because you could be revisiting the same state.
            // If you pop a state, you must keep popping done nodes because parents are complete
            // when either the agent choses to be done, or there are no choices for the agent to make.

            // Required subgoals must be completed prior to optional subgoals
            var currentGoal = agentState.Goals.Peek();
            if (currentGoal.RequiredSubgoals.Count > 0 && !currentGoal.IsDone())
            {
                for (int i = currentGoal.RequiredSubgoals.Count - 1; i >= 0; i--)
                {
                    AgentGoal requiredSubgoal = currentGoal.RequiredSubgoals[i];

                    var isGoalOnStack = agentState.Goals.Any(g => g == requiredSubgoal);
                    if (isGoalOnStack)
                    {
                        throw new InvalidOperationException($"Cannot push goal '{requiredSubgoal.Title}' onto the stack when it's already on the stack.  Possible infinite recursion.");
                    }

                    requiredSubgoal.Reset();
                    agentState.Goals.Push(requiredSubgoal);
                }
            }

            // Complete any optional subgoals
            if (currentGoal.OptionalSubgoals.Count > 0 && !currentGoal.IsDone())
            {
                // Find the new goal, as selected by the agent (LLM)
                var chosenGoal = FindChosenGoal(transitionInfo.PromptContext, transitionInfo.ResponseTokens);
                if (chosenGoal == _doneGoal)
                {
                    _logger.Log($"[{currentGoal.Title}]: popping goal");

                    currentGoal.MarkDone();
                    agentState.Goals.Pop();

                    // Pop other goals that were previously completed
                    while (agentState.Goals.Count > 0)
                    {
                        var goal = agentState.Goals.Peek();
                        if (goal.IsDone())
                        {
                            agentState.Goals.Pop();
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    _logger.Log($"[{currentGoal.Title}]: pushing goal [{chosenGoal.Title}]");

                    chosenGoal.Reset();
                    agentState.Goals.Push(chosenGoal);
                }
            }
            else
            {
                // Goals with no options are automatically complete
                currentGoal.MarkDone();
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
            var currentGoal = agentState.Goals.Peek();

            bool hasResponseToken = responseTokens != null && responseTokens.Count == 1;
            if (!currentGoal.IsAutoComplete && !hasResponseToken)
            {
                throw new InvalidOperationException($"Invalid response tokens: {string.Join(",", responseTokens)}");
            }


            _logger.Log($"[{currentGoal.Title}]: processing response {string.Join(", ", responseTokens)}");

            await currentGoal.ProcessResponse(prompt, response, agentState, _languageModelParser);

            result.ResponseTokens = responseTokens;
            return result;
        }

        private AgentGoal FindChosenGoal(AgentPromptContext promptContext, List<string> responseTokens)
        {
            AgentGoal chosenGoal = null;
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

        private async Task<string> GenerateRepositoryQueryApi()
        {
            var repositoryFile = await _repositoryQuerySession.FindFileInRepo($"{nameof(RepositoryQuerySession)}.cs", logError: false);
            var repositoryQueryApi = _codeAnalysisService.GeneratePublicApiSkeleton(repositoryFile.Contents);
            return repositoryQueryApi;
        }

        public class GeneratePromptResult
        {
            public string Prompt { get; set; }
            public AgentPromptContext PromptContext { get; set; }
        }
        private async Task<GeneratePromptResult> GeneratePrompt(AgentState agentState)
        {
            var currentGoal = agentState.Goals.Peek();
            _logger.Log($"[{currentGoal.Title}]: generating prompt");

            // Fill-in prompt context from current agent state
            var promptContext = new AgentPromptContext();
            var repositoryQuerySessionApi = await GenerateRepositoryQueryApi();
            var repositoryQuerySample = await _repositoryQuerySession.FindFileInRepo("RepositoryQueryJob.cs");
            promptContext.AdditionalData["RepositoryQuerySessionApi"] = repositoryQuerySessionApi;
            promptContext.AdditionalData["RepositoryQuerySessionSample"] = repositoryQuerySample.Contents;            
            promptContext.ShortTermMemoryJson = agentState.ShortTermMemory.ToJson();
            promptContext.Observations = agentState.Observations;
            promptContext.Goals = agentState.Goals.Reverse().ToList();
            promptContext.FeatureSpecification = FeatureSpecification;

            // Construct a special "done" goal.
            if (!currentGoal.IsAutoComplete)
            {
                promptContext.OptionalSubgoals.Add(_doneGoal);
                if (currentGoal.DoneDescription == null && currentGoal.RequiresDoneDescription())
                {
                    throw new InvalidDataException($"The goal '{currentGoal.Title}' must have a {nameof(currentGoal.DoneDescription)}");
                }
                _doneGoal.OptionDescription = currentGoal.DoneDescription;
            }

            // Run template substitution based on context
            foreach (var optionalSubgoal in currentGoal.OptionalSubgoals)
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
            var buildResult = await BuildAgent.Build(LocalRepoPath);
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
