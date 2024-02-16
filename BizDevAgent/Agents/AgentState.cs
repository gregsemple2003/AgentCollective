using Newtonsoft.Json.Linq;

namespace BizDevAgent.Agents
{
    public interface IAgentShortTermMemory
    {
        string ToJson();
    }

    public abstract class AgentState
    {
        /// <summary>
        /// The current working stack of goals.
        /// </summary>
        public Stack<AgentGoal> Goals { get; set; }

        /// <summary>
        /// Information gained from raw sensory data, e.g. viewing logs, reading debugger output.
        /// </summary>
        public List<AgentObservation> Observations { get; set; }

        /// <summary>
        /// Our current planning state, which is specialized according to the task we are performing.
        /// </summary>
        public virtual IAgentShortTermMemory ShortTermMemory { get; set; }

        public AgentState() 
        { 
            Observations = new List<AgentObservation>();
            Goals = new Stack<AgentGoal>();
        }
    }
}
