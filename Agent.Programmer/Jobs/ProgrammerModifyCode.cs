using Agent.Core;
using Agent.Services;

namespace Agent.Programmer
{
    /// <summary>
    /// Modify the local source code using "git apply" to apply a diff patch to the codebase.
    /// </summary>
    [TypeId("ProgrammerModifyCode")]
    public class ProgrammerModifyCode : Job
    {
        private readonly GitService _gitService;
        private readonly string _diffFileContents;

        public ProgrammerModifyCode(GitService gitService, string diffFileContents) 
        { 
            _gitService = gitService;
            _diffFileContents = diffFileContents;
        }

        public override Task Run()
        {
            var diffFilePath = Path.Combine(Paths.GetSourceControlRootPath(), "file.diff");
            File.WriteAllText(diffFilePath, _diffFileContents);
            return Task.CompletedTask;
        }
    }
}
