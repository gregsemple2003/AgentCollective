using Agent.Core;
using Agent.Services;

namespace Agent.BizDev
{
    [TypeId("TestPeriodicJob")]
    public class TestPeriodicJob : Job
    {
        private readonly GameDataStore _gameDataStore;

        public TestPeriodicJob(GameDataStore gameDataStore)
        {
            _gameDataStore = gameDataStore;
        }

        public override Task UpdateScheduledRunTime()
        {
            ScheduledRunTime = ScheduledRunTime.AddSeconds(5);
            return Task.CompletedTask;
        }

        public async override Task Run()
        {
            await Task.Delay(1*1000);

            Console.WriteLine($"Test job '{Name}' finished.  now = {DateTime.Now}");

            string obj = null;
            var x = obj.Length;
        }
    }
}
