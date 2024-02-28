using Agent.Core;
using Agent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Programmer
{
    [TypeId("ProgrammerAgentState")]
    public class ProgrammerAgentState : AgentState
    {
        public override IAgentShortTermMemory ShortTermMemory { get; set; }

        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly VisualStudioService _visualStudioService;
        private readonly JobRunner _jobRunner;
        private readonly IServiceProvider _serviceProvider;
        private readonly RepositoryQuerySession _targetRepositoryQuerySession;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;

        public ProgrammerShortTermMemory ProgrammerShortTermMemory { get { return (ProgrammerShortTermMemory)ShortTermMemory; } }

        public ProgrammerAgentState(CodeAnalysisService codeAnalysisService, VisualStudioService visualStudioService, JobRunner jobRunner, IServiceProvider serviceProvider)
        {
            _codeAnalysisService = codeAnalysisService;
            _visualStudioService = visualStudioService;
            _jobRunner = jobRunner;
            _serviceProvider = serviceProvider;
            _targetRepositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
            ShortTermMemory = new ProgrammerShortTermMemory();
        }

        public string GenerateAgentApiSkeleton(List<string> requiredMethodAttributes)
        {
            var repositoryFile = _selfRepositoryQuerySession.FindFileInRepo($"{nameof(RepositoryQuerySession)}.cs", logError: false);
            var repositoryQueryApi = _codeAnalysisService.GeneratePublicApiSkeleton(repositoryFile.Contents, requiredMethodAttributes);
            return repositoryQueryApi;
        }

        public async Task<string> RunAgentApiJob(ResponseSnippet snippet)
        {
            var researchJobOutput = "";
            var researchClassName = $"{nameof(RepositoryQueryJob)}_{Guid.NewGuid().ToString("N")}";
            var researchClassSource = _codeAnalysisService.RenameClass(snippet.Contents, nameof(RepositoryQueryJob).ToString(), researchClassName);
            var researchAssembly = _visualStudioService.InjectCode(researchClassSource);
            foreach (var type in researchAssembly.GetTypes())
            {
                if (type.Name.Contains(researchClassName))
                {
                    var researchJob = (Job)ActivatorUtilities.CreateInstance(_serviceProvider, type, _targetRepositoryQuerySession.LocalRepoPath);
                    var researchJobResult = await _jobRunner.RunJob(researchJob);
                    researchJobOutput += researchJobResult.OutputStdOut;
                }
            }

            return researchJobOutput;
        }

        public async Task UpdateWorkingSet()
        {
            ProgrammerShortTermMemory.WorkingSet = await GenerateWorkingSet(ProgrammerShortTermMemory.RepositoryQueryEntries);
        }

        public async Task<string> GenerateWorkingSet(List<RepositoryQueryEntry> workingSetEntries)
        {
            var workingSetUpdateJob = (Job)ActivatorUtilities.CreateInstance(_serviceProvider, typeof(WorkingSetUpdateJob), workingSetEntries);
            var result = await _jobRunner.RunJob(workingSetUpdateJob);            
            return result.OutputStdOut;
        }
    }
}
