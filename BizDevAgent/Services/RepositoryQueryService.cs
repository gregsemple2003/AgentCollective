using BizDevAgent.DataStore;
using BizDevAgent.Utilities;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing.Printing;

namespace BizDevAgent.Services
{
    /// <summary>
    /// Defines a method as being part of the "agent api", which is exposed directly to LLMs for processing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AgentApiAttribute : Attribute
    {
        public AgentApiAttribute()
        {
        }
    }

    /// <summary>
    /// Marks a method as being appropriate in the context of an LLM building its "working set" which 
    /// is the subset of text in the repository that it should be aware of in order to accomplish a 
    /// specific task.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class WorkingSetAttribute : Attribute
    {
        public WorkingSetAttribute()
        {
        }
    }

    /// <summary>
    /// An agent that is called by LLMs with a simple API that dumps information to the console, and can 
    /// be piped back into prompts for the LLM to make decisions.  This isn't normally used directly by
    /// application code.  It is used by LLMs when preparing an implementation plan to modify source
    /// code.
    /// </summary>
    public class RepositoryQueryService : Service
    {
        private readonly RepositorySummaryDataStore _repositorySummaryDataStore;
        private readonly VisualStudioService _visualStudioService;
        private readonly GitService _gitService;
        private readonly Dictionary<string, RepositoryQuerySession> _sessionsCache = new Dictionary<string, RepositoryQuerySession>();

        public RepositoryQueryService(RepositorySummaryDataStore repositorySummaryDataStore, VisualStudioService visualStudioService, GitService gitService, IServiceProvider serviceProvider)
        {
            _repositorySummaryDataStore = repositorySummaryDataStore;
            _visualStudioService = visualStudioService;
            _gitService = gitService;
        }

        public RepositoryQuerySession CreateSession(string localRepoPath)
        {
            // Check if a session for the given path already exists in the cache
            if (!_sessionsCache.TryGetValue(localRepoPath, out RepositoryQuerySession session))
            {
                // If it doesn't exist, create a new session and add it to the cache
                session = new RepositoryQuerySession(this, _gitService, _repositorySummaryDataStore, localRepoPath);
                _sessionsCache[localRepoPath] = session;
            }

            // Return the existing or new session
            return session;
        }

    }
}
