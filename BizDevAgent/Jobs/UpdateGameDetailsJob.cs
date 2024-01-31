using BizDevAgent.Model;
using BizDevAgent.DataStore;

namespace BizDevAgent.Jobs
{
    /// <summary>
    /// For each game in the database, update game stats such as 
    ///     - review count
    ///     - engine type
    ///  Use the steam store page.
    /// </summary>
    [TypeId("UpdateGameDetailsJob")]
    public class UpdateGameDetailsJob : Job
    {
        private readonly GameDataStore _gameDataStore;

        public UpdateGameDetailsJob(GameDataStore gameDataStore)
        {
            _gameDataStore = gameDataStore;
        }

        public override Task UpdateScheduledRunTime()
        {
            ScheduledRunTime = ScheduledRunTime.AddDays(1);
            return Task.CompletedTask;
        }

        public async override Task Run()
        {
            var games = _gameDataStore.All;
            Random rnd = new Random();
            TimeSpan medianDelay = TimeSpan.FromSeconds(3);
            TimeSpan radiusDelay = TimeSpan.FromSeconds(1);
            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];

                Console.WriteLine($"[{i} / {games.Count}] Updating details for '{game.Name}' by '{game.DeveloperName}'");

                // Update the game with information from the steamdb app page
                await _gameDataStore.UpdateDetails(game);

                // Throttle the update so we don't impolitely spam the server
                int minDelay = (int)(medianDelay.TotalMilliseconds - radiusDelay.TotalMilliseconds);
                int maxDelay = (int)(medianDelay.TotalMilliseconds + radiusDelay.TotalMilliseconds);
                int delay = rnd.Next(minDelay, maxDelay);
                Console.WriteLine($"Waiting {delay} ms");
                await Task.Delay(delay);
            }

            await _gameDataStore.SaveAll();
        }
    }
}
