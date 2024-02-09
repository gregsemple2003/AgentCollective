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

        public ProgrammerImplementFeatureJob(GitAgent gitAgent, CodeQueryAgent codeQueryAgent)
        {
            _gitAgent = gitAgent;
            _codeQueryAgent = codeQueryAgent;
        }

        public async override Task Run()
        {
            // Create a test prompt
            var promptData = new PromptData
            {
                Observations = new List<AgentObservation>
                {
                    new AgentObservation { ObservationIndex = 1, ObservationDetail = "implementation_plan = {}" },
                },
                Actions = new List<AgentAction>
                {
                    new AgentAction { ActionIndex = 1, OptionTitle = "Refactor serialization", OptionDescription = "Change serialization method from JSON to XML." },
                }
            };
            var promptTemplatePath = Path.Combine(Paths.GetProjectPath(), "Config", "Templates", "Goal_RefineImplementationPlan.txt");
            var promptTemplate = File.ReadAllText(promptTemplatePath);
            var promptBuilder = new PromptBuilder(promptTemplate);
            var prompt = promptBuilder.Evaluate(promptData);

            // Create a test goal hierarchy
            var goalTree = new AgentGoal("Implement Feature")
            {
                SubGoals = new List<AgentGoal>
                {
                    new AgentGoal("Create Implementation Plan"),

                    new AgentGoal("Modify Repository"),

                    new AgentGoal("Write Unit Tests"),
                }
            };

            var codeQuerySession = _codeQueryAgent.CreateSession(LocalRepoPath);

            // Clone the repository
            if (!Directory.Exists(LocalRepoPath))
            {
                var cloneResult = await _gitAgent.CloneRepository(GitRepoUrl, LocalRepoPath);
                if (cloneResult.IsFailed)
                {
                    throw new InvalidOperationException("Failed to clone repository");
                }
            }

            // Build and report errors
            var buildResult = await BuildRepository(codeQuerySession);
            if (buildResult.IsFailed) 
            { 
                throw new NotImplementedException("The build failed, we don't deal with that yet.");
            }


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
