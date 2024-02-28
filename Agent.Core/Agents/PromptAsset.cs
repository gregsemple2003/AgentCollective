using HandlebarsDotNet;

namespace Agent.Core
{
    public class AgentPromptContext
    {
        public List<AgentGoal> Goals { get; set; }
        public List<AgentObservation> Observations { get; set; }
        public List<AgentGoalSpec> OptionalSubgoals { get; set; }
        public string ShortTermMemoryJson { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }
        public string FeatureSpecification { get; set; } // TODO gsemple: should be additional data?
        public bool ShouldDisplayActions { get; set; }
        public bool ShouldDisplayObservations { get; set; }
        public List<AgentReminder> Reminders { get; set; } // At the end of the prompt, ensuring higher likelihood that it won't get lost in large prompts.

        public AgentPromptContext() 
        { 
            Observations = new List<AgentObservation>();
            OptionalSubgoals = new List<AgentGoalSpec>();
            AdditionalData = new Dictionary<string, object>();
            Reminders = new List<AgentReminder>();
        }
    }

    public class PromptAssetFactory : IAssetFactory
    {
        public PromptAssetFactory()
        {
        }

        public object Create(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                var fileContent = reader.ReadToEnd();
                return new PromptAsset(fileContent);
            }
        }
    }

    [TypeId("Prompt")]
    public class PromptAsset : Asset
    {
        private readonly HandlebarsTemplate<object, object> _promptTemplate;

        public PromptAsset(string promptTemplateText) 
        {
            _promptTemplate = Handlebars.Compile(promptTemplateText);
        }

        public string Evaluate(AgentPromptContext promptContext)
        {
            return _promptTemplate(promptContext);
        }
    }
}
