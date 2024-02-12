using BizDevAgent.Services;
using BizDevAgent.DataStore;
using BizDevAgent.Model;
using BizDevAgent.Utilities;

namespace BizDevAgent.Jobs
{
    /// <summary>
    /// Update the source code summary database, ensuring summary data is up-to-date.
    /// </summary>
    public class UpdateSourceSummaryDatabaseJob : Job
    {
        private readonly SourceSummaryDataStore _sourceSummaryDataStore;
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly RepositoryQueryService _repositoryQueryService;
        private readonly RepositoryQuerySession _repositoryQuerySession;
        private readonly VisualStudioService _visualStudioService;
        private readonly LanguageModelService _languageAgent;
        private readonly GitService _gitService;

        private const int RequiredSummaryVerison = 1;

        public UpdateSourceSummaryDatabaseJob(SourceSummaryDataStore sourceSummaryDataStore, CodeAnalysisService codeAnalysisService, RepositoryQueryService repositoryQueryService, VisualStudioService visualStudioService, LanguageModelService languageModelService, string localRepoPath) 
        {
            _sourceSummaryDataStore = sourceSummaryDataStore;
            _codeAnalysisService = codeAnalysisService;
            _visualStudioService = visualStudioService;
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
            var totalSourceSize = 0;
            var detailedSummarySourceSize = 0;
            var repositoryPath = _repositoryQuerySession.LocalRepoPath;
            var moduleSummary = new SourceSummary
            {
                Key = _repositoryQuerySession.LocalRepoPath,
                DetailedSummary = "",
                Type = "Repository",
                ChildKeys = new List<string>()
            };

            var path = Environment.CurrentDirectory;
            var repositoryFiles = await _repositoryQuerySession.GetAllRepoFiles();
            foreach (var repositoryFile in repositoryFiles)
            {
                if (repositoryFile.FileName.EndsWith(".cs"))
                {
                    var fileName = Path.GetFileName(repositoryFile.FileName);
                    var fileLastModified = File.GetLastWriteTime(repositoryFile.FileName);

                    // Get file summary
                    var fileSummary = await _sourceSummaryDataStore.Get(repositoryFile.FileName);
                    if (fileSummary != null && fileSummary.LastModified >= fileLastModified && fileSummary.Version >= RequiredSummaryVerison)
                    {
                        // Grab cached file summary if it has not been modified since the last update
                        Console.WriteLine($"{fileName}: not updating, has not been modified");
                    }
                    else
                    {
                        // Rebuild file summary by collapsing methods into comments
                        fileSummary = await BuildFileSummary(repositoryFile, fileName, fileSummary);
                    }

                    totalSourceSize += repositoryFile.Contents.Length;
                    detailedSummarySourceSize += fileSummary.DetailedSummary.Length;

                    moduleSummary.DetailedSummary += fileSummary.BriefSummary;
                    moduleSummary.ChildKeys.Add(fileSummary.Key);
                }
            }

            // Construct file summary for inclusion in the module summary.
            string moduleSummaryPrompt = $"Summarize this C# module in 1-3 sentences (60 words max):\n\n{moduleSummary.DetailedSummary}";
            moduleSummary.BriefSummary = (await _languageAgent.ChatCompletion(moduleSummaryPrompt)).ToString();
            _sourceSummaryDataStore.Add(moduleSummary, shouldOverwrite: true);

            Console.Write($"Module summary complete, totalSourceSize = {totalSourceSize}, detailedSummarySourceSize = {detailedSummarySourceSize}");
        }

        private async Task<SourceSummary> BuildFileSummary(RepositoryFile repositoryFile, string fileName, SourceSummary fileSummary)
        {
            Func<string, string, Task<string>> transformMethodBody = async (originalMethodText, originalBody) =>
            {
                // Use a language model to summarize the method.
                string prompt = $"Summarize this method in 1-3 sentences (60 words max):\n\n{originalMethodText}";
                var chatResult = await _languageAgent.ChatCompletion(prompt);
                var replacementBody = $"// {chatResult.ToString()}";
                if (originalBody.Length < replacementBody.Length)
                {
                    return originalBody;
                }
                return replacementBody;
            };
            var originalSourceLength = repositoryFile.Contents.Length;
            var transformedSource = await _codeAnalysisService.TransformMethodBodies(fileName, repositoryFile.Contents, transformMethodBody);
            var transformedSourceLength = transformedSource.Length;
            var reduction = originalSourceLength - transformedSourceLength;
            var percentageReduction = 100.0 * reduction / originalSourceLength;
            Console.WriteLine($"{fileName}: Old Size: {originalSourceLength} chars, New Size: {transformedSourceLength} chars, Reduction: {reduction} chars ({percentageReduction:F2}%)");

            // Construct file summary for inclusion in the module summary.
            string prompt = $"Summarize this file in 1-3 sentences (60 words max):\n\n{repositoryFile.Contents}";
            var chatResult = await _languageAgent.ChatCompletion(prompt);
            var fileSummaryText = $"{fileName}: {chatResult}\n";
            fileSummary = new SourceSummary
            {
                Key = repositoryFile.FileName,
                DetailedSummary = transformedSource,
                BriefSummary = fileSummaryText,
                Type = "CSharpFile",
                ChildKeys = new List<string>(),
                LastModified = DateTime.Now,
                Version = RequiredSummaryVerison
            };
            _sourceSummaryDataStore.Add(fileSummary, shouldOverwrite: true);
            return fileSummary;
        }
    }
}
