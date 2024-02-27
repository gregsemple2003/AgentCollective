using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;

namespace BizDevAgent.Flow
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("RequestResearchJobAgentGoal")]
    public class RequestResearchJobAgentGoal : ProgrammerAgentGoal
    {
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;

        public RequestResearchJobAgentGoal(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner, IServiceProvider serviceProvider, AgentGoalSpec spec)
            : base(spec)
        { 
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
        }

        protected override bool ShouldRequestPromptCustom(AgentState agentState)
        {
            return true;
        }

        protected override void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState)
        {
            var programmerAgentState = (agentState as ProgrammerAgentState);

            var requiredMethodAttributes = new List<string>() { "AgentApi" };
            var agentApiSkeleton = programmerAgentState.GenerateAgentApiSkeleton(requiredMethodAttributes);
            var agentApiSample = _selfRepositoryQuerySession.FindFileInRepo($"{nameof(RepositoryQueryJob)}.cs");
            promptContext.AdditionalData["AgentApiSkeleton"] = agentApiSkeleton;
            promptContext.AdditionalData["AgentApiSample"] = agentApiSample.Contents;
        }

        protected override async Task ProcessResponseCustom(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            var snippets = languageModelParser.ExtractSnippets(response);
            var programmerAgentState = (agentState as ProgrammerAgentState);

            // Run the research job and gather output
            var researchJobOutput = string.Empty;
            foreach (var snippet in snippets)
            {
                if (snippet.LanguageId == "csharp")
                {
                    researchJobOutput += await programmerAgentState.RunAgentApiJob(snippet);
                }
            }

            if (!string.IsNullOrWhiteSpace(researchJobOutput))
            {
                agentState.Observations.Add(new AgentObservation() { Description = researchJobOutput });
            }
        }
    }
}
