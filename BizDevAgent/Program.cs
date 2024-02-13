using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using BizDevAgent.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BizDevAgent.Services;
using FluentResults;
using System.Runtime;
using System;

class Program
{
    private static string GameSeriesDataPath => Path.Combine(Paths.GetDataPath(), "GameSeriesDB");
    private static string SourceSummaryDataPath => Path.Combine(Paths.GetDataPath(), "RepositorySummaryDB");    
    private static string GameDataPath => Path.Combine(Paths.GetDataPath(), "games.json");
    private static string JobDataPath => Path.Combine(Paths.GetDataPath(), "jobs.json");
    private static string CompanyDataPath => Path.Combine(Paths.GetDataPath(), "companies.json");
    private static string WebsiteDataPath => Path.Combine(Paths.GetDataPath(), "CrawlerCacheDB");
    private static string AssetDataPath => Paths.GetAssetsPath();


    public static async Task Main()
    {
        var serviceCollection = new ServiceCollection();

        // Register config
        var builder = new ConfigurationBuilder()
            .SetBasePath(Paths.GetConfigPath())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        IConfiguration configuration = builder.Build();
        serviceCollection.AddSingleton<IConfiguration>(configuration);

        // Register data stores
        serviceCollection.AddSingleton<AssetDataStore>(serviceProvider =>
        {
            return new AssetDataStore(AssetDataPath, serviceProvider);
        });
        serviceCollection.AddSingleton<WebsiteDataStore>(serviceProvider =>
        {
            return new WebsiteDataStore(WebsiteDataPath);
        });
        serviceCollection.AddSingleton<JobDataStore>(serviceProvider =>
        {
            return new JobDataStore(JobDataPath, serviceProvider);
        });
        serviceCollection.AddSingleton<CompanyDataStore>(serviceProvider =>
        {
            var browsingService = serviceProvider.GetRequiredService<WebBrowsingService>();
            return new CompanyDataStore(CompanyDataPath, browsingService);
        });
        serviceCollection.AddSingleton<GameSeriesDataStore>(serviceProvider =>
        {
            return new GameSeriesDataStore(GameSeriesDataPath);
        });
        serviceCollection.AddSingleton<RepositorySummaryDataStore>(serviceProvider =>
        {
            return new RepositorySummaryDataStore(SourceSummaryDataPath);
        });
        serviceCollection.AddSingleton<GameDataStore>(serviceProvider =>
        {
            var gameSeriesDataStore = serviceProvider.GetRequiredService<GameSeriesDataStore>();
            var browsingService = serviceProvider.GetRequiredService<WebBrowsingService>();
            return new GameDataStore(gameSeriesDataStore, browsingService, GameDataPath);
        });

        // Register jobs 
        Job.RegisterAll(serviceCollection);
        Service.RegisterAll(serviceCollection);

        // Build the service provider
        var serviceProvider = serviceCollection.BuildServiceProvider();
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

        // Run jobs until we're told to exit
        //await jobRunner.RunJob(new UpdateRepositorySummaryJob(repositorySummaryDataStore, codeAnalysisService, repositoryQueryService, languageModelService, @"c:\Features\BizDevAgent_convertxml"));

        await jobRunner.RunJob(new ProgrammerImplementFeatureJob(gitService, repositoryQueryService, codeAnalysisService, assetDataStore, languageModelService, visualStudioService, serviceProvider, jobRunner)
        {
            GitRepoUrl = "https://github.com/gregsemple2003/BizDevAgent.git",
            LocalRepoPath = @"c:\Features\BizDevAgent_convertxml",
            FeatureSpecification = @"Convert any data load or saving functionality from JSON to XML.",
            BuildAgent = new BatchFileBuildCommand
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