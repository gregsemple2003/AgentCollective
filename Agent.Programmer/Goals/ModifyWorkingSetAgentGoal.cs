using Agent.Core;
using Agent.Services;

namespace Agent.Programmer
{
    /// <summary>
    /// Decide on which files/lines to include in the LLM's context window as part of its "working set".
    /// Everything else from the repository is effectively culled, and the LLM cannot see it or make decisions
    /// on it.
    /// </summary>
    [TypeId("ModifyWorkingSetAgentGoal")]
    public class ModifyWorkingSetAgentGoal : ProgrammerAgentGoal
    {
        private readonly RepositoryQuerySession _targetRepositoryQuerySession;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;
        private readonly IServiceProvider _serviceProvider;

        public ModifyWorkingSetAgentGoal(IServiceProvider serviceProvider, AgentGoalSpec spec)
            : base(spec)
        {
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
            _targetRepositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
            _serviceProvider = serviceProvider;
        }

        protected override bool ShouldRequestPromptCustom(AgentState agentState)
        {
            return true;
        }

        protected override void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState)
        {
            var programmerAgentState = (agentState as ProgrammerAgentState);

            var requiredMethodAttributes = new List<string>() { "WorkingSet" };
            var agentApiSkeleton = programmerAgentState.GenerateAgentApiSkeleton(requiredMethodAttributes);
            var agentApiSample = _selfRepositoryQuerySession.FindFileInRepo($"{nameof(RepositoryQueryJob)}.cs");
            promptContext.AdditionalData["AgentApiSkeleton"] = agentApiSkeleton;
            promptContext.AdditionalData["AgentApiSample"] = agentApiSample.Contents;
        }

        protected override async Task ProcessResponseCustom(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            if (!agentState.TryGetGoal(out var currentGoal)) return;

            var snippets = languageModelParser.ExtractSnippets(response);
            var programmerAgentState = (agentState as ProgrammerAgentState);

            // Clear working set
            programmerAgentState.ProgrammerShortTermMemory.RepositoryQueryEntries.Clear();

            // Run the agent job and gather output
            var researchJobOutput = string.Empty;
            foreach (var snippet in snippets)
            {
                if (snippet.LanguageId == "csharp")
                {
                    var programmerShortTermMemory = agentState.ShortTermMemory as ProgrammerShortTermMemory;
                    _targetRepositoryQuerySession.RepositoryQueryEntries.Clear();
                    await programmerAgentState.RunAgentApiJob(snippet);

                    programmerAgentState.ProgrammerShortTermMemory.RepositoryQueryEntries.AddRange(_targetRepositoryQuerySession.RepositoryQueryEntries);
                    programmerAgentState.ProgrammerShortTermMemory.WorkingSet = await programmerAgentState.GenerateWorkingSet(programmerAgentState.ProgrammerShortTermMemory.RepositoryQueryEntries);
                }
            }

            var responseTokens = languageModelParser.ExtractResponseTokens(response);
            if (snippets.Count == 0 && !responseTokens.Any(t => t.Contains("NoWorkingSet")))
            {
                throw new Exception($"Invalid response, could not find snippet or token");
            }

            if (!string.IsNullOrWhiteSpace(researchJobOutput))
            {
                agentState.Observations.Add(new AgentObservation() { Description = researchJobOutput });
            }

            currentGoal.MarkDone();
        }
    }
}
