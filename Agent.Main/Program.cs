using Agent.Core;
using Agent.Programmer;
using Agent.Services;
using Agent.BizDev;
using Microsoft.Extensions.DependencyInjection;
using GrafanaServiceExample;

namespace Agent.Main
{
    class Program
    {
        public static async Task Main()
		{
			// Build the service provider
			var serviceCollection = ServiceConfiguration.ConfigureServices();
			var serviceProvider = serviceCollection.BuildServiceProvider();

			// Fetch required services
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
			var languageModelService = serviceProvider.GetRequiredService<OpenAiLanguageModel>();
			var anthropicLanguageModel = serviceProvider.GetRequiredService<AnthropicLanguageModel>();
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

			//// GCP query to get VM startup times
			//var gcpOperationsService = new GcpOperationsService("lastepoch-agones-prod");
			//var insertOperations = await gcpOperationsService.GetInsertOperationsAsync(6);
			//gcpOperationsService.QueryStartupTimesByZone(insertOperations);

			// Grafana query to fetch information
			var prometheusService = new PrometheusService("https://last-epoch.gamefabric.dev/observability/metrics", "XADdmtkHmLguHyq5VFaCbeWTiFJDsyIM");
			List<string> armadaSets = new List<string>
			{
				"prod-a",
				"prod-a-towns",
			};
			await QueryClusterCpuByModel_Pre12(prometheusService, armadaSets);
			//await QueryClusterCpuByModel_Post12(prometheusService, armadaSets);
			//         await playerQueueConfigService.ExportAllocatedCpuByModelCsvAsync("all_clusters.csv");
			//await playerQueueConfigService.CalculateDailyPeaks(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
			//         playerQueueConfigService.WriteClustersToCsv(fileName: "clusters.csv");
			//await playerQueueConfigService.GetPeakAllocationRatesByRegion();
			// Run jobs until we're told to exit
			//await jobRunner.RunJob(new UpdateRepositorySummaryJob(repositorySummaryDataStore, codeAnalysisService, repositoryQueryService, languageModelService, @"c:\Features\BizDevAgent_convertxml"));


			//string inputAssetsPath = @"C:\LE_Server_Linux\LastEpoch_Data";
			//string outputAssetsPath = @"C:\LE_Server_Linux\LastEpoch_Data_Stripped";
			//string classDataPath = Path.Combine(@"C:\Backup", "classdata.tpk");
			//UnityAssetService assetService = new UnityAssetService(classDataPath, inputAssetsPath, outputAssetsPath);
			//assetService.ProcessAllAssets();
			////string assetsFilePath = Path.Combine(inputAssetsPath, "sharedassets14.assets");

			//await jobRunner.RunJob(new ProgrammerImplementFeatureJob(gitService, repositoryQueryService, assetDataStore, languageModelService, anthropicLanguageModel, serviceProvider, loggerFactory)
			//{
			//    GitRepoUrl = "https://github.com/gregsemple2003/BizDevAgent.git",
			//    LocalRepoPath = @"c:\Features\BizDevAgent_convertxml",
			//    //FeatureSpecification = @"Convert any data load or saving functionality from JSON to XML.",
			//    FeatureSpecification = @"Convert console logging to use the Logger class.",
			//    BuildCommand = new BatchFileBuildCommand
			//    {
			//        ScriptPath = "Build.bat"
			//    }
			//});
			//await jobRunner.RunJob(new ProgrammerResearchJob(visualStudioService, serviceProvider, jobRunner));
			//await jobRunner.RunJob(new ProgrammerModifyCode(gitService, diffFileContents));
			//await jobRunner.RunJob(new ResearchCompanyJob(companyDataStore, serviceProvider.GetRequiredService<WebSearchAgent>()));
			//await jobRunner.RunJob(new UpdateCompanyWebsitesJob(websiteDataStore, companyDataStore));       
			//await jobRunner.RunJob(new UpdateGameDetailsJob(gameDataStore));
			//await jobRunner.RunJob(new UpdateGameRankingsJob(gameDataStore, gameSeriesDataStore));
			//await jobRunner.Start();
		}

		private static async Task QueryClusterCpuByModel_Pre12(PrometheusService prometheusService, List<string> armadaSets)
		{
			var playerQueueConfigService = new PlayerQueueConfigService(prometheusService, armadaSets, "Output\\");
			var startTime = DateTime.UtcNow.AddDays(-10);
			var endTime = DateTime.UtcNow.AddDays(-3);
			var step = TimeSpan.FromMinutes(15);
			var weightedCpu = await playerQueueConfigService.GetGlobalWeightedCpuAsync(startTime, endTime, step);
		}

		private static async Task QueryClusterCpuByModel_Post12(PrometheusService prometheusService, List<string> armadaSets)
		{
			var playerQueueConfigService = new PlayerQueueConfigService(prometheusService, armadaSets, "Output\\");
			var startTime = DateTime.UtcNow.AddDays(-1);
			var endTime = DateTime.UtcNow.AddDays(0);
			var step = TimeSpan.FromMinutes(15);
			var weightedCpu = await playerQueueConfigService.GetGlobalWeightedCpuAsync(startTime, endTime, step);
		}

	}
}
