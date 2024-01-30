using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.Model
{
    public class Website
    {
        public Website() { }

        public List<string> ExtractedEmails { get; set; } = new List<string>();
    }
}
