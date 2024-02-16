using BizDevAgent.Agents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDevAgent.Flow
{
    public class CodingTasks
    {
        public List<ImplementationStep> Steps { get; set; }
    }

    public class ImplementationStep
    {
        public int Step { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Facts which have been extracted from our sensory data, and are relevant to the immediate task (and possibly
    /// the overall task).
    /// </summary>
    public class ProgrammerShortTermMemory : IAgentShortTermMemory
    {
        public CodingTasks CodingTasks { get; set; }
        public JToken Conclusions { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

    }
}
