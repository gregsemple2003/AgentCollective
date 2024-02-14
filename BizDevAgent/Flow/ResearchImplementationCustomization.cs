using Microsoft.Extensions.DependencyInjection;
using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;

namespace BizDevAgent.Flow
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("ResearchImplementationCustomization")]
    public class ResearchImplementationCustomization : AgentGoalCustomization
    {
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly VisualStudioService _visualStudioService;
        private readonly JobRunner _jobRunner;
        private readonly IServiceProvider _serviceProvider;

        public ResearchImplementationCustomization(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner, IServiceProvider serviceProvider) 
        { 
            _codeAnalysisService = codeAnalysisService;
            _visualStudioService = visualStudioService;
            _jobRunner = jobRunner;
            _serviceProvider = serviceProvider;
        }

        public override async Task ProcessResponse(string response, AgentState agentState, IResponseParser languageModelParser)
        {
            if (response.Contains("EvaluateResearch_Option"))
            {
                var x = 3;
            }

            var snippets = languageModelParser.ExtractSnippets(response);

            // Run the research job and gather output
            var researchJobOutput = string.Empty;
            foreach (var snippet in snippets)
            {
                if (snippet.LanguageId == "csharp")
                {
                    var researchClassName = $"{nameof(RepositoryQueryJob)}_{Guid.NewGuid().ToString("N")}";
                    var researchClassSource = _codeAnalysisService.RenameClass(snippet.Contents, nameof(RepositoryQueryJob).ToString(), researchClassName);
                    var researchAssembly = _visualStudioService.InjectCode(researchClassSource);
                    foreach (var type in researchAssembly.GetTypes())
                    {
                        if (type.Name.Contains(researchClassName))
                        {
                            //var researchJob = (Job)ActivatorUtilities.CreateInstance(_serviceProvider, type, LocalRepoPath);
                            //var researchJobResult = await _jobRunner.RunJob(researchJob);
                            //researchJobOutput += researchJobResult.OutputStdOut;
                        }
                    }
                }
            }

            agentState.Observations.Add(new AgentObservation() { Description = researchJobOutput });
        }

    }
}
