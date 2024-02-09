using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Goals;
using BizDevAgent.Utilities;
using FluentResults;

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
        public IBuildAgent BuildAgent { get; set; }

        private readonly GitAgent _gitAgent;
        private readonly CodeQueryAgent _codeQueryAgent;
        private readonly CodeAnalysisAgent _codeAnalysisAgent;
        private readonly AssetDataStore _assetDataStore;

        public ProgrammerImplementFeatureJob(GitAgent gitAgent, CodeQueryAgent codeQueryAgent, CodeAnalysisAgent codeAnalysisAgent, AssetDataStore assetDataStore)
        {
            _gitAgent = gitAgent;
            _codeQueryAgent = codeQueryAgent;
            _codeAnalysisAgent = codeAnalysisAgent;
            _assetDataStore = assetDataStore;
        }

        public async override Task Run()
        {
            // Define workflow
            var goalTree = CreateImplementFeatureTree();

            // Instantiate agent
            var agentState = new AgentState();
            agentState.Variables.Add(new AgentVariable { Name = "ImplementationPlan", Value = "{}" });
            agentState.Goals.Push(goalTree);
            agentState.Goals.Push(goalTree.SubGoals[0]);

            // Generate prompt for current state
            var prompt = await GeneratePrompt(agentState);

            // Clone the repository
            if (!Directory.Exists(LocalRepoPath))
            {
                var cloneResult = await _gitAgent.CloneRepository(GitRepoUrl, LocalRepoPath);
                if (cloneResult.IsFailed)
                {
                    throw new InvalidOperationException("Failed to clone repository");
                }
            }

            //// Build and report errors
            //var codeQuerySession = _codeQueryAgent.CreateSession(LocalRepoPath);
            //var buildResult = await BuildRepository(codeQuerySession);
            //if (buildResult.IsFailed)
            //{
            //    throw new NotImplementedException("The build failed, we don't deal with that yet.");
            //}
        }

        private async Task<string> GenerateCodeQueryApi()
        {
            var codeQuerySession = _codeQueryAgent.CreateSession(Paths.GetSourceControlRootPath());
            var projectFile = await codeQuerySession.FindFileInRepo("CodeQueryAgent.cs", logError: false);
            var codeQueryApi = _codeAnalysisAgent.GeneratePublicApiSkeleton(projectFile.Contents);
            return codeQueryApi;
        }

        private async Task<string> GeneratePrompt(AgentState agentState)
        {
            var codeQuerySessionApi = await GenerateCodeQueryApi();
            var currentGoal = agentState.Goals.Peek();
            var promptContext = new PromptContext();
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
            return prompt;
        }

        private AgentGoalAsset CreateImplementFeatureTree()
        {
            // Create a test goal hierarchy
            return new AgentGoalAsset("Implement Feature")
            {
                SubGoals = new List<AgentGoalAsset>
                {
                    new AgentGoalAsset("Create Implementation Plan")
                    {
                        PromptBuilder = AssetRef<PromptAsset>("Goal_RefineImplementationPlan"),
                        BaselineActions = new List<AgentActionAsset>
                        {
                            AssetRef<AgentActionAsset>("RefineImplementationPlan"),
                            AssetRef<AgentActionAsset>("ResearchCode"),
                            AssetRef<AgentActionAsset>("RequestHelp")
                        }
                    },

                    new AgentGoalAsset("Modify Repository"),

                    new AgentGoalAsset("Write Unit Tests"),
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
