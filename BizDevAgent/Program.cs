﻿using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using BizDevAgent.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BizDevAgent.Agents;
using FluentResults;
using System.Runtime;
using System;

class Program
{
    private static string GameSeriesDataPath => Path.Combine(Paths.GetDataPath(), "GameSeriesDB");
    private static string SourceSummaryDataPath => Path.Combine(Paths.GetDataPath(), "SourceSummaryDB");    
    private static string GameDataPath => Path.Combine(Paths.GetDataPath(), "games.json");
    private static string JobDataPath => Path.Combine(Paths.GetDataPath(), "jobs.json");
    private static string CompanyDataPath => Path.Combine(Paths.GetDataPath(), "companies.json");
    private static string WebsiteDataPath => Path.Combine(Paths.GetDataPath(), "CrawlerCacheDB");

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
            var browsingAgent = serviceProvider.GetRequiredService<WebBrowsingAgent>();
            return new CompanyDataStore(CompanyDataPath, browsingAgent);
        });
        serviceCollection.AddSingleton<GameSeriesDataStore>(serviceProvider =>
        {
            return new GameSeriesDataStore(GameSeriesDataPath);
        });
        serviceCollection.AddSingleton<SourceSummaryDataStore>(serviceProvider =>
        {
            return new SourceSummaryDataStore(SourceSummaryDataPath);
        });
        serviceCollection.AddSingleton<GameDataStore>(serviceProvider =>
        {
            var gameSeriesDataStore = serviceProvider.GetRequiredService<GameSeriesDataStore>();
            var browsingAgent = serviceProvider.GetRequiredService<WebBrowsingAgent>();
            return new GameDataStore(gameSeriesDataStore, browsingAgent, GameDataPath);
        });

        // Register jobs 
        Job.RegisterAll(serviceCollection);
        Agent.RegisterAll(serviceCollection);

        // Build the service provider
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var gameDataStore = serviceProvider.GetRequiredService<GameDataStore>();
        var gameSeriesDataStore = serviceProvider.GetRequiredService<GameSeriesDataStore>();
        var companyDataStore = serviceProvider.GetRequiredService<CompanyDataStore>();
        var jobDataStore = serviceProvider.GetRequiredService<JobDataStore>();
        var websiteDataStore = serviceProvider.GetRequiredService<WebsiteDataStore>();
        var sourceSummaryDataStore = serviceProvider.GetRequiredService<SourceSummaryDataStore>();

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
        var jobRunner = serviceProvider.GetRequiredService<JobRunner>();
        var visualStudioAgent = serviceProvider.GetRequiredService<VisualStudioAgent>();
        var codeAnalysisAgent = serviceProvider.GetRequiredService<CodeAnalysisAgent>();
        var codeQueryAgent = serviceProvider.GetRequiredService<CodeQueryAgent>();
        var languageAgent = serviceProvider.GetRequiredService<LanguageModelAgent>();

        //var text = File.ReadAllText(Path.Combine(Paths.GetProjectPath(), "..", "actual.diff"));
        //char[] charArray = text.ToCharArray();

        //await codeQueryAgent.PrintModuleSummary();
        //await codeQueryAgent.PrintFileSkeleton("FileDataStore.cs");
        //await codeQueryAgent.PrintFileContents("FileDataStore.cs");
        //await codeQueryAgent.PrintMatchingSourceLines("*.cs", "jsonconvert", caseSensitive: false, matchWholeWord: true);               

        //await jobRunner.RunJob(new UpdateSourceSummaryDatabaseJob(sourceSummaryDataStore, codeAnalysisAgent, visualStudioAgent, languageAgent));
        //await jobRunner.RunJob(new ProgrammerResearchJob(visualStudioAgent, serviceProvider, jobRunner));
        var diffFilePath = Path.Combine(Paths.GetProjectPath(), "Jobs", "step_1.diff");
        var diffFileContents = File.ReadAllText(diffFilePath);
        if (!diffFileContents.EndsWith(" \n"))
        {
            diffFileContents += " \n"; // Append space followed by newline if not present
        }

        var modifiedDiffFilePath = Path.Combine(Path.GetDirectoryName(diffFilePath), Path.GetFileNameWithoutExtension(diffFilePath) + "_mod.diff");
        File.WriteAllText(modifiedDiffFilePath, diffFileContents);

        //await jobRunner.RunJob(new ProgrammerModifyCode(diffFileContents));

        //await jobRunner.RunJob(new ResearchCompanyJob(companyDataStore, serviceProvider.GetRequiredService<WebSearchAgent>()));
        //await jobRunner.RunJob(new UpdateCompanyWebsitesJob(websiteDataStore, companyDataStore));       
        //await jobRunner.RunJob(new UpdateGameDetailsJob(gameDataStore));
        //await jobRunner.RunJob(new UpdateGameRankingsJob(gameDataStore, gameSeriesDataStore));
        //await jobRunner.Start();
    }
}