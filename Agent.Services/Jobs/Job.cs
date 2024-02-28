using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Agent.Core;

namespace Agent.Services
{
    /// <summary>
    /// A periodic job which runs to perform some task, usually to mutate persistent data.
    /// All subclasses register as transient objects, since they are instantiated to perform
    /// some task and then discarded.
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

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Find all types that are subclasses of Job and are not abstract
                var types = assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(Job)) && !t.IsAbstract);

                // Register each job type
                foreach (var type in types)
                {
                    services.AddSingleton(type);
                }
            }
        }
    }
}
