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

        public ProgrammerImplementFeatureJob(GitService gitService, CodeQueryService codeQueryService, CodeAnalysisService codeAnalysisService, AssetDataStore assetDataStore, LanguageModelService languageModelService)
        {
            _gitService = gitService;
            _codeQueryService = codeQueryService;
            _codeAnalysisService = codeAnalysisService;
            _assetDataStore = assetDataStore;
            _languageModelService = languageModelService;
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
            agentState.Goals.Push(goalTree.SubGoals[0]);

            // Get language model to respond to goal prompt
            var generatePromptResult = await GeneratePrompt(agentState);
            var chatResult = await _languageModelService.ChatCompletion(generatePromptResult.Prompt);
            if (chatResult.ChatResult.Choices.Count == 0) 
            {
                throw new InvalidOperationException("The chat API call failed to return a choice.");
            }

            // Figure out which action it is taking.
            var responseTokens = ExtractResponseTokens(chatResult .ChatResult.Choices[0].Message.TextContent);
            if (responseTokens == null || responseTokens.Count != 1)
            {
                throw new InvalidOperationException($"Invalid response tokens: {string.Join(",",responseTokens)}");
            }

            // Process action to change agent state
            AgentAction chosenAction = null;
            foreach (var possibleAction in generatePromptResult.PromptContext.Actions)
            {
                if (responseTokens[0] == possibleAction.PromptTemplatePath)
                {
                    chosenAction = possibleAction;
                }
            }
            if (chosenAction == null)
            {
                throw new InvalidOperationException($"Agent chose an option that was not currently available {responseTokens[0]}");
            }
            foreach(var chosenGoal in chosenAction.Goals) 
            {
                agentState.Goals.Push(chosenGoal);
            }

            // Now loop everything over again


            //// Build and report errors
            //var codeQuerySession = _codeQueryService.CreateSession(LocalRepoPath);
            //var buildResult = await BuildRepository(codeQuerySession);
            //if (buildResult.IsFailed)
            //{
            //    throw new NotImplementedException("The build failed, we don't deal with that yet.");
            //}
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
            var projectFile = await codeQuerySession.FindFileInRepo("codeQueryService.cs", logError: false);
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

            foreach (var baselineAction in currentGoal.BaselineActions)
            {
                baselineAction.Bind(promptContext);
                promptContext.Actions.Add(baselineAction);
            }

            for (var actionIndex = 0; actionIndex < promptContext.Actions.Count; actionIndex++)
            {
                var action = promptContext.Actions[actionIndex];
                action.Index = (actionIndex + 1);
            }

            var prompt = currentGoal.PromptBuilder.Evaluate(promptContext);

            return new GeneratePromptResult
            {
                Prompt = prompt,
                PromptContext = promptContext
            };
        }

        private AgentGoal CreateGoalTree()
        {
            // Create a test goal hierarchy
            return new AgentGoal("Implement Feature")
            {
                SubGoals = new List<AgentGoal>
                {
                    new AgentGoal("Create Implementation Plan")
                    {
                        PromptBuilder = AssetRef<PromptAsset>("Goal_RefineImplementationPlan"),
                        BaselineActions = new List<AgentAction>
                        {
                            AssetRef<AgentAction>("RefineImplementationPlan"),
                            AssetRef<AgentAction>("ResearchImplementation"),
                            AssetRef<AgentAction>("RequestHelp")
                        }
                    },

                    new AgentGoal("Modify Repository"),

                    new AgentGoal("Write Unit Tests"),
                }
            };
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
