using BizDevAgent.Services;
using BizDevAgent.DataStore;
using BizDevAgent.Agents;
using BizDevAgent.Utilities;
using BizDevAgent.Utilities.Commands;
using FluentResults;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Microsoft.Extensions.DependencyInjection;
using System;
using Newtonsoft.Json;
using Rystem.OpenAi;
using static BizDevAgent.Jobs.ProgrammerImplementFeatureJob;

namespace BizDevAgent.Jobs
{
    public class ImplementationStep
    { 
        public int Step { get; set; }
        public string Description { get; set; }
    }
    public class ImplementationPlan
    {
        public List<ImplementationStep> Steps { get; set; }
    }

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
            var implementationPlan = new ImplementationPlan
            {
                Steps = new List<ImplementationStep> 
                { 
                    new ImplementationStep { Step = 1, Description = "Example step, feel free to modify." }
                }
            };
            var implementationPlanJson = JsonConvert.SerializeObject(implementationPlan);
            var agentState = new AgentState();
            agentState.Variables.Add(new AgentVariable { Name = "ImplementationPlan", Value = implementationPlanJson });
            agentState.Variables.Add(new AgentVariable { Name = "Conclusions", Value = "{}" });
            agentState.Goals.Push(goalTree);
            agentState.Goals.Push(goalTree.RequiredSubgoals[0]);

            // Run the machine on the goal hierarchy until done
            await StateMachineLoop(agentState);

            //// Build and report errors
            //var repositoryQuerySession = _repositoryQueryService.CreateSession(LocalRepoPath);
            //var buildResult = await BuildRepository(repositoryQuerySession);
            //if (buildResult.IsFailed)
            //{
            //    throw new NotImplementedException("The build failed, we don't deal with that yet.");
            //}
        }

        private async Task StateMachineLoop(AgentState agentState)
        {
            while(agentState.Goals.Count > 0) 
            {
                // Get language model to respond to goal prompt
                var generatePromptResult = await GeneratePrompt(agentState);
                var chatResult = await _languageModelService.ChatCompletion(generatePromptResult.Prompt);
                if (chatResult.ChatResult.Choices.Count == 0)
                {
                    throw new InvalidOperationException("The chat API call failed to return a choice.");
                }
                agentState.Observations.Clear();

                // Figure out which action it is taking.
                var response = chatResult.ChatResult.Choices[0].Message.TextContent;
                var processResult = ProcessResponse(response, agentState);

                // Transition to next step (push or pop)
                Transition(agentState, processResult.ResponseTokens, generatePromptResult);
            }
        }

        private void Transition(AgentState agentState, List<string> responseTokens, GeneratePromptResult generatePromptResult)
        {
            var currentGoal = agentState.Goals.Peek();
            if (currentGoal.AutoComplete)
            {
                agentState.Goals.Pop();
                return;
            }

            var chosenGoal = FindChosenGoal(generatePromptResult, responseTokens);
            if (chosenGoal == _doneGoal)
            {
                _logger.Log($"[{currentGoal.Title}]: popping goal");

                agentState.Goals.Pop();
            }
            else
            {
                _logger.Log($"[{currentGoal.Title}]: pushing goal [{chosenGoal.Title}]");

                agentState.Goals.Push(chosenGoal);
            }
        }

        private class ProcessResponseResult
        { 
            public List<string> ResponseTokens { get; set; }
        }
        private ProcessResponseResult ProcessResponse(string response, AgentState agentState)
        {
            var result = new ProcessResponseResult();
            var responseTokens = _languageModelParser.ExtractResponseTokens(response);
            var currentGoal = agentState.Goals.Peek();

            bool hasResponseToken = responseTokens != null && responseTokens.Count == 1;
            if (!currentGoal.AutoComplete && !hasResponseToken)
            {
                throw new InvalidOperationException($"Invalid response tokens: {string.Join(",", responseTokens)}");
            }


            _logger.Log($"[{currentGoal.Title}]: processing response {string.Join(", ", responseTokens)}");

            currentGoal.ProcessResponse(response, agentState, _languageModelParser);

            result.ResponseTokens = responseTokens;
            return result;
        }

        private AgentGoal FindChosenGoal(GeneratePromptResult generatePromptResult, List<string> responseTokens)
        {
            AgentGoal chosenGoal = null;
            foreach (var possibleGoal in generatePromptResult.PromptContext.OptionalSubgoals)
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
            promptContext.Variables = agentState.Variables;
            promptContext.Observations = agentState.Observations;
            promptContext.Goals = agentState.Goals.Reverse().ToList();
            promptContext.FeatureSpecification = FeatureSpecification;

            // Construct a special "done" goal.
            if (!currentGoal.AutoComplete)
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
            var asset = _assetDataStore.Get(assetName).GetAwaiter().GetResult();
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
