using Agent.Services;

namespace Agent.Programmer
{
    /// <summary>
    /// Update the source code summary database, ensuring summary data is up-to-date.
    /// </summary>
    public class UpdateRepositorySummaryJob : Job
    {
        private readonly RepositorySummaryDataStore _repositorySummaryDataStore;
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly RepositoryQuerySession _repositoryQuerySession;
        private readonly LanguageModelService _languageAgent;

        private const int RequiredSummaryVerison = 1;

        public UpdateRepositorySummaryJob(RepositorySummaryDataStore repositorySummaryDataStore, CodeAnalysisService codeAnalysisService, RepositoryQueryService repositoryQueryService, LanguageModelService languageModelService, string localRepoPath) 
        {
            _repositorySummaryDataStore = repositorySummaryDataStore;
            _codeAnalysisService = codeAnalysisService;
            _languageAgent = languageModelService;

            _repositoryQuerySession = repositoryQueryService.CreateSession(localRepoPath);
        }

        // The next line is line 29 for the purposes of creating a .diff file.
        public override Task UpdateScheduledRunTime()
        {
            ScheduledRunTime = ScheduledRunTime.AddDays(1);
            return Task.CompletedTask;
        }

        public async override Task Run()
        {
            var repositorySummaryProvider = new RepositorySummaryProvider(_repositoryQuerySession, _repositorySummaryDataStore, _languageAgent);
            await repositorySummaryProvider.Refresh();

            //_repositoryQuerySession.PrintRepositoryPathSummary("");
            await _repositoryQuerySession.PrintRepositoryPathSummary("BizDevAgent/Agents");
            await _repositoryQuerySession.PrintRepositoryPathSummary("BizDevAgent/DataStore/FileDataStore.cs");
            await _repositoryQuerySession.PrintRepositoryPathSummary("BizDevAgent/DataStore/FileDataStore.dfdfdfcs");
        }
    }
}
