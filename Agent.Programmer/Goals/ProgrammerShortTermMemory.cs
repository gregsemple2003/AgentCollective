using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Agent.Core;
using Agent.Services;

namespace Agent.Programmer
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
    [TypeId("ProgrammerShortTermMemory")]
    public class ProgrammerShortTermMemory : IAgentShortTermMemory
    {
        /// <summary>
        /// Our top-down conception of what we need to do to accomplish the feature goal.
        /// </summary>
        public CodingTasks CodingTasks { get; set; }

        /// <summary>
        /// The current index to the above coding tasks.
        /// </summary>
        public int CodingTaskStep { get; set; }

        /// <summary>
        /// Storage for when observations or memory are summarized into their most relevant form.
        /// </summary>
        public JToken Conclusions { get; set; }

        /// <summary>
        /// The entries needed to regenerate the WorkingSet.  We can cull these entries to fit within the context window.
        /// </summary>
        [JsonIgnoreInPrompt]
        public List<RepositoryQueryEntry> RepositoryQueryEntries { get; set; }

        /// <summary>
        /// Working set memory, of the most relevant pieces of information for the given goal (e.g. subset of repository text).
        /// </summary>
        [JsonIgnoreInPrompt]
        public string WorkingSet {  get; set; }

        public ProgrammerShortTermMemory()
        {
            RepositoryQueryEntries = new List<RepositoryQueryEntry>();
        }

        public string ToJson()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new JsonPromptContractResolver()
            };

            return JsonConvert.SerializeObject(this, settings);
        }
    }
}
