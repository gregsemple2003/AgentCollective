using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BizDevAgent.DataStore;

namespace BizDevAgent.Jobs
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
