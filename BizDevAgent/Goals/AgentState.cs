namespace BizDevAgent.Goals
{
    public class AgentState
    {
        /// <summary>
        /// The current observation state of the agent in natural language form.  When the agent requests information via
        /// our API, it is recorded here for future use in prompts.
        /// </summary>
        public List<AgentObservation> Observations { get; set; }

        /// <summary>
        /// Our current planning state, which is changed according to responses from language models.
        /// </summary>
        public List<AgentVariable> Variables { get; set; }

        /// <summary>
        /// The current working stack of goals.
        /// </summary>
        public Stack<AgentGoalAsset> Goals { get; set; }

        public AgentState() 
        { 
            Observations = new List<AgentObservation>();
            Variables = new List<AgentVariable>();
            Goals = new Stack<AgentGoalAsset>();
        }
    }
}
