using BizDevAgent.Services;
using BizDevAgent.DataStore;
using BizDevAgent.Agents;
using BizDevAgent.Utilities;
using BizDevAgent.Utilities.Commands;
using FluentResults;
using System.Text.RegularExpressions;

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
        private readonly CodeQueryService _codeQueryService;
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly AssetDataStore _assetDataStore;
        private readonly LanguageModelService _languageModelService;
        private readonly AgentGoal _doneGoal;
        private readonly PromptAsset _goalPrompt;

        public ProgrammerImplementFeatureJob(GitService gitService, CodeQueryService codeQueryService, CodeAnalysisService codeAnalysisService, AssetDataStore assetDataStore, LanguageModelService languageModelService)
        {
            _gitService = gitService;
            _codeQueryService = codeQueryService;
            _codeAnalysisService = codeAnalysisService;
            _assetDataStore = assetDataStore;
            _languageModelService = languageModelService;

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
            //var codeQuerySession = _codeQueryService.CreateSession(LocalRepoPath);
            //var buildResult = await BuildRepository(codeQuerySession);
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
                var responseTokens = ExtractResponseTokens(chatResult.ChatResult.Choices[0].Message.TextContent);
                if (responseTokens == null || responseTokens.Count != 1)
                {
                    throw new InvalidOperationException($"Invalid response tokens: {string.Join(",", responseTokens)}");
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

                if (responseTokens[0] == possibleGoal.OptionBuilder.Key)
                {
                    chosenGoal = possibleGoal;
                }
            }

            if (chosenGoal == null)
            {
                if (responseTokens[0] == _doneGoal.OptionBuilder.Key)
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

        private List<string> ExtractResponseTokens(string input)
        {
            var tokens = new List<string>();

            // Regular expression to match tokens starting with @ and followed by alphanumeric characters
            var regex = new Regex(@"@(\w+)");

            // Find matches in the input text
            var matches = regex.Matches(input);

            foreach (Match match in matches)
            {
                // Add the matched token to the list, excluding the @ symbol
                tokens.Add(match.Groups[1].Value);
            }

            return tokens;
        }

        private async Task<string> GenerateCodeQueryApi()
        {
            var codeQuerySession = _codeQueryService.CreateSession(Paths.GetSourceControlRootPath());
            var projectFile = await codeQuerySession.FindFileInRepo("CodeQueryService.cs", logError: false);
            var codeQueryApi = _codeAnalysisService.GeneratePublicApiSkeleton(projectFile.Contents);
            return codeQueryApi;
        }

        public class GeneratePromptResult
        {
            public string Prompt { get; set; }
            public PromptContext PromptContext { get; set; }
        }
        private async Task<GeneratePromptResult> GeneratePrompt(AgentState agentState)
        {
            var promptContext = new PromptContext();

            var codeQuerySessionApi = await GenerateCodeQueryApi();
            var currentGoal = agentState.Goals.Peek();
            promptContext.AdditionalData["CodeQuerySessionApi"] = codeQuerySessionApi;
            promptContext.Variables = agentState.Variables;
            promptContext.Goals = agentState.Goals.Reverse().ToList();
            promptContext.FeatureSpecification = FeatureSpecification;

            promptContext.OptionalSubgoals.Add(_doneGoal);
            foreach (var optionalSubgoal in currentGoal.OptionalSubgoals)
            {
                if (optionalSubgoal.OptionBuilder != null)
                {
                    optionalSubgoal.OptionDescription = optionalSubgoal.OptionBuilder.Evaluate(promptContext);
                }
                if (optionalSubgoal.OptionBuilder != null)
                {
                    optionalSubgoal.StackDescription = optionalSubgoal.StackBuilder.Evaluate(promptContext);
                }
                promptContext.OptionalSubgoals.Add(optionalSubgoal);
            }

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

        private async Task<Result<BuildResult>> BuildRepository(CodeQuerySession codeQuerySession)
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
                        await codeQuerySession.PrintFileContentsAroundLine(buildError.FilePath, buildError.LineNumber, 5); // Example: 5 lines around each error
                    }
                }
                Console.WriteLine();
            }

            return buildResult;
        }
    }
}
