using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace BizDevAgent.Flow
{
    /// <summary>
    /// Process the logs that we get from running the research API into short-term memory.
    /// </summary>
    [TypeId("SnythesizeResearchOutputCustomization")]
    public class SnythesizeResearchOutput : AgentGoalCustomization
    {
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly VisualStudioService _visualStudioService;
        private readonly JobRunner _jobRunner;

        public SnythesizeResearchOutput(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner) 
        { 
            _codeAnalysisService = codeAnalysisService;
            _visualStudioService = visualStudioService;
            _jobRunner = jobRunner;
        }

        public override bool ShouldRequestCompletion(AgentState agentState)
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
