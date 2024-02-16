using Microsoft.Extensions.DependencyInjection;
using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;
using BizDevAgent.Utilities;

namespace BizDevAgent.Flow
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("RequestResearchJobCustomization")]
    public class RequestResearchJobCustomization : AgentGoalCustomization
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
            _repositoryQuerySession = GoalTreeContext.Current.RepositoryQuerySession;
        }

        public override bool ShouldRequestCompletion()
        {
            return true;
        }

        public override async Task ProcessResponse(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
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
                            var researchJob = (Job)ActivatorUtilities.CreateInstance(_serviceProvider, type, _repositoryQuerySession.LocalRepoPath);
                            var researchJobResult = await _jobRunner.RunJob(researchJob);
                            researchJobOutput += researchJobResult.OutputStdOut;
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(researchJobOutput))
            {
                agentState.Observations.Add(new AgentObservation() { Description = researchJobOutput });
            }
        }
    }
}
