using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;

namespace BizDevAgent.Flow
{
    [TypeId("RefineFixPlanAgentGoal")]
    public class RefineFixPlanAgentGoal : ProgrammerAgentGoal
    {
        private readonly RepositoryQuerySession _targetRepositoryQuerySession;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;
        private readonly IServiceProvider _serviceProvider;

        public RefineFixPlanAgentGoal(IServiceProvider serviceProvider, AgentGoalSpec spec)
            : base(spec)
        {
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
            _targetRepositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
            _serviceProvider = serviceProvider;
        }
    }
}
