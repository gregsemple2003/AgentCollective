using BizDevAgent.Agents;

namespace BizDevAgent.Flow
{
    public class ProgrammerAgentState : AgentState
    {
        public override IAgentShortTermMemory ShortTermMemory { get; set; }

        public ProgrammerAgentState() 
        {
            ShortTermMemory = new ProgrammerShortTermMemory();
        }
    }
}
