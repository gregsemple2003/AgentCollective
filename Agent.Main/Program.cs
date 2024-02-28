using Agent.Core;
using Agent.Programmer;
using Agent.Services;
using Agent.BizDev;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Main
{
    class Program
    {
        public static async Task Main()
        {
            // Build the service provider
            var serviceCollection = ServiceConfiguration.ConfigureServices();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var loggerFactory = serviceProvider.GetRequiredService<LoggerFactory>();
            var gameDataStore = serviceProvider.GetRequiredService<GameDataStore>();
            var gameSeriesDataStore = serviceProvider.GetRequiredService<GameSeriesDataStore>();
            var companyDataStore = serviceProvider.GetRequiredService<CompanyDataStore>();
            var jobDataStore = serviceProvider.GetRequiredService<JobDataStore>();
            var websiteDataStore = serviceProvider.GetRequiredService<WebsiteDataStore>();
            var repositorySummaryDataStore = serviceProvider.GetRequiredService<RepositorySummaryDataStore>();
            var jobRunner = serviceProvider.GetRequiredService<JobRunner>();
            var visualStudioService = serviceProvider.GetRequiredService<VisualStudioService>();
            var codeAnalysisService = serviceProvider.GetRequiredService<CodeAnalysisService>();
            var repositoryQueryService = serviceProvider.GetRequiredService<RepositoryQueryService>();
            var languageModelService = serviceProvider.GetRequiredService<LanguageModelService>();
            var gitService = serviceProvider.GetRequiredService<GitService>();
            var assetDataStore = serviceProvider.GetRequiredService<AssetDataStore>();

            // Load baseline required data
            var games = await gameDataStore.LoadAll();
            var companies = await companyDataStore.LoadAll();
            for (int i = 0; i < companies.Count; i++)
            {
                var company = companies[i];
                company.Index = i;
            }
            var jobs = await jobDataStore.LoadAll();

            var ws = new System.Net.WebSockets.ClientWebSocket();

            // Run jobs until we're told to exit
            //await jobRunner.RunJob(new UpdateRepositorySummaryJob(repositorySummaryDataStore, codeAnalysisService, repositoryQueryService, languageModelService, @"c:\Features\BizDevAgent_convertxml"));

            await jobRunner.RunJob(new ProgrammerImplementFeatureJob(gitService, repositoryQueryService, codeAnalysisService, assetDataStore, languageModelService, visualStudioService, serviceProvider, jobRunner, loggerFactory)
            {
                GitRepoUrl = "https://github.com/gregsemple2003/BizDevAgent.git",
                LocalRepoPath = @"c:\Features\BizDevAgent_convertxml",
                //FeatureSpecification = @"Convert any data load or saving functionality from JSON to XML.",
                FeatureSpecification = @"Convert console logging to use the Logger class.",
                BuildCommand = new BatchFileBuildCommand
                {
                    ScriptPath = "Build.bat"
                }
            });
            //await jobRunner.RunJob(new ProgrammerResearchJob(visualStudioService, serviceProvider, jobRunner));
            //await jobRunner.RunJob(new ProgrammerModifyCode(gitService, diffFileContents));
            //await jobRunner.RunJob(new ResearchCompanyJob(companyDataStore, serviceProvider.GetRequiredService<WebSearchAgent>()));
            //await jobRunner.RunJob(new UpdateCompanyWebsitesJob(websiteDataStore, companyDataStore));       
            //await jobRunner.RunJob(new UpdateGameDetailsJob(gameDataStore));
            //await jobRunner.RunJob(new UpdateGameRankingsJob(gameDataStore, gameSeriesDataStore));
            //await jobRunner.Start();
        }
    }
}
