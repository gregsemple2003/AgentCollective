namespace BizDevAgent.Goals
{
    public class AgentGoal
    {
        public string Description { get; set; }
        public List<AgentGoal> SubGoals { get; set; } = new List<AgentGoal>();

        public bool Completed { get; set; }

        public AgentGoal(string description)
        {
            Description = description;
        }
    }
}
