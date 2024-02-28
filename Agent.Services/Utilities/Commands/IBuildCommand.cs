using FluentResults;

namespace Agent.Services
{
    public class BuildResult
    {
        public string Output { get; set; }
    }

    /// <summary>
    /// Builds a project producing artifacts such as executables etc.
    /// </summary>
    public interface IBuildCommand
    {
        public Task<Result<BuildResult>> Build(string rootRepoPath);
    }
}
