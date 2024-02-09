using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Utilities;

namespace BizDevAgent.Jobs
{
    /// <summary>
    /// Modify the local source code using "git apply" to apply a diff patch to the codebase.
    /// </summary>
    [TypeId("ProgrammerModifyCode")]
    public class ProgrammerModifyCode : Job
    {
        private readonly GitAgent _gitAgent;
        private readonly string _diffFileContents;

        public ProgrammerModifyCode(GitAgent gitAgent, string diffFileContents) 
        { 
            _gitAgent = gitAgent;
            _diffFileContents = diffFileContents;
        }

        public async override Task Run()
        {
            var diffFilePath = Path.Combine(Paths.GetSourceControlRootPath(), "file.diff");
            File.WriteAllText(diffFilePath, _diffFileContents);
            //await _gitAgent.ApplyDiff(diffFilePath);
        }
    }
}
