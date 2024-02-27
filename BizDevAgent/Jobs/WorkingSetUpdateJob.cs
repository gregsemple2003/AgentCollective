using System.Reflection;
using BizDevAgent.Services;
using BizDevAgent.Utilities;
using BizDevAgent.DataStore;
using Microsoft.Extensions.DependencyInjection;
using BizDevAgent.Flow;

namespace BizDevAgent.Jobs
{
    [TypeId("WorkingSetUpdateJob")]
    public class WorkingSetUpdateJob : Job
    {
        private readonly List<WorkingSetEntry> _workingSetEntries;

        public WorkingSetUpdateJob(List<WorkingSetEntry> workingSetEntries)
        {
            _workingSetEntries = workingSetEntries;
        }

        public override Task Run()
        {
            foreach(var workingSetEntry in _workingSetEntries)
            {
                workingSetEntry.WriteToConsole();
            }

            return Task.CompletedTask;
        }
    }
}
