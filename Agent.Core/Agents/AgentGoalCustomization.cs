namespace Agent.Core
{
    /// <summary>
    /// Class which customizes behavior of the core agent loop 
    ///    - prompt generation
    ///    - response processing
    ///    - transition logic
    /// </summary>
    [TypeId("AgentGoalCustomization")]
    public class AgentGoalCustomization : JsonAsset // TODO gsemple: remove this, deprecated
    {
        public virtual bool ShouldRequestPrompt(AgentState agentState) 
        { 
            return false;
        }

        public virtual void PopulatePrompt(AgentPromptContext promptContext, AgentState agentState)
        {
        }

        public virtual Task PrePrompt(AgentState agentState)
        {
            return Task.CompletedTask;
        }

        public virtual Task ProcessResponse(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            return Task.CompletedTask;
        }
    }
}
