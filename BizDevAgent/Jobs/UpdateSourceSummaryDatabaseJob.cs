using BizDevAgent.Agents;
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
        private readonly CodeAnalysisAgent _codeAnalysisAgent;
        private readonly VisualStudioAgent _visualStudioAgent;
        private readonly LanguageModelAgent _languageAgent;

        private const int RequiredSummaryVerison = 1;

        public UpdateSourceSummaryDatabaseJob(SourceSummaryDataStore sourceSummaryDataStore, CodeAnalysisAgent codeAnalysisAgent, VisualStudioAgent visualStudioAgent, LanguageModelAgent languageModelAgent) 
        {
            _sourceSummaryDataStore = sourceSummaryDataStore;
            _codeAnalysisAgent = codeAnalysisAgent;
            _visualStudioAgent = visualStudioAgent;
            _languageAgent = languageModelAgent;
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
            // Construct module summary
            var projectPath = Paths.GetProjectPath();
            var moduleSummary = new SourceSummary
            {
                Key = projectPath,
                DetailedSummary = "",
                Type = "CSharpModule",
                ChildKeys = new List<string>()
            };

            var path = Environment.CurrentDirectory;
            var projectFiles = await _visualStudioAgent.LoadProjectFiles(projectPath);
            foreach (var projectFile in projectFiles)
            {
                if (projectFile.FileName.EndsWith(".cs"))
                {
                    var fileName = Path.GetFileName(projectFile.FileName);
                    var fileLastModified = File.GetLastWriteTime(projectFile.FileName);

                    // Get file summary
                    var fileSummary = await _sourceSummaryDataStore.Get(projectFile.FileName);
                    if (fileSummary != null && fileSummary.LastModified >= fileLastModified && fileSummary.Version >= RequiredSummaryVerison)
                    {
                        // Grab cached file summary if it has not been modified since the last update
                        Console.WriteLine($"{fileName}: not updating, has not been modified");
                    }
                    else
                    {
                        // Rebuild file summary by collapsing methods into comments
                        fileSummary = await BuildFileSummary(projectFile, fileName, fileSummary);
                    }

                    totalSourceSize += projectFile.Contents.Length;
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

        private async Task<SourceSummary> BuildFileSummary(ProjectFile projectFile, string fileName, SourceSummary fileSummary)
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
            var originalSourceLength = projectFile.Contents.Length;
            var transformedSource = await _codeAnalysisAgent.TransformMethodBodies(fileName, projectFile.Contents, transformMethodBody);
            var transformedSourceLength = transformedSource.Length;
            var reduction = originalSourceLength - transformedSourceLength;
            var percentageReduction = 100.0 * reduction / originalSourceLength;
            Console.WriteLine($"{fileName}: Old Size: {originalSourceLength} chars, New Size: {transformedSourceLength} chars, Reduction: {reduction} chars ({percentageReduction:F2}%)");

            // Construct file summary for inclusion in the module summary.
            string prompt = $"Summarize this file in 1-3 sentences (60 words max):\n\n{projectFile.Contents}";
            var chatResult = await _languageAgent.ChatCompletion(prompt);
            var fileSummaryText = $"{fileName}: {chatResult}\n";
            fileSummary = new SourceSummary
            {
                Key = projectFile.FileName,
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
