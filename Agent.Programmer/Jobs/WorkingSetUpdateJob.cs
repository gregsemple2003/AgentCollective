using Agent.Core;
using Agent.Services;

namespace Agent.Programmer
{
    [TypeId("WorkingSetUpdateJob")]
    public class WorkingSetUpdateJob : Job
    {
        private readonly List<RepositoryQueryEntry> _workingSetEntries;

        public WorkingSetUpdateJob(List<RepositoryQueryEntry> workingSetEntries)
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
