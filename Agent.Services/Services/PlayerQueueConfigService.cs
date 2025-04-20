using AngleSharp.Dom;
using CsvHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Drawing;
using Agent.Services.Utils;
using System.Text;

namespace Agent.Services
{
    public class Cluster
    {
        public string Name { get; set; }
        public string Type { get; set; } // gcp, bare-metal, gcp-tw
        public string Region { get; set; }
        public string Namespace { get; set; } // last-epoch
        public string Environment { get; set; } // prod, stge
		public List<Node> Nodes { get; set; } = new();
		public Dictionary<DateTime, double> ReadyReplicaCounts { get; set; } = new();
		public Dictionary<DateTime, double> AllocatedReplicaCounts { get; set; } = new();
		public Dictionary<DateTime, double> AllocatedCpuUtilisations { get; set; } = new();
		public Dictionary<DateTime, double> AvgCpuRequestPerPod { get; set; } = new();
		public Dictionary<string, string> DebugValues { get; set; } = new();
		public Dictionary<DateTime, double> PodCounts { get; set; } = new();

		public bool IsBareMetal
		{
			get
			{
				return string.Equals(Type, "bare-metal", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(Type, "baremetal", StringComparison.OrdinalIgnoreCase);
			}
		}

		public double LastPeakCpuRequestPerPod { get; set; }

		/// <summary>
		/// The time of peak allocated replicas on the last day processed by CalculateDailyPeaks.
		/// </summary>
		public DateTime LastPeakAllocatedReplicaTime { get; set; }

		/// <summary>
		/// The count of ready replicas near the peak allocation time on the last day processed by CalculateDailyPeaks.
		/// </summary>
		public int LastPeakReadyReplicaCount { get; set; }

		/// <summary>
		/// The count of peak allocated replicas on the last day processed by CalculateDailyPeaks.
		/// </summary>
		public int LastPeakAllocatedReplicaCount { get; set; }

		/// <summary>
		/// The highest daily peak CPU utilization value found across ALL DAYS processed by CalculateDailyPeaks.
		/// </summary>
		public double PeakAllocatedCpuUtilisation { get; set; }

		/// <summary>
		/// The peak CPU utilization calculated for each specific day
		/// </summary>
		public Dictionary<DateTime, double> DailyPeakAllocatedCpuUtilisations { get; set; } = new();

		/// <summary>
		/// The Allocated‑replica count at the daily peak time for each day.
		/// </summary>
		internal Dictionary<DateTime, int> DailyPeakAllocatedCounts { get; set; } = new();

		/// <summary>
		/// Read-only property that formats the daily peak CPU values into a comma-separated string, ordered by date.
		/// Suitable for CSV export.
		/// </summary>
		public string DailyPeakAllocatedCpuValuesCsv
		{
			get
			{
				if (DailyPeakAllocatedCpuUtilisations == null || !DailyPeakAllocatedCpuUtilisations.Any())
				{
					return string.Empty;
				}

				// CPU values ordered by date ascending
				var valuesStr = string.Join(", ", DailyPeakAllocatedCpuUtilisations
					.OrderBy(kvp => kvp.Key) // Order by date
					.Select(kvp => kvp.Value.ToString("F4", CultureInfo.InvariantCulture)));
				return $"[{valuesStr}]";
			}
		}

		/// <summary>
		/// Daily peak Allocated counts as a CSV string, ordered by date.
		/// </summary>
		internal string DailyPeakAllocatedCountsCsv
		{
			get
			{
				if (DailyPeakAllocatedCounts == null || !DailyPeakAllocatedCounts.Any())
					return string.Empty;

				var valuesStr = string.Join(", ",
					DailyPeakAllocatedCounts
						.OrderBy(kvp => kvp.Key)   // date ascending
						.Select(kvp => kvp.Value));

				return $"[{valuesStr}]";
			}
		}

		public bool TryGetNodeByName(string nodeName, out Node node, bool allowCreate = false)
        {
            node = null;

            // Reject creates for invalid names
            if (allowCreate && string.IsNullOrWhiteSpace(nodeName))
            {
                return false;
            }

            // Attempt lookup
            node = Nodes.Find(node => node.Name == nodeName);
            if (node == null)
            {
                if (allowCreate)
                {
                    node = new Node();
                    node.Name = nodeName;
                    Nodes.Add(node);
                }
            }

            return node != null;
        }

        public override string ToString()
        {
            return $"{Name}, Nodes = {Nodes.Count}";
        }
    }

	public delegate bool ClusterPredicate(Cluster c);

	public class Node
    {
        public string Name { get; set; }

        /// <summary>
        /// Ratio of CPU actually being used to the total CPU request across the entire node
        /// </summary>
        public Dictionary<DateTime, double> CpuActualToCpuRequest { get; set; }

        /// <summary>
        /// Number of pods (game server) running on the node
        /// </summary>
        public Dictionary<DateTime, double> PodCounts { get; set; }

        public Node()
        {
            CpuActualToCpuRequest = new Dictionary<DateTime, double>();
            PodCounts = new Dictionary<DateTime, double>();
        }

        public override string ToString()
        {
            return $"{Name}";
        }
    }

    public class PlayerQueueConfigService
    {
		private readonly PrometheusService _prometheusService;
		private readonly Dictionary<string, Cluster> _clusters;
        private readonly List<string> _armadaSets;

        private DateTime _startTime;
		private string _prometheusBaseUrl;
		private string _prometheusToken;
		private string _rootPath;


		public PlayerQueueConfigService(PrometheusService prometheusService, List<string> armadaSets, string rootPath)
		{
			_prometheusService = prometheusService;
			_clusters = new Dictionary<string, Cluster>();
			_armadaSets = armadaSets;
			_rootPath = rootPath;
		}

		/// <summary>
		/// Calls <see cref="GetAllocatedCpuPerModelAsync"/> for the two cluster
		/// prefixes we care about ("le-prod" and "ni-prod") and writes the results
		/// to <paramref name="fileName"/> as CSV.
		/// </summary>
		public async Task ExportAllocatedCpuByModelCsvAsync(string fileName)
		{
			var prefixes = new[] { "le-prod", "ni-prod" };
			var rows = new List<AllocatedCpuRow>();

			foreach (var prefix in prefixes)
			{
				var perModel = await GetAllocatedCpuPerModelAsync(prefix);

				foreach (var kvp in perModel)
				{
					rows.Add(new AllocatedCpuRow
					{
						ClusterPrefix = prefix,
						ModelName = kvp.Key,
						CpuUsage = kvp.Value
					});
				}
			}

			var csv = ExportListToCsv(rows);
			File.WriteAllText(fileName, csv);
		}

		/// <summary>
		/// Generates <c>visualize_daily_peaks.kql</c> from the in‑memory
		/// <see cref="_dailyPeakRows"/> list.  The KQL produces a time‑chart
		/// of daily peak CPU cores per Allocated server, one series per cluster.
		/// </summary>
		public void WriteDailyPeaksKql()
		{
			if (_dailyPeakRows.Count == 0)
			{
				Console.WriteLine("No daily‑peak rows in memory; KQL not written.");
				return;
			}

			var sb = new StringBuilder();
			sb.AppendLine("// Auto‑generated KQL — daily peak CPU per Allocated server");
			sb.AppendLine("let peaks = datatable (");
			sb.AppendLine("    Cluster:string,");
			sb.AppendLine("    Time:datetime,");
			sb.AppendLine("    PeakCpu:double,");
			sb.AppendLine("    PeakAllocatedCount:int,");
			sb.AppendLine("    PeakDate:string");
			sb.AppendLine(")[");
			foreach (var r in _dailyPeakRows
							   .OrderBy(r => r.Cluster)
							   .ThenBy(r => r.PeakTimeUtc))
			{
				sb.AppendLine(
					$"    \"{r.Cluster}\", datetime({r.PeakTimeUtc:u}), {r.PeakCpu:F6}, {r.PeakAllocatedCount}, \"{r.Date}\",");
			}
			sb.AppendLine("];");
			sb.AppendLine();
			sb.AppendLine("// Default view: time chart of CPU usage");
			sb.AppendLine("peaks");
			sb.AppendLine("| project Time, Cluster, PeakCpu");
			sb.AppendLine("| order by Time asc");
			sb.AppendLine("| render timechart");

			var outPath = Path.Combine(_rootPath, "visualize_daily_peaks.kql");
			File.WriteAllText(outPath, sb.ToString());
			Console.WriteLine($"Wrote daily‑peaks KQL → {outPath}");
		}

		private class AllocatedCpuRow
		{
			public string ClusterPrefix { get; set; }
			public string ModelName { get; set; }
			public double CpuUsage { get; set; }
		}

		public async Task<Dictionary<string, double>> GetGlobalWeightedCpuAsync(
			DateTime startTime,
			DateTime endTime,
			TimeSpan step,
			TimeSpan slice
		)
		{
			await UpdateClusterArmadaInfo(startTime, endTime);
			await UpdateClusterReplicaCounts(startTime, endTime);

			var all = new List<CpuSample>();          // (region, model) rows

			foreach (var cluster in _clusters.Values)
			{
				if (!cluster.IsBareMetal) continue;

				if (cluster.AllocatedReplicaCounts.Count > 0)
				{
					all.AddRange(await QueryRegionModelCpuAsync(cluster.Name, startTime, endTime, step, slice));
				}
			}

			// raw per‑cluster / per‑region data
			DumpListToCsvFile(all, Path.Combine(_rootPath, "server_cpu_by_region.csv"));

			// reduce the time‑series to **one row per cluster / region / model**
			var perCluster = all
				.GroupBy(r => (r.ClusterName, r.Region, r.ModelName))
				.Select(g => g
					.OrderByDescending(r => r.SampleTimeUtc)    // newest sample first
					.First())                                   // keep ONE row
				.ToList();

			// weighted average per (region, model)
			var regionModelCpu = perCluster
				.GroupBy(r => (r.Region, r.ModelName))
				.ToDictionary(
					g => $"{g.Key.Region}|{g.Key.ModelName}",
					g =>
					{
						var tot = g.Sum(s => s.ClusterServerCount);
						return tot == 0
							   ? 0
							   : g.Sum(s => s.AvgCpuPerServer * s.ClusterServerCount) / tot;
					});

			// summary CSV with separate Region / ModelName columns
			var summaryRows = regionModelCpu
				.Select(kvp =>
				{
					var parts = kvp.Key.Split('|', 2);   // "region|model"
					return new
					{
						Region = parts[0],
						ModelName = parts.Length > 1 ? parts[1] : "",
						WeightedCpu = kvp.Value
					};
				})
				.OrderBy(r => r.Region)
				.ThenBy(r => r.ModelName)
				.ToList();

			DumpListToCsvFile(summaryRows, Path.Combine(_rootPath, "model_region_cpu.csv"));

			return regionModelCpu;          // key = "region|model", value = weighted CPU
		}


		public sealed record CpuSample(
			string Region,
			string ModelName,
			double AvgCpuPerServer,
			int ClusterServerCount,
			string ClusterName,
			DateTime SampleTimeUtc
		);

		private async Task<List<CpuSample>> QueryRegionModelCpuAsync(
				string clusterName,
				DateTime startUtc,
				DateTime endUtc,
				TimeSpan step,
				TimeSpan slice
		)
		{
			Console.WriteLine($"QueryRegionModelCpuAsync: clusterName={clusterName}, startUtc={startUtc}, endUtc={endUtc}, step={step}");

			// load template that contains <CLUSTERNAME>
			var promptPath = Path.Combine(_rootPath, "Config/RegionModelCpu.promql");
			var promql = await File.ReadAllTextAsync(promptPath);
			var query = promql.Replace("<CLUSTERNAME>", clusterName);

			var frames = await _prometheusService.ExecuteQueryRangeSlicedAsync(startUtc, endUtc, query, step, slice);

			var samples = new List<CpuSample>();

			foreach (var group in frames.GroupBy(f => (
						 region: f.Dimensions["region"],
						 model: f.Dimensions["model_name"],
						 stat: f.Dimensions["stat"])))
			{
				double windowAvg = group.SelectMany(f => f.Series).Average();
				DateTime latestTs = group.SelectMany(f => f.Timestamps).Max();   // latest in slice

				samples.Add(new CpuSample(
					Region: group.Key.region,
					ModelName: group.Key.model,
					AvgCpuPerServer: group.Key.stat == "avg" ? windowAvg : 0,
					ClusterServerCount: group.Key.stat == "count" ? (int)windowAvg : 0,
					ClusterName: clusterName,
					SampleTimeUtc: latestTs
				));
			}

			// collapse count+avg rows into a single row per (region,model)
			// collapse avg+count rows into ONE sample per (region, model)
			return samples
				.GroupBy(s => (s.Region, s.ModelName))
				.Select(g =>
				{
					// split into two lookup tables: ts → value
					var avgDict = g.Where(s => s.AvgCpuPerServer > 0)
									 .ToDictionary(s => s.SampleTimeUtc,
												   s => s.AvgCpuPerServer);

					var cntDict = g.Where(s => s.ClusterServerCount > 0)
									 .ToDictionary(s => s.SampleTimeUtc,
												   s => s.ClusterServerCount);

					// intersection of timestamps
					var latestTs = avgDict.Keys.Intersect(cntDict.Keys).Max();

					return new CpuSample(
						Region: g.Key.Region,
						ModelName: g.Key.ModelName,
						AvgCpuPerServer: avgDict[latestTs],
						ClusterServerCount: cntDict[latestTs],
						ClusterName: clusterName,
						SampleTimeUtc: latestTs);
				})
				.ToList();

		}

		/// <summary>
		/// Reads the PROMQL from 'AllocatedCpuQuery.promql', substitutes the
		/// cluster prefix, executes it as an instant query, and returns the
		/// utilisation value per CPU‑model.
		/// </summary>
		public async Task<Dictionary<string, double>> GetAllocatedCpuPerModelAsync(string clusterPrefix)
		{
			const string queryFile = "C:\\Users\\Admin\\Documents\\Projects\\AgentCollective\\Agent.Services\\Config\\AllocatedCpuQuery.promql";

			if (!File.Exists(queryFile))
				throw new FileNotFoundException($"Query file '{queryFile}' not found.");

			var template = await File.ReadAllTextAsync(queryFile);
			var query = template.Replace("<PFX>", clusterPrefix);

			var dataFrames = await _prometheusService.ExecuteInstantQueryAsync(query);

			var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			foreach (var dataFrame in dataFrames)
			{
				if (dataFrame.Dimensions.TryGetValue("model_name", out var model) && dataFrame.Series.Count > 0)
					dict[model] = dataFrame.Series[0];
			}

			return dict;
		}

		public async Task CalculateDailyPeaks(DateTime overallStartTime, DateTime overallEndTime, ClusterPredicate pred = null)
		{
			using var _ = ScopedTimer.Measure(new { overallStartTime, overallEndTime });

			// Ensure start/end times are aligned to UTC days for clarity
			overallStartTime = overallStartTime.Date; // Start at midnight UTC
			overallEndTime = overallEndTime.Date.AddDays(1).AddTicks(-1); // End just before midnight UTC of the next day

			// Get Cluster Info (only need to do this once)
			await UpdateClusterArmadaInfo(overallStartTime, overallEndTime);

			// Fetch Replica Counts for the entire period (more efficient than daily queries)
			await UpdateClusterReplicaCounts(overallStartTime, overallEndTime);

			await UpdateClusterCpuRequests(overallStartTime, overallEndTime);

			await UpdateClusterPodCounts(overallStartTime, overallEndTime);

			// Process each day within the range
			for (DateTime currentDay = overallStartTime; currentDay <= overallEndTime; currentDay = currentDay.AddDays(1))
			{
				DateTime dayStart = currentDay;
				DateTime dayEnd = currentDay.AddDays(1).AddTicks(-1); // End of the current day

				Console.WriteLine($"--- Processing Day: {dayStart:yyyy-MM-dd} ---");

				foreach (var cluster in FilterClusters(pred))

				{
					// Calculate the peak for *this specific day* using the pre-fetched replica counts
					await CalculateAndStoreDailyPeakCpu(cluster, dayStart, dayEnd, TimeSpan.FromMinutes(20));
				}
			}

			// Optional: Calculate overall peak across all days if needed (using the stored daily peaks or re-evaluating full data)
			// CalculateOverallPeakMetrics(); // Implement this if you still need the single 'overall' peak values
			WriteClustersToCsv(fileName: "clusters.csv");

			DumpListToCsvFile(_dailyPeakRows.OrderBy(r => r.Cluster).ThenBy(r => r.Date).ToList(), Path.Combine(_rootPath, "daily_cluster_peaks.csv"));

			WriteDailyPeaksKql();

			Console.WriteLine("Daily peak calculation complete.");
		}

		/// <summary>
		/// *Methodology*
		/// 
		/// Our capacity planning is much closer to arithmetic mean CPU usage since the worst-cases (player inviting a party)
		/// tend to be improbable, and we do leave 20-30% capacity per machine unused.  So we average each day's CPU, then 
		/// take the max PeakAllocatedCpuUtilisation over a given time period, so that we ensure our mean CPU usage should suffice
		/// over that time period (e.g. 7 days).
		/// 
		/// For a given cluster and day we sample the average CPU usage rate of the allocated pods at peak usage.
		///    1. Determines the time where peak allocated replicas occurs.  This is the best time for us to sample CPU usage.
		///    2. Uses a Prometheus query to get the average CPU usage rate of the top N pods (where N = allocated replica count).
		///    3. Stores the result per cluster in PeakAllocatedCpuUtilisation.
		///    
		/// We don't have a simple way to just query CPU usage of only allocated pods, so we use topk here as a workaround.
		/// This assumes that allocated containers use more cpu than ready containers, which except for bugs is true.
		/// Manually sanity-check the data derived from this routine.
		/// </summary>
		private async Task CalculateAndStoreDailyPeakCpu(Cluster cluster, DateTime dayStart, DateTime dayEnd, TimeSpan timeAroundPeak)
		{
			using var _ = ScopedTimer.Measure(new { cluster = cluster.Name, dayStart, dayEnd });

			string clusterName = cluster.Name; // Use the already normalized name
			DateTime dayDate = dayStart.Date; // Key for the dictionary

			// Filter the cluster's replica counts to *only* include data for the current day
			var dailyAllocatedCounts = cluster.AllocatedReplicaCounts
				.Where(kvp => kvp.Key >= dayStart && kvp.Key <= dayEnd)
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			var dailyReadyCounts = cluster.ReadyReplicaCounts
				.Where(kvp => kvp.Key >= dayStart && kvp.Key <= dayEnd)
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

			// Check if we have any allocation data for this day
			if (dailyAllocatedCounts == null || dailyAllocatedCounts.Count == 0)
			{
				Console.WriteLine($"CalculateDailyPeakCpu [{dayDate:yyyy-MM-dd}]: Cluster '{clusterName}' has no AllocatedReplicaCounts data for this day. Setting daily peak CPU to 0.");
				cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = 0;
				return;
			}

			// Find the peak allocation time and count *for this day*
			DateTime dailyPeakAllocatedTime = default;
			int dailyPeakAllocatedCount = 0;
			foreach (var kvp in dailyAllocatedCounts)
			{
				// Use Math.Ceiling for peak count to be slightly more conservative with topk
				int currentCount = (int)Math.Ceiling(kvp.Value);
				if (currentCount > dailyPeakAllocatedCount)
				{
					dailyPeakAllocatedCount = currentCount;
					dailyPeakAllocatedTime = kvp.Key;
				}
				// If counts are equal, prefer the later time within the day? (Optional refinement)
				// else if (currentCount == dailyPeakAllocatedCount && kvp.Key > dailyPeakAllocatedTime)
				// {
				//     dailyPeakAllocatedTime = kvp.Key;
				// }
			}

			// Check valid peak time for the day
			if (dailyPeakAllocatedTime == default(DateTime))
			{
				Console.WriteLine($"CalculateDailyPeakCpu [{dayDate:yyyy-MM-dd}]: Could not determine PeakAllocatedReplicaTime for cluster '{clusterName}' for this day (maybe counts were zero). Setting daily peak CPU to 0.");
				cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = 0;
				return;
			}

			double avgRequestAtPeak =
			cluster.PodCounts.TryGetValue(dailyPeakAllocatedTime, out var pods) && pods > 0
				? cluster.AllocatedCpuUtilisations[dailyPeakAllocatedTime] / pods
				: 0;
			cluster.LastPeakCpuRequestPerPod = avgRequestAtPeak;

			// Find peak ready count at the *daily* peak allocated time
			int dailyPeakReadyCount = 0;
			// Find the closest ready count measurement to the peak allocated time for the day
			if (dailyReadyCounts.Any())
			{
				var closestReadyEntry = dailyReadyCounts
					.OrderBy(kvp => Math.Abs((kvp.Key - dailyPeakAllocatedTime).TotalSeconds))
					.FirstOrDefault();
				// Check if the closest entry is reasonably close (e.g., within one step interval)
				if (closestReadyEntry.Key != default(DateTime) && Math.Abs((closestReadyEntry.Key - dailyPeakAllocatedTime).TotalMinutes) <= 15) // Assuming 15 min step
				{
					dailyPeakReadyCount = (int)Math.Round(closestReadyEntry.Value);
				}
				else
				{
					Console.WriteLine($"Warning [{dayDate:yyyy-MM-dd}]: Could not find a ReadyReplicaCount close enough to the daily PeakAllocatedReplicaTime {dailyPeakAllocatedTime} in cluster '{clusterName}'. Setting daily PeakReadyReplicaCount to 0 for logging purposes.");
				}
			}
			else
			{
				Console.WriteLine($"Warning [{dayDate:yyyy-MM-dd}]: No ReadyReplicaCounts found for cluster '{clusterName}' on this day.");
			}


			Console.WriteLine($"Cluster '{clusterName}' [{dayDate:yyyy-MM-dd}]: Daily Peak Allocated Replicas = {dailyPeakAllocatedCount} at {dailyPeakAllocatedTime}. Ready Replicas (approx) = {dailyPeakReadyCount}.");

			cluster.DailyPeakAllocatedCounts[dayDate] = dailyPeakAllocatedCount;

			// If peak count is 0, the CPU query is invalid/meaningless.
			if (dailyPeakAllocatedCount <= 0)
			{
				Console.WriteLine($"CalculateDailyPeakCpu [{dayDate:yyyy-MM-dd}]: Daily PeakAllocatedReplicaCount is {dailyPeakAllocatedCount} for cluster '{clusterName}'. Setting daily peak CPU to 0.");
				cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = 0;
				return;
			}

			// --- Query Prometheus for CPU data around the daily peak time ---
			//var queryTemplate = @"
			//       avg(
			//         topk(<ALLOCATEDREPLICACOUNT>,
			//        rate(agones_cluster_pod_cpu:container_cpu_usage_seconds_total{
			//          cluster=""<CLUSTERNAME>"",
			//          pod=~"".*prod-a.*""
			//        }[5m])
			//         )
			//       )
			//         ";
			// NOTE: We're using the worker pattern match to include only containers running on game servers.  
			var queryTemplate = @"
				avg(
				  topk(<ALLOCATEDREPLICACOUNT>,
					rate(agones_cluster_pod_cpu:container_cpu_usage_seconds_total{
					  cluster=""<CLUSTERNAME>"",
					  namespace=""last-epoch"",
					  node=~"".*worker.*"",
					  pod=~"".*prod-a.*""
					}[5m])
				  )
				) without (pod, node)
			";
			//var queryTemplate = @"
			//        rate(agones_cluster_pod_cpu:container_cpu_usage_seconds_total{
			//          cluster=""<CLUSTERNAME>"",
			//          pod=~"".*prod-a.*""
			//        }[5m])
			//         ";
			var query = queryTemplate
				.Replace("<CLUSTERNAME>", clusterName)
				.Replace("<ALLOCATEDREPLICACOUNT>", dailyPeakAllocatedCount.ToString());


			// Define the time window for the CPU query centered around the *daily* peak
			DateTime peakStartTime = dailyPeakAllocatedTime - timeAroundPeak;
			DateTime peakEndTime = dailyPeakAllocatedTime + timeAroundPeak;
			TimeSpan step = TimeSpan.FromMinutes(1); // Use smaller step for averaging around peak
			//cluster.DebugValues[$"AllocationsQuery_{dayDate:yyyyMMdd}"] = query; // Store query per day if needed

			try
			{
				var dataFrames = await _prometheusService.ExecuteQueryRangeAsync(peakStartTime, peakEndTime, query, step);

				if (dataFrames == null || !dataFrames.Any())
				{
					Console.WriteLine($"CalculateDailyPeakCpu [{dayDate:yyyy-MM-dd}]: No CPU data returned for cluster '{clusterName}' around peak time {dailyPeakAllocatedTime}. Setting daily peak CPU to 0.");
					cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = 0;
					return;
				}
				if (dataFrames.Count > 1)
				{
					Console.WriteLine($"Warning [{dayDate:yyyy-MM-dd}]: CPU query for cluster '{clusterName}' returned multiple ({dataFrames.Count}) data frames. Using the first one.");
					// Consider logging details of extra frames if this happens.
				}


				var dataFrame = dataFrames.First();
				if (dataFrame.Values == null || !dataFrame.Values.Any())
				{
					Console.WriteLine($"CalculateDailyPeakCpu [{dayDate:yyyy-MM-dd}]: CPU data frame for cluster '{clusterName}' has no values. Setting daily peak CPU to 0.");
					cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = 0;
					return;
				}

				// Store the detailed CPU utilization data for this window if needed (optional)
				// cluster.AllocatedCpuUtilisations = dataFrame.AsDictionary(); // This would overwrite daily, maybe merge or store differently if needed

				// Calculate the average CPU utilization *within the queried window*
				var cpuValuesInWindow = dataFrame.AsDictionary()
					 .Where(kvp => kvp.Key >= peakStartTime && kvp.Key <= peakEndTime) // Redundant filter? AsDictionary should only contain window data. Safety check.
					 .Select(kvp => kvp.Value);

				if (cpuValuesInWindow.Any())
				{
					double dailyPeakCpu = cpuValuesInWindow.Average();
					cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = dailyPeakCpu;
					_dailyPeakRows.Add(new DailyPeakRow(
						Cluster: clusterName,
						Date: dayDate.ToString("yyyy-MM-dd"),
						PeakCpu: dailyPeakCpu,
						PeakAllocatedCount: dailyPeakAllocatedCount,
						PeakTimeUtc: dailyPeakAllocatedTime)
					);

					Console.WriteLine($"Cluster '{clusterName}' [{dayDate:yyyy-MM-dd}]: Calculated Daily Peak CPU Utilisation = {dailyPeakCpu:F4}");
				}
				else
				{
					Console.WriteLine($"CalculateDailyPeakCpu [{dayDate:yyyy-MM-dd}]: No CPU values found within the specific window [{peakStartTime} - {peakEndTime}] for cluster '{clusterName}'. Setting daily peak CPU to 0.");
					cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = 0;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error querying/processing CPU data for cluster '{clusterName}' on {dayDate:yyyy-MM-dd}: {ex.Message}");
				cluster.DailyPeakAllocatedCpuUtilisations[dayDate] = -1; // Indicate error state? Or 0?
			}

			// Recalculate overall averages
			var validDailyPeaks = cluster.DailyPeakAllocatedCpuUtilisations
										 .Where(kvp => kvp.Value >= 0) // Exclude error indicators
										 .Select(kvp => kvp.Value);
			cluster.LastPeakAllocatedReplicaCount = dailyPeakAllocatedCount;
			cluster.LastPeakReadyReplicaCount = dailyPeakReadyCount;
			cluster.LastPeakAllocatedReplicaTime = dailyPeakAllocatedTime;
			if (validDailyPeaks.Any())
			{
				cluster.PeakAllocatedCpuUtilisation = validDailyPeaks.Max();
			}
			else
			{
				cluster.PeakAllocatedCpuUtilisation = 0;
				Console.WriteLine($"Cluster '{cluster.Name}': No valid daily peak CPU values found to calculate overall peak. Setting to 0.");
			}
		}

		/// <summary>
		/// Row used for the daily peaks CSV export.
		/// </summary>
		private sealed record DailyPeakRow(
			string Cluster,
			string Date,
			double PeakCpu,
			int PeakAllocatedCount,
			DateTime PeakTimeUtc
		);
		private readonly List<DailyPeakRow> _dailyPeakRows = new();

		public async Task WriteClustersToCsv(string fileName)
        {
			var csv = ExportListToCsv(_clusters.Values.ToList());
			Console.WriteLine(csv);

            File.WriteAllText(fileName, csv);
		}

		/// <summary>
		/// Queries Grafana with a Prometheus data source for time-series data on 'allocated' and 'ready' replica 
		/// counts for a specific Agones fleet across all known clusters.  This data is stored as a time-series
		/// in AllocatedReplicaCounts and ReadyReplicaCounts.
		/// </summary>
		/// <returns></returns>
		public async Task UpdateClusterReplicaCounts(DateTime startTime, DateTime endTime)
		{
			using var _ = ScopedTimer.Measure(new { startTime, endTime });

			var queryTemplate = @"
				sum by (type, cluster) (
					agones_fleets_replicas_count{type=~""allocated|ready"", name=""prod-a""}
				)
			";

			var step = TimeSpan.FromMinutes(15); // Keep reasonable step for granularity
			var dataFrames = await _prometheusService.ExecuteQueryRangeAsync(startTime, endTime, queryTemplate, step);

			foreach (var dataFrame in dataFrames)
			{
				var clusterName = NormalizeClusterName(dataFrame.Dimensions["cluster"]);

				if (TryGetClusterByName(clusterName, out var cluster))
				{
					var type = dataFrame.Dimensions["type"];
					var counts = dataFrame.AsDictionary(); // Contains data for the *entire* period

					if (type == "allocated")
					{
						// Overwrite or merge? Overwrite seems fine as we call this once per run.
						cluster.AllocatedReplicaCounts = counts;
					}
					else if (type == "ready")
					{
						cluster.ReadyReplicaCounts = counts;
					}
				}
				else
				{
					Console.WriteLine($"Warning: Replica count data found for unknown cluster '{clusterName}' (normalized from '{dataFrame.Dimensions["cluster"]}'). Skipping.");
				}
			}
		}

		public async Task UpdateClusterCpuRequests(DateTime startTime, DateTime endTime)
		{
			using var _ = ScopedTimer.Measure(new { startTime, endTime });

			const string query = @"
			sum by (cluster) (
			  agones_cluster_node_cpu_requests:kube_pod_container_resource_requests:sum{
				namespace=""last-epoch"",
				node=~"".*worker.*""
			  }
			)";

			var step = TimeSpan.FromMinutes(15);
			var frames = await _prometheusService.ExecuteQueryRangeAsync(startTime, endTime, query, step);

			foreach (var f in frames)
				if (TryGetClusterByName(NormalizeClusterName(f.Dimensions["cluster"]), out var c))
					c.AllocatedCpuUtilisations = f.AsDictionary();        // reuse existing dict
		}

		public async Task UpdateClusterPodCounts(DateTime startTime, DateTime endTime)
		{
			using var _ = ScopedTimer.Measure(new { startTime, endTime });

			var step = TimeSpan.FromMinutes(15);

			foreach (var (name, cluster) in _clusters)
			{
				var query = $@"
					count by (node) (
					  agones_cluster_pod_cpu:container_cpu_usage_seconds_total{{
						cluster=""{name}"",
					  }}
					)";

				using var _inner = ScopedTimer.Measure(new { clusterName = name, clusterType = cluster.Type});
				var frames = await _prometheusService.ExecuteQueryRangeSlicedAsync(startTime, endTime, query, step, slice: TimeSpan.FromHours(24));
				cluster.PodCounts = frames
					.SelectMany(f => f.AsDictionary())
					.GroupBy(kvp => kvp.Key)          // timestamp
					.ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
			}
		}

		public async Task UpdateClusterAvgCpuRequests(DateTime startTime, DateTime endTime)
		{
			//					cluster=~""ni-prod-usdal-sco01""

			const string query = @"
				count by (node) (agones_cluster_pod_cpu:container_cpu_usage_seconds_total{cluster=""ni-prod-usdal-sco01""})
			";

			var step = TimeSpan.FromMinutes(15);
			var dataFrames = await _prometheusService.ExecuteQueryRangeAsync(startTime, endTime, query, step);

			foreach (var dataFrame in dataFrames)
			{
				var clusterName = NormalizeClusterName(dataFrame.Dimensions["cluster"]);
				if (TryGetClusterByName(clusterName, out var cluster))
				{
					cluster.AvgCpuRequestPerPod = dataFrame.AsDictionary();
				}
			}
		}

		public async Task UpdateClusterNodePodCounts()
        {
            // Query each worker node for cpu/memory stats
            var datasourceName = "Prometheus";
            var queryTemplate = $@"
                count by (node, cluster, region) (agones_cluster_pod_cpu:container_cpu_usage_seconds_total)
            ";
			var step = TimeSpan.FromMinutes(15);
			var endTime = DateTime.UtcNow;
			var dataFrames = await _prometheusService.ExecuteQueryRangeAsync(_startTime, endTime, queryTemplate, step);

			// Query each worker node for podcount, so we can filter bogus ratios
			foreach (var dataFrame in dataFrames)
            {
                var clusterName = NormalizeClusterName(dataFrame.Dimensions["cluster"]);
                if (TryGetClusterByName(clusterName, out var cluster))
                {
                    if (dataFrame.Dimensions.ContainsKey("node"))
                    {
                        var nodeName = dataFrame.Dimensions["node"];
                        if (cluster.TryGetNodeByName(nodeName, out var node, allowCreate: true))
                        {
                            node.PodCounts = dataFrame.AsDictionary();
                        }
                    }
                }
            }
        }

        public async Task UpdateClusterNodeResources()
        {
            //// Query each worker node for cpu/memory stats
            //var datasourceName = "Prometheus";
            //var armadaRegex = string.Join("|", _armadaSets.Select(s => s.Replace("\"", "")));
            //var queryTemplate = $@"
            //    label_replace(
            //        instance:node_cpu:rate:sum,
            //        ""internal_ip"",
            //        ""$1"",
            //        ""instance"",
            //        ""(.+):9100""
            //    )
            //    * on (internal_ip, cluster) group_left (node, cluster) kube_node_info{{node!~""master.+""}}
            //    * on (node, cluster) kube_node_labels{{label_game_server=~"".*""}}
            //    /
            //    sum by (node, cluster) (
            //        agones_cluster_node_cpu_requests:kube_pod_container_resource_requests:sum
            //    )
            //";
            //var dataFrames = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, queryTemplate);

            //// Query each worker node for podcount, so we can filter bogus ratios
            ////count by (node) (agones_cluster_pod_cpu:container_cpu_usage_seconds_total)

            //// Update nodes
            //foreach (var dataFrame in dataFrames)
            //{
            //    var clusterName = NormalizeClusterName(dataFrame.Dimensions["cluster"]);
            //    if (TryGetClusterByName(clusterName, out var cluster))
            //    {
            //        var nodeName = dataFrame.Dimensions["node"];
            //        if (cluster.TryGetNodeByName(nodeName, out var node, allowCreate: true))
            //        {
            //            node.CpuActualToCpuRequest = dataFrame.AsDictionary();
            //        }
            //    }
            //}
        }

		/// <summary>
		/// Queries Grafana with a Prometheus datasource to fetch basic metadata about the clusters in our system,
		/// like Name, Type, Region, Environment.  It normalizes cluster names (since in some places "-last-epoch"
		/// is added to the hostname, other places not).
		/// <summary>
		public async Task UpdateClusterArmadaInfo(DateTime startTime, DateTime endTime)
		{
			using var _ = ScopedTimer.Measure(new { startTime, endTime });

			var armadaRegex = string.Join("|", _armadaSets.Select(s => s.Replace("\"", "")));
			// Use 'max_over_time' to ensure we get labels even if the metric appears/disappears
			// during the query range. We only care about the labels existing at *some point*.
			var query = $@"
				max_over_time(armada_armada_status_sites_replicas{{armadaset=~""{armadaRegex}""}}[{(endTime - startTime).TotalSeconds}s])
			";

			// Query cluster data
			Console.WriteLine("Discovering clusters via Prometheus armada metric...");
			var step = endTime - startTime; // One step for the whole range
			List<DataFrame> dataFrames = await _prometheusService.ExecuteQueryRangeAsync(startTime, endTime, query, step);
			if (dataFrames == null)
			{
				Console.WriteLine("Failed to retrieve cluster armada info from Prometheus.");
				return;
			}

			// Process clusters
			foreach (var dataFrame in dataFrames)
			{
				var dimensions = dataFrame.Dimensions; // Labels are in Dimensions

				if (!dimensions.TryGetValue("site", out var siteName))
				{
					Console.WriteLine("Warning: Found armada metric without 'site' label. Skipping.");
					continue;
				}

				var clusterName = NormalizeClusterName(siteName);
				if (_clusters.TryAdd(clusterName, new Cluster()))
				{
					var cluster = _clusters[clusterName];
					cluster.Name = clusterName;
					cluster.Type = dimensions.TryGetValue("type", out var type) ? type : "unknown";
					cluster.Region = dimensions.TryGetValue("region", out var region) ? region : "unknown";
					cluster.Namespace = dimensions.TryGetValue("namespace", out var ns) ? ns : "unknown";
					cluster.Environment = dimensions.TryGetValue("environment", out var env) ? env : "unknown";
				}
			}

			Console.WriteLine($"Discovered {_clusters.Count} clusters via Prometheus armada metric.");
		}

		private bool TryGetClusterByName(string clusterName, out Cluster cluster, bool allowCreate = false)
        {
            cluster = null;

            // Reject creates for invalid names
            if (allowCreate && string.IsNullOrWhiteSpace(clusterName))
            {
                return false;
            }

            // Attempt lookup
            if (!_clusters.TryGetValue(clusterName, out cluster))
            {
                if (allowCreate)
                {
                    cluster = new Cluster();
                    _clusters.Add(clusterName, cluster);
                }
            }

            return cluster != null;
        }

        private static string NormalizeClusterName(string clusterName)
        {
            // Armada set sites have "last-epoch" appended
            const string suffix = "-last-epoch";
            if (clusterName != null && clusterName.EndsWith(suffix))
            {
                return clusterName.Substring(0, clusterName.Length - suffix.Length);
            }

            return clusterName;
        }

		public void DumpListToCsvFile(IList list, string filePath)
		{
			if (list == null) throw new ArgumentNullException(nameof(list));
			if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

			// Make sure the directory exists
			var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
			if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			// Convert to CSV using your existing helper
			var csv = ExportListToCsv(list);

			// Write out
			File.WriteAllText(filePath, csv);

			Console.WriteLine($"[DEBUG] Wrote {list.Count} rows → {filePath}");
		}

		public string ExportListToCsv(IList list)
		{
			if (list == null || list.Count == 0)
			{
				return string.Empty;
			}

			var elementType = list.GetType().GetGenericArguments()[0];
			var properties = elementType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			var simpleProperties = properties.Where(prop =>
				!typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) || prop.PropertyType == typeof(string))
				.ToArray();

			var dictProperties = properties.Where(prop =>
				//prop.PropertyType.IsGenericType
				false // disabled
				&& prop.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
				&& prop.PropertyType.GetGenericArguments()[0] == typeof(string)
				&& prop.PropertyType.GetGenericArguments()[1] == typeof(string))
				.ToArray();

			var dictKeys = new Dictionary<PropertyInfo, HashSet<string>>();
			foreach (var dictProp in dictProperties)
			{
				dictKeys[dictProp] = new HashSet<string>();
			}

			foreach (var item in list)
			{
				foreach (var dictProp in dictProperties)
				{
					var dict = dictProp.GetValue(item) as Dictionary<string, string>;
					if (dict != null)
					{
						foreach (var key in dict.Keys)
						{
							dictKeys[dictProp].Add(key);
						}
					}
				}
			}

			var columns = new List<string>();
			foreach (var prop in simpleProperties)
			{
				columns.Add(prop.Name);
			}
			foreach (var dictProp in dictProperties)
			{
				var propName = dictProp.Name;
				// Order the keys for consistent column output
				foreach (var key in dictKeys[dictProp].OrderBy(k => k))
				{
					columns.Add($"{propName}.{key}");
				}
			}

			using (var writer = new StringWriter())
			{
				var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
				{
					// CsvHelper will automatically handle escaping internal quotes (by doubling them)
					// when it determines a field needs quoting (which is always, in this case).
					ShouldQuote = args => true,
				};

				using (var csv = new CsvWriter(writer, config))
				{
					// Write header
					foreach (var columnName in columns)
					{
						csv.WriteField(columnName);
					}
					csv.NextRecord();

					// Write records
					foreach (var item in list)
					{
						// Write simple property values
						foreach (var prop in simpleProperties)
						{
							var value = prop.GetValue(item);
							// Let CsvHelper handle formatting and escaping based on ShouldQuote
							csv.WriteField(value);
						}

						// Write dictionary values
						foreach (var dictProp in dictProperties)
						{
							var dict = dictProp.GetValue(item) as Dictionary<string, string>;
							// Use the same ordered keys as the header
							var keys = dictKeys[dictProp].OrderBy(k => k);

							foreach (var key in keys)
							{
								string valueToWrite = string.Empty; // Default to empty if key not found for this item
								if (dict != null && dict.TryGetValue(key, out var dictValue))
								{
									valueToWrite = dictValue;
								}
								// Write the potentially complex string - CsvHelper takes care of it
								csv.WriteField(valueToWrite);
							}
						}
						csv.NextRecord();
					}
				}
				return writer.ToString();
			}
		}

		public async Task TestSeriesQuery()
		{
			Console.WriteLine("Testing Prometheus /api/v1/series endpoint...");

			// Matcher similar to the curl command: -d 'match[]=up'
			var matchers = new List<string> { "up" };

			try
			{
				// Assuming _prometheusService is initialized correctly in the constructor
				List<Dictionary<string, string>> seriesLabelSets = await _prometheusService.ExecuteSeriesQueryAsync(matchers);

				if (seriesLabelSets != null)
				{
					Console.WriteLine($"Found {seriesLabelSets.Count} series matching '{string.Join(",", matchers)}'.");

					// Print the first few results (optional)
					int count = 0;
					foreach (var labelSet in seriesLabelSets)
					{
						if (count < 5) // Print labels for the first 5 series found
						{
							Console.WriteLine($"  Series {count + 1}:");
							foreach (var kvp in labelSet)
							{
								Console.WriteLine($"    {kvp.Key} = {kvp.Value}");
							}
						}
						else if (count == 5)
						{
							Console.WriteLine("  ...");
						}
						count++;
					}
				}
				else
				{
					Console.WriteLine("Series query returned null.");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error during series query test: {ex.Message}");
				// Log ex.ToString() for full stack trace if needed
			}
			Console.WriteLine("Finished testing /api/v1/series.");
		}

		public async Task<List<string>> GetCpuMetricNamespacesAsync(DateTime startTime, DateTime endTime)
		{
			/* BEGIN fix – make the series matcher valid */
			var matchers = new List<string>
{
	"{__name__=\"agones_cluster_pod_cpu:container_cpu_usage_seconds_total\"}"
};
			/* END fix */

			var seriesLabelSets = await _prometheusService.ExecuteSeriesQueryAsync(matchers,
																				   startTime,
																				   endTime);

			var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var labelSet in seriesLabelSets ?? Enumerable.Empty<Dictionary<string, string>>())
				if (labelSet.TryGetValue("namespace", out var ns) && !string.IsNullOrWhiteSpace(ns))
					namespaces.Add(ns);

			return namespaces.ToList();
		}

		private void CalculatePeakCpu()
        {
            // Calculate peak ratio of CpuActualToCpuRequest
            foreach (var cluster in _clusters.Values)
            {
                foreach (var node in cluster.Nodes)
                {
                    foreach (var kvp in node.CpuActualToCpuRequest)
                    {
                        var cpuActualToCpuRequest = kvp.Value;
                        if (node.PodCounts.ContainsKey(kvp.Key))
                        {
                            var podCount = node.PodCounts[kvp.Key];
                            if (cpuActualToCpuRequest > 2.0 && podCount > 15)
                            {
                                var x = 3;
                            }
                        }
                    }
                }
            }
        }

		private IEnumerable<Cluster> FilterClusters(ClusterPredicate pred)
		{
			return pred == null
				 ? _clusters.Values
				 : _clusters.Values.Where(pred.Invoke);
		}
	}
}
