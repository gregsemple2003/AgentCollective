using BizDevAgent.DataStore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BizDevAgent.Jobs
{
    /// <summary>
    /// A periodic job which runs to perform some task, usually to mutate persistent data.
    /// </summary>
    [TypeId("Job")]
    public class Job
    {
        public string Name { get; set; }
        public bool Enabled { get; set; } = true;
        public DateTime ScheduledRunTime { get; set; }
        public DateTime LastRunTime { get; set; }

        public async virtual Task Run()
        {
            await Task.CompletedTask;
        }

        public async virtual Task UpdateScheduledRunTime()
        {
            await Task.CompletedTask;
        }

        public static void RegisterAll(IServiceCollection services)
        {
            services.AddTransient<JobRunner>();

            var assembly = Assembly.GetExecutingAssembly();

            // Find all types that are subclasses of Job and are not abstract
            var jobTypes = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Job)) && !t.IsAbstract);

            // Register each job type
            foreach (var type in jobTypes)
            {
                services.AddTransient(type);
            }
        }
    }
}
