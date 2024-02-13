using HandlebarsDotNet;
using BizDevAgent.DataStore;

namespace BizDevAgent.Agents
{
    public class AgentPromptContext
    {
        public List<AgentGoal> Goals { get; set; }
        public List<AgentObservation> Observations { get; set; }
        public List<AgentGoal> OptionalSubgoals { get; set; }
        public List<AgentVariable> Variables { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }
        public string FeatureSpecification { get; set; }
        public bool ShouldDisplayActions { get; set; }
        public bool ShouldDisplayObservations { get; set; }

        public AgentPromptContext() 
        { 
            Observations = new List<AgentObservation>();
            OptionalSubgoals = new List<AgentGoal>();
            Variables = new List<AgentVariable>();
            AdditionalData = new Dictionary<string, object>();
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
