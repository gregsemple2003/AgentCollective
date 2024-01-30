using System.Collections.Concurrent;
using BizDevAgent.Model;
using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using BizDevAgent.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

class Program
{
    public const int WorkerCount = 30;

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
        serviceCollection.AddTransient<RankingChangesJob>();
        serviceCollection.AddTransient<TestPeriodicJob>();
        serviceCollection.AddTransient<JobRunner>();

        // Build the service provider
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var gameDataStore = serviceProvider.GetRequiredService<GameDataStore>();
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
        await jobRunner.Start();


        // Instantiate new job and run it
        //var rankingChangesJob = serviceProvider.GetRequiredService<RankingChangesJob>();



        ////await CheckGamesForNewHighs(gameDataStore, gameSeriesDataStore);
        ////await SendTestEmailAsync();

        //jobDataStore.All.Clear();
        //var job = new RankingChangesJob(gameDataStore, gameSeriesDataStore);
        //await jobDataStore.LoadAll();
        //jobDataStore.All.Add(job);
        //await jobDataStore.SaveAll();

        //var companyRegistry = new CompanyDataStore(CompanyDataPath);
        //var companies = await companyRegistry.LoadAll();

        // Update game review counts to time-series db
        //await UpdateGameDetails(gameDataStore);

        //// Update company websites, discovering emails and any other bizdev related needs
        //await UpdateCompanyWebsites(companies);
    }

    private static async Task CheckGamesForNewHighs(GameDataStore gameDataStore, GameSeriesDataStore gameSeriesDataStore)
    {
        var newHighsTask = new RankingChangesJob(gameDataStore, gameSeriesDataStore);
        await newHighsTask.Run();
    }

    private static async Task UpdateGameDetails(GameDataStore gameDataStore)
    {
        // Update game details
        var games = gameDataStore.All;
        Random rnd = new Random();
        TimeSpan medianDelay = TimeSpan.FromSeconds(3);
        TimeSpan radiusDelay = TimeSpan.FromSeconds(1);
        for (int i = 0; i < games.Count; i++)
        {
            var game = games[i];

            Console.WriteLine($"[{i} / {games.Count}] Updating details for '{game.Name}' by '{game.DeveloperName}'");

            // Update the game with information from the steamdb app page
            await gameDataStore.UpdateDetails(game);

            // Throttle the update so we don't impolitely spam the server
            int minDelay = (int)(medianDelay.TotalMilliseconds - radiusDelay.TotalMilliseconds);
            int maxDelay = (int)(medianDelay.TotalMilliseconds + radiusDelay.TotalMilliseconds);
            int delay = rnd.Next(minDelay, maxDelay);
            Console.WriteLine($"Waiting {delay} ms");
            await Task.Delay(delay);

            // TODO gsemple: remove
            await gameDataStore.SaveAll();
        }

        await gameDataStore.SaveAll();
    }

    private static async Task UpdateCompanyWebsites(CompanyDataStore companiesDataStore)
    {
        var companies = companiesDataStore.All;
        var websiteDataStore = new WebsiteDataStore(WebsiteDataPath);

        // Process companies until all websites are crawled.
        var companyQueue = new ConcurrentQueue<Company>(companies);

        // Create a number of workers which drain the queue of work until empty
        var workerTasks = new List<Task>();
        for (int i = 0; i < WorkerCount; i++)
        {
            workerTasks.Add(Task.Run(async () =>
            {
                while (companyQueue.TryDequeue(out var company))
                {
                    var website = await websiteDataStore.Load(company.Url, $"{company.Index} / {companies.Count}");
                    company.Emails = website.ExtractedEmails;
                }
            }));
        }

        // Wait until the work queue is done
        await Task.WhenAll(workerTasks);

        // Save any mutations in companies
        await companiesDataStore.SaveAll();

    }
}