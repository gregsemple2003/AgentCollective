using BizDevAgent.DataStore;
using BizDevAgent.Services;

namespace BizDevAgent.Agents
{
    /// <summary>
    /// Class which customizes behavior of the core agent loop 
    ///    - prompt generation
    ///    - response processing
    ///    - transition logic
    /// </summary>
    [TypeId("AgentGoalCustomization")]
    public class AgentGoalCustomization : JsonAsset
    {
        public virtual bool ShouldRequestCompletion(AgentState agentState) 
        { 
            return false;
        }

        public virtual void CustomizePrompt(AgentPromptContext promptContext, AgentState agentState)
        {
        }

        public virtual Task PreCompletion(AgentState agentState)
        {
            return Task.CompletedTask;
        }

        public virtual Task ProcessResponse(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            return Task.CompletedTask;
        }
    }
}
