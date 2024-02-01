using BizDevAgent.Model;
using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using BizDevAgent.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

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
            return new CompanyDataStore(CompanyDataPath);
        });
        serviceCollection.AddSingleton<GameSeriesDataStore>(serviceProvider =>
        {
            return new GameSeriesDataStore(GameSeriesDataPath);
        });
        serviceCollection.AddSingleton<GameDataStore>(serviceProvider =>
        {
            var gameSeriesDataStore = serviceProvider.GetRequiredService<GameSeriesDataStore>();
            return new GameDataStore(gameSeriesDataStore, GameDataPath);
        });

        // Register jobs 
        Job.RegisterAll(serviceCollection);
        serviceCollection.AddTransient<JobRunner>();

        // Build the service provider
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var gameDataStore = serviceProvider.GetRequiredService<GameDataStore>();
        var gameSeriesDataStore = serviceProvider.GetRequiredService<GameSeriesDataStore>();
        var companyDataStore = serviceProvider.GetRequiredService<CompanyDataStore>();
        var jobDataStore = serviceProvider.GetRequiredService<JobDataStore>();

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
        //jobRunner.RunJob(new UpdateGameRankingsJob(gameDataStore, gameSeriesDataStore));
        await jobRunner.Start();
    }
}