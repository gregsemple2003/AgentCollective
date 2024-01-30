using BizDevAgent.DataStore;

namespace BizDevAgent.Model
{
    /// <summary>
    /// A periodic job which runs to perform some task, usually to mutate persistent data.
    /// </summary>
    [TypeId("Job")]
    public class Job
    {
        public string Name { get; set; }
        public DateTime ScheduledRunTime { get; set; }
        public DateTime LastRunTime { get; set; }

        public async virtual Task Run()
        {
            await Task.CompletedTask;
        }
    }
}
