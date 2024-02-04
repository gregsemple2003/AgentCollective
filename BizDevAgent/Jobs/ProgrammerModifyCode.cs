using BizDevAgent.DataStore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.Jobs
{
    /// <summary>
    /// Modify the local source code using "git apply" to apply a diff patch to the codebase.
    /// </summary>
    [TypeId("ProgrammerModifyCode")]
    public class ProgrammerModifyCode : Job
    {
        public ProgrammerModifyCode(string diffFileContents) 
        { 
        }
    }
}
