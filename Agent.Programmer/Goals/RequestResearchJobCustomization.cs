using Agent.Core;
using Agent.Services;

namespace Agent.Programmer
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("RequestResearchJobCustomization")]
    public class RequestResearchJobCustomization : AgentGoalCustomization  // TODO gsemple: make these customizations global to the goal tree, since so many common processing routines
    {
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly VisualStudioService _visualStudioService;
        private readonly JobRunner _jobRunner;
        private readonly IServiceProvider _serviceProvider;
        private readonly RepositoryQuerySession _repositoryQuerySession;

        public RequestResearchJobCustomization(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner, IServiceProvider serviceProvider)
        { 
            _codeAnalysisService = codeAnalysisService;
            _visualStudioService = visualStudioService;
            _jobRunner = jobRunner;
            _serviceProvider = serviceProvider;
            _repositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
        }

        public override bool ShouldRequestPrompt(AgentState agentState)
        {
            return true;
        }

        public override void PopulatePrompt(AgentPromptContext promptContext, AgentState agentState)
        {
            var programmerAgentState = (agentState as ProgrammerAgentState);

            var requiredMethodAttributes = new List<string>() { "AgentApi" };
            var agentApiSkeleton = programmerAgentState.GenerateAgentApiSkeleton(requiredMethodAttributes);
            var agentApiSample = _repositoryQuerySession.FindFileInRepo($"{nameof(RepositoryQueryJob)}.cs");
            promptContext.AdditionalData["AgentApiSkeleton"] = agentApiSkeleton;
            promptContext.AdditionalData["AgentApiSample"] = agentApiSample.Contents;
        }

        public override async Task ProcessResponse(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
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
