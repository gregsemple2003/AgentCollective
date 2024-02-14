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
        public virtual Task ProcessResponse(string response, AgentState agentState, IResponseParser languageModelParser)
        {
            return Task.CompletedTask;
        }
    }
}
