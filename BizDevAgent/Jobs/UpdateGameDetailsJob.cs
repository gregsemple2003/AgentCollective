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
            var games = await _gameDataStore.LoadAll(forceRemote: true);
            for (int i = 0; i < games.Count; i++)
            {
                var game = games[i];

                Console.WriteLine($"[{i} / {games.Count}] Updating details for '{game.Name}' by '{game.DeveloperName}'");

                // Update the game with information from the steamdb app page
                await _gameDataStore.UpdateDetails(game);
            }

            await _gameDataStore.SaveAll();
        }
    }
}
