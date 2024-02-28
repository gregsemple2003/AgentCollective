namespace Agent.BizDev
{
    public class Game
    {
        public string Name { get; internal set; }
        public string DeveloperName { get; internal set; }
        public string Engine { get; internal set; }
        public int YearPublished { get; internal set; }
        public double UserRating { get; internal set; }
        public int FollowerCount { get; internal set; }
        public int PeakUserCount { get; internal set; }
        public int ReviewCount { get; internal set; }
        public string SteamDbUrl { get; internal set; }
        public string SteamHeaderImageUrl { get; internal set; }
        public int SteamAppId { get; internal set; }
    }
}
