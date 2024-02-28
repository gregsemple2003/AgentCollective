namespace Agent.BizDev
{
    /// <summary>
    /// A data point in the time series data associated with a specific game.
    /// </summary>
    public class GameSeries
    {
        public DateTime TimeGenerated { get; set; }
        public int AppId { get; set; }
        public int TotalReviewCount { get; set; }
        public int RecentReviewCount { get; set; }
    }
}
