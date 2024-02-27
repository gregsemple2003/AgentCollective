using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BizDevAgent.Utilities
{
    public static class ServiceConfiguration
    {
        private static string GameSeriesDataPath => Path.Combine(Paths.GetDataPath(), "GameSeriesDB");
        private static string SourceSummaryDataPath => Path.Combine(Paths.GetDataPath(), "RepositorySummaryDB");
        private static string GameDataPath => Path.Combine(Paths.GetDataPath(), "games.json");
        private static string JobDataPath => Path.Combine(Paths.GetDataPath(), "jobs.json");
        private static string CompanyDataPath => Path.Combine(Paths.GetDataPath(), "companies.json");
        private static string WebsiteDataPath => Path.Combine(Paths.GetDataPath(), "CrawlerCacheDB");
        private static string AssetDataPath => Paths.GetAssetsPath();

        public static ServiceCollection ConfigureServices()
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

            var loggerFactory = new LoggerFactory();
            serviceCollection.AddSingleton<LoggerFactory>(loggerFactory);

            // Register jobs 
            Job.RegisterAll(serviceCollection);
            Service.RegisterAll(serviceCollection);
            return serviceCollection;
        }
    }
}
