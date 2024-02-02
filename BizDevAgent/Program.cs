using BizDevAgent.Model;
using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using BizDevAgent.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using BizDevAgent.Agents;

class Program
{
    private static string GameSeriesDataPath => Path.Combine(Paths.GetDataPath(), "GameSeriesDB");
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
        await jobRunner.RunJob(new UpdateCompanyWebsitesJob(websiteDataStore, companyDataStore));       
        //await jobRunner.RunJob(new UpdateGameDetailsJob(gameDataStore));
        //await jobRunner.RunJob(new UpdateGameRankingsJob(gameDataStore, gameSeriesDataStore));
        //await jobRunner.Start();
    }
}