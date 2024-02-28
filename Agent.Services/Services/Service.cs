using Agent.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Agent.Services
{
    /// <summary>
    /// An service adds a capability to interact with the world in some way (email, linkedin, github, web, etc).
    /// These are typically leveraged by either data stores to fetch data, or jobs which accomplish a specific task.
    /// All subclasses register as singleton objects, since they are instantiated for the lifetime of the application.
    /// </summary>
    [TypeId("Agent")]
    public class Service
    {
        public static void RegisterAll(IServiceCollection services)
        {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Find all types that are subclasses of Job and are not abstract
                var types = assembly.GetTypes()
                    .Where(t => t.IsSubclassOf(typeof(Service)) && !t.IsAbstract);

                // Register each job type
                foreach (var type in types)
                {
                    services.AddSingleton(type);
                }
            }
        }
    }
}
