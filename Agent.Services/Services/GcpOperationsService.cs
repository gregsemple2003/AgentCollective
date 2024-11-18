using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.Compute.V1;
using Google.Api.Gax;
using static Google.Cloud.Compute.V1.ComputeEnumConstants;

// DTO class to represent operation timing information
public class OperationInfo
{
    public string Name { get; set; }
    public string Zone { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

public class GcpOperationsService
{
    private readonly GlobalOperationsClient _operationsClient;
    private readonly string _projectId;

    public GcpOperationsService(string projectId)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _operationsClient = GlobalOperationsClient.Create();
    }

    /// <summary>
    /// Retrieves insert operations for Compute Engine instances within the specified time window.
    /// </summary>
    /// <param name="hoursAgo">Number of hours ago to start the time window.</param>
    /// <returns>List of insert operations.</returns>
    public async Task<IList<OperationInfo>> GetInsertOperationsAsync(int hoursAgo)
    {
        if (hoursAgo <= 0)
            throw new ArgumentException("Hours ago must be greater than zero.", nameof(hoursAgo));

        DateTime startTimeLimit = DateTime.UtcNow.AddHours(-hoursAgo);
        var operations = new List<OperationInfo>();

        // Prepare the request
        var request = new AggregatedListGlobalOperationsRequest
        {
            Project = _projectId,
            Filter = $"operationType = insert",// AND startTime >= '{startTimeLimit:yyyy-MM-ddTHH:mm:ssZ}'",
            //Zone = "europe-west3-c"
        };

        // Retrieve operations
        var response = _operationsClient.AggregatedList(request);

        foreach (var entry in response)
        {
            foreach(var operation in entry.Value.Operations)
            {
                var parseSucceeded = true;
                parseSucceeded &= DateTime.TryParse(operation.StartTime, out var startTime);
                parseSucceeded &= DateTime.TryParse(operation.EndTime, out var endTime);
                if (parseSucceeded)
                {
                    var operationInfo = new OperationInfo
                    { 
                        Name = operation.Name,
                        Zone = GetZoneNameFromUrl(operation.Zone),
                        StartTime = startTime,
                        EndTime = endTime
                    };
                    operations.Add(operationInfo);
                }
            }
        }

        return operations;
    }
    public void QueryStartupTimesByZone(IList<OperationInfo> operations)
    {
        if (operations == null)
            throw new ArgumentNullException(nameof(operations));

        // 1. Group startup times by zone
        var startupTimesByZone = operations
            .GroupBy(op => op.Zone)
            .ToDictionary(
                g => g.Key,
                g => g.Select(op => (op.EndTime - op.StartTime).TotalSeconds).OrderBy(t => t).ToList()
            );
        var minTime = operations.Min(op => op.StartTime);
        var maxTime = operations.Max(op => op.StartTime);

        // 2. For each zone, compute percentiles
        Console.WriteLine($"Query startup times by zone: {new { minTime, maxTime}}");
        Console.WriteLine($"Zone, P50, P95, P99, P100");
        foreach (var kvp in startupTimesByZone)
        {
            var zone = kvp.Key;
            var times = kvp.Value;

            double p50 = Percentile(times, 50);
            double p95 = Percentile(times, 95);
            double p99 = Percentile(times, 99);
            double p100 = Percentile(times, 100);

            Console.WriteLine($"{zone}, {p50:F2}, {p95:F2}, {p99:F2}, {p100:F2}");
        }
    }

    // Helper method to calculate percentiles
    private double Percentile(List<double> sortedData, double percentile)
    {
        if (sortedData == null || sortedData.Count == 0)
            throw new InvalidOperationException("Cannot compute percentile for empty data.");

        if (percentile < 0 || percentile > 100)
            throw new ArgumentOutOfRangeException(nameof(percentile));

        int N = sortedData.Count;

        // Position calculation
        double position = (percentile / 100) * (N - 1);

        int lowerIndex = (int)Math.Floor(position);
        int upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return sortedData[lowerIndex];
        }
        else
        {
            double weight = position - lowerIndex;
            return sortedData[lowerIndex] * (1 - weight) + sortedData[upperIndex] * weight;
        }
    }

    /// <summary>
    /// Extracts the zone name from a zone URL.
    /// </summary>
    /// <param name="zoneUrl">The zone URL.</param>
    /// <returns>The zone name.</returns>
    private string GetZoneNameFromUrl(string zoneUrl)
    {
        if (string.IsNullOrEmpty(zoneUrl))
            return null;

        return zoneUrl.Substring(zoneUrl.LastIndexOf('/') + 1);
    }
}
