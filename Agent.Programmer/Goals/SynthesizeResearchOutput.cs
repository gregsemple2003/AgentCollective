using Agent.Core;
using Agent.Services;
using Newtonsoft.Json;

namespace Agent.Programmer
{
    /// <summary>
    /// Process the logs that we get from running the research API into short-term memory.
    /// </summary>
    [TypeId("SynthesizeResearchOutputCustomization")]
    public class SynthesizeResearchOutput : AgentGoalCustomization  // TODO gsemple: convert to instancetypeid
    {
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly VisualStudioService _visualStudioService;
        private readonly JobRunner _jobRunner;

        public SynthesizeResearchOutput(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner) 
        { 
            _codeAnalysisService = codeAnalysisService;
            _visualStudioService = visualStudioService;
            _jobRunner = jobRunner;
        }

        public override bool ShouldRequestPrompt(AgentState agentState)
        {
            return true;
        }

        public override Task ProcessResponse(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            var snippets = languageModelParser.ExtractSnippets(response);

            // Run the research job and gather output
            var researchJobOutput = string.Empty;
            foreach (var snippet in snippets)
            {
                if (snippet.LanguageId == "json")
                {
                    // Update short-term memory
                    agentState.ShortTermMemory = JsonConvert.DeserializeObject<ProgrammerShortTermMemory>(snippet.Contents);
                }
            }

            return Task.CompletedTask;
        }

    }
}
