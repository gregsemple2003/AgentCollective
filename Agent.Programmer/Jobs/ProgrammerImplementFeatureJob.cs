﻿using Agent.Services;
using Agent.Core;
using FluentResults;

namespace Agent.Programmer
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
        private readonly AssetDataStore _assetDataStore;
        private readonly ILanguageModel _languageModelService;
        private readonly ILanguageModel _planningLanguageModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;
        private readonly ILogger _logger;

        private RepositoryQuerySession _targetRepositoryQuerySession;

        public ProgrammerImplementFeatureJob(GitService gitService, RepositoryQueryService repositoryQueryService, AssetDataStore assetDataStore, OpenAiLanguageModel languageModelService, AnthropicLanguageModel anthropicLanguageModel, IServiceProvider serviceProvider, LoggerFactory loggerFactory)
        {
            _gitService = gitService;
            _repositoryQueryService = repositoryQueryService;
            _assetDataStore = assetDataStore;
            _languageModelService = languageModelService;
            _planningLanguageModel = anthropicLanguageModel;
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger("Agent");

            _selfRepositoryQuerySession = _repositoryQueryService.CreateSession(Paths.GetSourceControlRootPath());
        }

        public AgentState CreateAgent(string targetLocalRepoPath)
        {
            var goalTreeSpec = _assetDataStore.GetHardRef<AgentGoalSpec>("ImplementFeatureGoal");

            // instantiate graph
            var goalTree = goalTreeSpec.InstantiateGraph(_serviceProvider);

            // TODO gsemple: Jump-start the agent into a specified state, not always possible
            var agentState = _assetDataStore.GetHardRef<ProgrammerAgentState>("RefinedImplementationPlanModified");                
            agentState.SetCurrentGoal(goalTree.Children[1]);
            return agentState;
        }

        public async Task RunInternal()
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

            var chatResult = await _planningLanguageModel.ChatCompletion("Hello how are you?  Do you feel like being an agent today?");

            // Run the machine on the goal hierarchy until done
            var agentState = CreateAgent(LocalRepoPath);
            var goalPrompt = _assetDataStore.GetHardRef<PromptAsset>("Default_Goal");
            var doneGoal = _assetDataStore.GetHardRef<AgentGoalSpec>("DoneGoal");
            var agentOptions = new AgentOptions { DoneGoal = doneGoal, GoalPrompt = goalPrompt };
            var agentExecutor = new AgentExecutor(agentState, agentOptions, _languageModelService, _logger, _serviceProvider);
            agentExecutor.CustomizePromptContext += (promptContext, agentState) =>
            {
                promptContext.FeatureSpecification = FeatureSpecification;
            };
            await agentExecutor.Run();
        }

        public async override Task Run()
        {
            try
            {
                _targetRepositoryQuerySession = _repositoryQueryService.CreateSession(LocalRepoPath);
                ProgrammerContext.Current = new ProgrammerContext()
                {
                    ImplementFeatureJob = this,
                    SelfRepositoryQuerySession = _selfRepositoryQuerySession,
                    TargetRepositoryQuerySession = _targetRepositoryQuerySession,
                };

                await RunInternal();
            }
            finally
            {
                ProgrammerContext.Current = null;
            }
        }
    }
}
