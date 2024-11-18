using CsvHelper;
using GrafanaServiceExample;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace Agent.Services
{
    internal class Cluster
    {
        internal string Name { get; set; }
        internal string Type { get; set; } // gcp, bare-metal, gcp-tw
        internal string Region { get; set; }
        internal string Namespace { get; set; } // last-epoch
        internal string Environment { get; set; } // prod, stge
        internal List<Node> Nodes { get; set; }
        internal DateTime PeakAllocatedReplicaTime { get; set; }
        internal int PeakReadyReplicaCount { get; set; }
        internal int PeakAllocatedReplicaCount { get; set; }
        internal double PeakAllocatedCpuUtilisation { get; set; }
        internal Dictionary<DateTime, double> ReadyReplicaCounts { get; set; }
        internal Dictionary<DateTime, double> AllocatedReplicaCounts { get; set; }
        internal Dictionary<DateTime, double> AllocatedCpuUtilisations { get; set; }
        internal Dictionary<string, string> DebugValues { get; set; }

        internal Cluster()
        {
            Nodes = new List<Node>();
            ReadyReplicaCounts = new Dictionary<DateTime, double>();
            AllocatedReplicaCounts = new Dictionary<DateTime, double>();
            AllocatedCpuUtilisations = new Dictionary<DateTime, double>();
            DebugValues = new Dictionary<string, string>();
        }

        internal bool TryGetNodeByName(string nodeName, out Node node, bool allowCreate = false)
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

    internal class Node
    {
        internal string Name { get; set; }

        /// <summary>
        /// Ratio of CPU actually being used to the total CPU request across the entire node
        /// </summary>
        internal Dictionary<DateTime, double> CpuActualToCpuRequest { get; set; }

        /// <summary>
        /// Number of pods (game server) running on the node
        /// </summary>
        internal Dictionary<DateTime, double> PodCounts { get; set; }

        internal Node()
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
        private readonly GrafanaService _grafanaService;
        private readonly Dictionary<string, Cluster> _clusters;
        private readonly List<string> _armadaSets;

        private DateTime _startTime;

        public PlayerQueueConfigService(GrafanaService grafanaService, List<string> armadaSets)
        {
            _grafanaService = grafanaService;
            _clusters = new Dictionary<string, Cluster>();
            _armadaSets = armadaSets;
        }

        public async Task UpdateResourceRequests(DateTime startTime)
        {
            _startTime = startTime;

            await UpdateClusterArmadaInfo();
            await UpdateClusterReplicaCounts();
            await UpdateClusterAllocatedCpu();
            //await UpdateClusterNodePodCounts();
            //await UpdateClusterNodeResources();


            //CalculatePeakCpu();

            //await GetPodCountsByNode();
        }

        public async Task UpdateClusterAllocatedCpu()
        {
            foreach (var cluster in _clusters.Values)
            {
                await UpdateClusterAllocatedCpu(cluster.Name, TimeSpan.FromMinutes(20));
            }

            var csv = ExportListToCsv(_clusters.Values.ToList());
            Console.WriteLine(csv);
        }

        public async Task UpdateClusterAllocatedCpu(string clusterName, TimeSpan timeAroundPeak)
        {
            // Check that the cluster exists
            if (!TryGetClusterByName(clusterName, out var cluster))
            {
                return;
            }

            // Check that cluster has replica count data
            if (cluster.AllocatedReplicaCounts.Count == 0)
            {
                return;
            }

            // Update peak allocation time
            cluster.PeakAllocatedReplicaTime = default(DateTime);
            cluster.PeakAllocatedReplicaCount = 0;
            foreach (var kvp in cluster.AllocatedReplicaCounts)
            {
                if (kvp.Value > cluster.PeakAllocatedReplicaCount)
                {
                    cluster.PeakAllocatedReplicaCount = (int)Math.Round(kvp.Value);
                    cluster.PeakAllocatedReplicaTime = kvp.Key;
                }
            }
            if (cluster.PeakAllocatedReplicaTime != default(DateTime))
            {
                cluster.PeakReadyReplicaCount = (int)Math.Round(cluster.ReadyReplicaCounts[cluster.PeakAllocatedReplicaTime]);
            }

            // Now you have the peakTime and peakValue

            // Query each worker node for cpu/memory stats
            var datasourceName = "Prometheus";
            var queryTemplate = @"
		        avg(
		          topk(<ALLOCATEDREPLICACOUNT>,
			        rate(agones_cluster_pod_cpu:container_cpu_usage_seconds_total{
			          cluster=""<CLUSTERNAME>"",
			          pod=~"".*prod-a.*""
			        }[5m])
		          )
		        )
            ";
            if (cluster.PeakAllocatedReplicaCount == 0) return;
            var query = queryTemplate.Replace("<CLUSTERNAME>", clusterName).Replace("<ALLOCATEDREPLICACOUNT>", cluster.PeakAllocatedReplicaCount.ToString());
            var dataFrames = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, query);
            cluster.DebugValues["AllocationsQuery"] = query;

            if (dataFrames.Count > 1) throw new InvalidOperationException("Unexpected frame count");

            var dataFrame = dataFrames.First();
            if (dataFrame.Values.Count == 0) return; // no cpu data, could be because nothing was running
            cluster.AllocatedCpuUtilisations = dataFrame.AsDictionary();

            // Define the time window around the peak allocated replica time
            DateTime startTimeWindow = cluster.PeakAllocatedReplicaTime - timeAroundPeak;
            DateTime endTimeWindow = cluster.PeakAllocatedReplicaTime + timeAroundPeak;

            // Filter the CPU utilization data within the time window
            var cpuValuesInWindow = cluster.AllocatedCpuUtilisations
                .Where(kvp => kvp.Key >= startTimeWindow && kvp.Key <= endTimeWindow)
                .Select(kvp => kvp.Value);

            // Compute the average CPU utilization within the time window
            if (cpuValuesInWindow.Any())
            {
                cluster.PeakAllocatedCpuUtilisation = cpuValuesInWindow.Average();
            }
            else
            {
                cluster.PeakAllocatedCpuUtilisation = 0;
            }

            var csv = ExportListToCsv(_clusters.Values.ToList());
            Console.WriteLine(csv);
        }

        public async Task UpdateClusterReplicaCounts()
        {
            // Query each worker node for cpu/memory stats
            var datasourceName = "Prometheus";
            var queryTemplate = $@"
                sum by (type, cluster) (
                    agones_fleets_replicas_count{{type=~""allocated|ready"", name=""prod-a""}}
                )
            ";
            var dataFrames = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, queryTemplate);

            // Query each worker node for podcount, so we can filter bogus ratios
            foreach (var dataFrame in dataFrames)
            {
                var clusterName = NormalizeClusterName(dataFrame.Dimensions["cluster"]);
                if (TryGetClusterByName(clusterName, out var cluster))
                {
                    var type = dataFrame.Dimensions["type"];
                    var counts = dataFrame.AsDictionary();

                    if (type == "allocated")
                    {
                        cluster.AllocatedReplicaCounts = counts;
                    }
                    else if (type == "ready")
                    {
                        cluster.ReadyReplicaCounts = counts;
                    }
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
            var dataFrames = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, queryTemplate);

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
            // Query each worker node for cpu/memory stats
            var datasourceName = "Prometheus";
            var armadaRegex = string.Join("|", _armadaSets.Select(s => s.Replace("\"", "")));
            var queryTemplate = $@"
                label_replace(
                    instance:node_cpu:rate:sum,
                    ""internal_ip"",
                    ""$1"",
                    ""instance"",
                    ""(.+):9100""
                )
                * on (internal_ip, cluster) group_left (node, cluster) kube_node_info{{node!~""master.+""}}
                * on (node, cluster) kube_node_labels{{label_game_server=~"".*""}}
                /
                sum by (node, cluster) (
                    agones_cluster_node_cpu_requests:kube_pod_container_resource_requests:sum
                )
            ";
            var dataFrames = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, queryTemplate);

            // Query each worker node for podcount, so we can filter bogus ratios
            //count by (node) (agones_cluster_pod_cpu:container_cpu_usage_seconds_total)

            // Update nodes
            foreach (var dataFrame in dataFrames)
            {
                var clusterName = NormalizeClusterName(dataFrame.Dimensions["cluster"]);
                if (TryGetClusterByName(clusterName, out var cluster))
                {
                    var nodeName = dataFrame.Dimensions["node"];
                    if (cluster.TryGetNodeByName(nodeName, out var node, allowCreate: true))
                    {
                        node.CpuActualToCpuRequest = dataFrame.AsDictionary();
                    }
                }
            }
        }

        public async Task UpdateClusterArmadaInfo()
        {
            // Query each "site" (aka cluster) along with associated region info
            var datasourceName = "Prometheus";
            var armadaRegex = string.Join("|", _armadaSets.Select(s => s.Replace("\"", "")));
            var queryTemplate = $@"
                armada_armada_status_sites_replicas{{armadaset=~""{armadaRegex}""}}
            ";
            var dataFrames = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, queryTemplate);

            foreach (var dataFrame in dataFrames)
            {
                var clusterName = NormalizeClusterName(dataFrame.Dimensions["site"]);
                if (TryGetClusterByName(clusterName, out var cluster, allowCreate: true))
                {
                    cluster.Name = clusterName;
                    cluster.Type = dataFrame.Dimensions["type"];
                    cluster.Region = dataFrame.Dimensions["region"];
                    cluster.Namespace = dataFrame.Dimensions["namespace"];
                    cluster.Environment = dataFrame.Dimensions["environment"];
                }
            }
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

        public async Task GetPodCountsByNode()
        {
            var datasourceName = "Prometheus";
            var queryTemplate = "count by (node) (agones_cluster_pod_cpu:container_cpu_usage_seconds_total{})";
            var response = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, queryTemplate);
        }

        public async Task GetPeakAllocationRatesByRegion()
        {
            var datasourceName = "Prometheus";
            var interval = "5m";
            var cluster = "allocator-prod-eu";
            //var queryTemplate = "sum by (cluster) (rate(distributor_allocate{svc=\"allocator\", cluster=~\"$cluster\"}[$__interval]))";
            var queryTemplate = "sum by (cluster) (rate(distributor_allocate{svc=\"allocator\"}[$__interval]))";
            var queryExpr = queryTemplate.Replace("$__interval", interval).Replace("$cluster", cluster);
            var response = await _grafanaService.ExecuteQueryAsync(datasourceName, _startTime, queryExpr);
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

        public string ExportListToCsv(IList list)
        {
            if (list == null || list.Count == 0)
            {
                return string.Empty;
            }

            // Determine the element type of the list
            var elementType = list.GetType().GetGenericArguments()[0];

            // Get all properties
            var properties = elementType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Separate simple properties and Dictionary<string, string> properties
            var simpleProperties = properties.Where(prop =>
                !typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) || prop.PropertyType == typeof(string))
                .ToArray();

            var dictProperties = properties.Where(prop =>
                prop.PropertyType.IsGenericType
                && prop.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && prop.PropertyType.GetGenericArguments()[0] == typeof(string)
                && prop.PropertyType.GetGenericArguments()[1] == typeof(string))
                .ToArray();

            // Collect all keys for each dictionary property
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

            // Build columns list
            var columns = new List<string>();

            // Add simple property names
            foreach (var prop in simpleProperties)
            {
                columns.Add(prop.Name);
            }

            // Add dictionary property keys
            foreach (var dictProp in dictProperties)
            {
                var propName = dictProp.Name;
                foreach (var key in dictKeys[dictProp])
                {
                    columns.Add($"{propName}.{key}");
                }
            }

            using (var writer = new StringWriter())
            {
                var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    // Always quote fields to handle commas, newlines, and quotes within fields
                    ShouldQuote = args => true
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

                            if (value is string strValue)
                            {
                                // Ensure that any double quotes in the string are properly escaped
                                if (strValue.Contains("topk"))
                                {
                                    var x = 3;
                                }
                                strValue = strValue.Replace("\"", "\"\"");
                                strValue = strValue.Replace("\n", "");
                                strValue = strValue.Replace("\r", "");
                                strValue = strValue.Replace("\f", "");

                                csv.WriteField(strValue);
                            }
                            else
                            {
                                csv.WriteField(value);
                            }
                        }

                        // Write dictionary values
                        foreach (var dictProp in dictProperties)
                        {
                            var dict = dictProp.GetValue(item) as Dictionary<string, string>;
                            var keys = dictKeys[dictProp];

                            foreach (var key in keys)
                            {
                                string value = string.Empty;
                                if (dict != null && dict.TryGetValue(key, out var dictValue))
                                {
                                    // Ensure that any double quotes in the string are properly escaped
                                    //dictValue = dictValue.Replace("\"", "\"\"");
                                    dictValue = dictValue.Replace("\n", "");
                                    dictValue = dictValue.Replace("\r", "");
                                    dictValue = dictValue.Replace("\f", "");

                                    csv.WriteField(dictValue);
                                }
                                else
                                {
                                    csv.WriteField(value);
                                }
                            }
                        }

                        csv.NextRecord();
                    }
                }

                // Return the CSV content as a string
                return writer.ToString();
            }
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
    }
}
