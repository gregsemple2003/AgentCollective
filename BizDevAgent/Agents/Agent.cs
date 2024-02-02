using BizDevAgent.DataStore;
using BizDevAgent.Model;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.Agents
{
    /// <summary>
    /// An agent adds a capability to interact with the world in some way (email, linkedin, github, web, etc).
    /// These are typically leveraged by either data stores to fetch data, or jobs which accomplish a specific task.
    /// </summary>
    [TypeId("Agent")]
    public class Agent
    {
        public static void RegisterAll(IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Find all types that are subclasses of Job and are not abstract
            var jobTypes = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Agent)) && !t.IsAbstract);

            // Register each job type
            foreach (var type in jobTypes)
            {
                services.AddTransient(type);
            }
        }
    }
}
