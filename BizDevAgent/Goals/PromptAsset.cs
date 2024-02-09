using HandlebarsDotNet;
using BizDevAgent.DataStore;

namespace BizDevAgent.Goals
{
    public class PromptContext
    {
        public List<AgentObservation> Observations { get; set; }
        public List<AgentActionAsset> Actions { get; set; }
        public List<AgentVariable> Variables { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }

        public PromptContext() 
        { 
            Observations = new List<AgentObservation>();
            Actions = new List<AgentActionAsset>();
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

    public class PromptAsset : Asset
    {
        private readonly HandlebarsTemplate<object, object> _promptTemplate;

        public PromptAsset(string promptTemplateText) 
        {
            _promptTemplate = Handlebars.Compile(promptTemplateText);
        }

        public string Evaluate(PromptContext promptContext)
        {
            return _promptTemplate(promptContext);
        }
    }
}
