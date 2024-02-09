using HandlebarsDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.Goals
{
    public class PromptData
    {
        public List<AgentObservation> Observations { get; set; }
        public List<AgentAction> Actions { get; set; }
    }

    public class PromptBuilder
    {
        private readonly HandlebarsTemplate<object, object> _promptTemplate;

        public PromptBuilder(string promptTemplateText) 
        {
            _promptTemplate = Handlebars.Compile(promptTemplateText);
        }

        public string Evaluate(PromptData promptData)
        {
            return _promptTemplate(promptData);
        }
    }
}
