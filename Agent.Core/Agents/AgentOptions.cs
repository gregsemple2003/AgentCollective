namespace Agent.Core
{
    /// <summary>
    /// Parameters which can be chosen at creation-time.
    /// </summary>
    public class AgentOptions
    {
        public PromptAsset GoalPrompt { get; set; }
        public AgentGoalSpec DoneGoal { get; set; }
    }
}
