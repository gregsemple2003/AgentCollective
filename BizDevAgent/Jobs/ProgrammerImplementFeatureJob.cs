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

namespace BizDevAgent.Jobs
{
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
        private readonly IResponseParser _languageModerParser;
        private readonly VisualStudioService _visualStudioService;
        private readonly IServiceProvider _serviceProvider;
        private readonly AgentGoal _doneGoal;
        private readonly PromptAsset _goalPrompt;
        private readonly RepositoryQuerySession _repositoryQuerySession;
        
        public ProgrammerImplementFeatureJob(GitService gitService, RepositoryQueryService repositoryQueryService, CodeAnalysisService codeAnalysisService, AssetDataStore assetDataStore, LanguageModelService languageModelService, VisualStudioService visualStudioService, IServiceProvider serviceProvider)
        {
            _gitService = gitService;
            _repositoryQueryService = repositoryQueryService;
            _codeAnalysisService = codeAnalysisService;
            _assetDataStore = assetDataStore;
            _languageModelService = languageModelService;
            _visualStudioService = visualStudioService;
            _serviceProvider = serviceProvider;

            _languageModerParser = _languageModelService.CreateResponseParser();
            _repositoryQuerySession = _repositoryQueryService.CreateSession(Paths.GetSourceControlRootPath());
            _goalPrompt = _assetDataStore.GetHardRef<PromptAsset>("Default_Goal");
            _doneGoal = _assetDataStore.GetHardRef<AgentGoal>("DoneGoal");
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
            var agentState = new AgentState();
            agentState.Variables.Add(new AgentVariable { Name = "ImplementationPlan", Value = "{}" });
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

                // Figure out which action it is taking.
                var response = chatResult.ChatResult.Choices[0].Message.TextContent;
                var responseTokens = _languageModerParser.ExtractResponseTokens(response);
                if (responseTokens == null || responseTokens.Count != 1)
                {
                    throw new InvalidOperationException($"Invalid response tokens: {string.Join(",", responseTokens)}");
                }

                if (response.Contains("EvaluateResearch_Option"))
                {
                    var snippets = _languageModerParser.ExtractSnippets(response);

                    foreach(var snippet in snippets)
                    {
                        if (snippet.LanguageId == "csharp")
                        {
                            // rename the class
                            var newResearchClassName = $"CodeAnalysisJob_{Guid.NewGuid()}";
                            var newContents = _codeAnalysisService.RenameClass(snippet.Contents, "CodeAnalysisJob", newResearchClassName);
                            var assembly = _visualStudioService.InjectCode(snippet.Contents);
                            var newResearchClass = assembly.GetType(newResearchClassName);
                            var newResearchJob = (Job)ActivatorUtilities.CreateInstance(_serviceProvider, newResearchClass);

                            //var jobResult = await _jobRunner.RunJob(job);
                            var x = 3;
                        }
                    }
                }

                // Process action to change agent state
                var chosenGoal = FindChosenGoal(generatePromptResult, responseTokens);
                if (chosenGoal == _doneGoal) 
                {
                    agentState.Goals.Pop();
                }
                else
                {
                    agentState.Goals.Push(chosenGoal);
                }
            }
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
            var repositoryFile = await _repositoryQuerySession.FindFileInRepo("RepositoryQueryService.cs", logError: false);
            var repositoryQueryApi = _codeAnalysisService.GeneratePublicApiSkeleton(repositoryFile.Contents);
            return repositoryQueryApi;
        }

        public class GeneratePromptResult
        {
            public string Prompt { get; set; }
            public PromptContext PromptContext { get; set; }
        }
        private async Task<GeneratePromptResult> GeneratePrompt(AgentState agentState)
        {
            // Fill-in prompt context from current agent state
            var promptContext = new PromptContext();
            var repositoryQuerySessionApi = await GenerateRepositoryQueryApi();
            var repositoryQuerySample = _repositoryQuerySession.FindFileInRepo("RepositoryQueryJob.cs");
            var currentGoal = agentState.Goals.Peek();
            promptContext.AdditionalData["RepositoryQuerySessionApi"] = repositoryQuerySessionApi;
            promptContext.Variables = agentState.Variables;
            promptContext.Goals = agentState.Goals.Reverse().ToList();
            promptContext.FeatureSpecification = FeatureSpecification;

            // Construct a special "done" goal.
            promptContext.OptionalSubgoals.Add(_doneGoal);
            _doneGoal.OptionDescription = currentGoal.DoneDescription;
            if (currentGoal.DoneDescription == null) 
            {
                throw new InvalidDataException($"The goal '{currentGoal.Title}' must have a {nameof(currentGoal.DoneDescription)}");
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

            // Generate final prompt
            var prompt = _goalPrompt.Evaluate(promptContext);

            return new GeneratePromptResult
            {
                Prompt = prompt,
                PromptContext = promptContext
            };
        }

        private AgentGoal CreateGoalTree()
        {
            return _assetDataStore.GetHardRef<AgentGoal>("ImplementFeatureGoal");
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
                Console.WriteLine("ERRORS:");
                foreach (var error in buildResult.Errors)
                {
                    if (error is BuildError buildError)
                    {
                        Console.WriteLine(buildError.RawMessage);
                    }
                }
                Console.WriteLine();

                Console.WriteLine("SOURCE:");
                foreach (var error in buildResult.Errors)
                {
                    if (error is BuildError buildError)
                    {
                        await repositoryQuerySession.PrintFileContentsAroundLine(buildError.FilePath, buildError.LineNumber, 5); // Example: 5 lines around each error
                    }
                }
                Console.WriteLine();
            }

            return buildResult;
        }
    }
}
