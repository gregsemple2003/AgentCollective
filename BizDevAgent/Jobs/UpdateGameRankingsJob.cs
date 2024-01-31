using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BizDevAgent.DataStore;
using BizDevAgent.Model;
using BizDevAgent.Utilities;

namespace BizDevAgent.Jobs
{
    public class NewHighsEntry
    {
        public DateTime TimeGenerated { get; set; }
        public Game Game { get; set; }
        public string Ranking { get; set; }
        public int ReviewCount { get; set; }
    }

    [TypeId("UpdateGameRankingsJob")]
    public class UpdateGameRankingsJob : Job
    {
        public DateTime LastRunTime { get; set; }

        public List<NewHighsEntry> NewHighs { get; set; }

        private readonly GameDataStore _gameDataStore;
        private readonly GameSeriesDataStore _gameSeriesDataStore;

        public UpdateGameRankingsJob(GameDataStore gameDataStore, GameSeriesDataStore gameSeriesDataStore)
        {
            _gameDataStore = gameDataStore;
            _gameSeriesDataStore = gameSeriesDataStore;

            NewHighs = new List<NewHighsEntry>();
        }

        public override Task UpdateScheduledRunTime()
        {
            ScheduledRunTime = ScheduledRunTime.AddDays(1);
            return Task.CompletedTask;
        }

        public async override Task Run()
        {
            foreach (var game in _gameDataStore.All)
            {
                // Load series data from the past 5 years until now
                var seriesData = await _gameSeriesDataStore.Load(game.SteamAppId, DateTime.UtcNow.AddYears(-5), DateTime.UtcNow);

                // Divide the data into before and after LastRunTime
                var seriesDataBefore = seriesData.Where(gs => gs.TimeGenerated < LastRunTime).ToList();
                var seriesDataAfter = seriesData.Where(gs => gs.TimeGenerated >= LastRunTime).ToList();

                // Find the highest review count before and after LastRunTime
                var highestBefore = seriesDataBefore.Any() ? seriesDataBefore.Max(gs => gs.TotalReviewCount) : 0;
                var highestAfter = seriesDataAfter.Any() ? seriesDataAfter.Max(gs => gs.TotalReviewCount) : 0;

                // Check if a new high is reached
                if (highestAfter > highestBefore)
                {
                    Console.WriteLine($"New high reached for game {game.Name} (AppId: {game.SteamAppId}): {highestAfter} reviews");
                    await OnNewHigh(game, highestBefore, highestAfter);
                }
            }

            LastRunTime = DateTime.Now;

            await ReportResults();
        }

        private async Task ReportResults()
        {
            var sb = new StringBuilder();
            sb.Append("<html><body>");
            foreach (var newHigh in NewHighs.OrderByDescending(nh => nh.ReviewCount))
            {
                string steamUrl = $"https://store.steampowered.com/app/{newHigh.Game.SteamAppId}";
                string imageUrl = newHigh.Game.SteamHeaderImageUrl; // Assuming this property holds the image URL
                sb.Append($"<p><a href='{steamUrl}' target='_blank'><img src='{imageUrl}' alt='Game Image' style='width:300px; height:auto; margin-right:10px; vertical-align:middle;'/> {newHigh.Game.Name}</a> is now a {newHigh.Ranking}!</p>");
            }
            sb.Append("</body></html>");
            await EmailUtils.SendEmail("gregsemple2003@gmail.com", "Ranking Changes", sb.ToString());
        }

        private async Task OnNewHigh(Game game, int highestBefore, int highestAfter)
        {
            // Log a rank change
            var beforeRanking = GetTierForReviewCount(highestBefore);
            var afterRanking = GetTierForReviewCount(highestAfter);
            if (beforeRanking != afterRanking)
            {
                var newHigh = new NewHighsEntry
                {
                    TimeGenerated = DateTime.UtcNow,
                    Game = game,
                    ReviewCount = highestAfter,
                    Ranking = GetTierForReviewCount(highestAfter)
                };
                NewHighs.Add(newHigh);
            }
        }

        private string GetTierForReviewCount(int reviewCount)
        {
            if (reviewCount > 100000)
            {
                return "Tier 5";
            }
            else if (reviewCount > 50000)
            {
                return "Tier 4";
            }
            else if (reviewCount > 10000)
            {
                return "Tier 3";
            }
            else if (reviewCount > 5000)
            {
                return "Tier 2";
            }
            else if (reviewCount > 1000)
            {
                return "Tier 1";
            }
            else
            {
                return "Tier 0";
            }
        }
    }
}
