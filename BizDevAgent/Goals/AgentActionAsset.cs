using BizDevAgent.DataStore;

namespace BizDevAgent.Goals
{
    [TypeId("AgentAction")]
    public class AgentActionAsset : JsonAsset
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public string PromptTemplatePath { get; set; }
        public PromptAsset PromptBuilder { get; set; }
        public string Description {  get; set; }

        public void Bind(PromptContext promptContext)
        {
            Description = PromptBuilder.Evaluate(promptContext);
        }

        internal override void PostLoad(AssetDataStore assetDataStore)
        {
            if (!string.IsNullOrEmpty(PromptTemplatePath))
            {
                PromptBuilder = (PromptAsset)assetDataStore.Get(PromptTemplatePath).GetAwaiter().GetResult();
            }
        }
    }
}
 