using FluentResults;

namespace BizDevAgent.Agents
{
    public class BuildResult
    {
        public string Output { get; set; }
    }

    /// <summary>
    /// Builds a project producing artifacts such as executables etc.
    /// </summary>
    public interface IBuildAgent
    {
        public Task<Result<BuildResult>> Build(string rootRepoPath);
    }
}
