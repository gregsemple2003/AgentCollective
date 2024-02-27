using BizDevAgent.Agents;
using BizDevAgent.DataStore;
using BizDevAgent.Jobs;
using BizDevAgent.Services;
using BizDevAgent.Utilities;

namespace BizDevAgent.Flow
{
    [TypeId("GetWorkingSetPatchAgentGoal")]
    public class GetWorkingSetPatchAgentGoal : ProgrammerAgentGoal
    {
        private readonly RepositoryQuerySession _targetRepositoryQuerySession;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;
        private readonly IServiceProvider _serviceProvider;

        public GetWorkingSetPatchAgentGoal(IServiceProvider serviceProvider, AgentGoalSpec spec)
            : base(spec)
        {
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
            _targetRepositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
            _serviceProvider = serviceProvider;
        }

        protected override bool ShouldRequestPromptCustom(AgentState agentState)
        {
            return true;
        }

        protected override void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState)
        {
            base.PopulatePromptCustom(promptContext, agentState);
        }

        protected override async Task ProcessResponseCustom(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            if (!agentState.TryGetGoal(out var currentGoal)) return;

            var snippets = languageModelParser.ExtractSnippets(response);
            var programmerAgentState = (agentState as ProgrammerAgentState);

            if (currentGoal.Parent.Spec.Title.Contains("FixBuildErrors"))
            {
                var x = 3;
            }

            var repositoryFilePatches = DiffUtils.ParseCustomPatches(response);
            foreach (var repositoryFilePatch in repositoryFilePatches)
            {
                var repoFile = _targetRepositoryQuerySession.FindFileInRepo(repositoryFilePatch.FileName);
                if (repoFile != null)
                {
                    var patchedFileContents = DiffUtils.ApplyCustomPatch(repositoryFilePatch.Patch, repoFile.Contents);
                    var localRepoPath = _targetRepositoryQuerySession.LocalRepoPath;
                    var patchedFilePath = Path.Combine(localRepoPath, repoFile.FileName);
                    patchedFileContents = DiffUtils.FormatRepositoryFile(patchedFileContents);
                    var updateResult = await _targetRepositoryQuerySession.UpdateFileInRepo(repoFile.FileName, patchedFileContents);
                    if (updateResult.IsFailed)
                    {
                        throw new Exception($"Failure to update file '{repoFile.FileName}' due to '{updateResult}'");
                    }
                }
                else if (repoFile == null && repositoryFilePatch.IsNewFile)
                {
                    var newFileContents = DiffUtils.NewFileCustomPatch(repositoryFilePatch.Patch);
                    newFileContents = DiffUtils.FormatRepositoryFile(newFileContents);
                    var addResult = await _targetRepositoryQuerySession.AddFileToRepo(repositoryFilePatch.FileName, newFileContents);
                    if (addResult.IsFailed)
                    {
                        throw new Exception($"Failure to add file {addResult}");
                    }

                    // Add the file to working set
                    _targetRepositoryQuerySession.WorkingSetEntries.Clear();
                    _targetRepositoryQuerySession.PrintFileContents(addResult.Value.FileName);
                    programmerAgentState.ProgrammerShortTermMemory.WorkingSetEntries.AddRange(_targetRepositoryQuerySession.WorkingSetEntries);

                    // Figure out how to add the new file to working set
                    programmerAgentState.ProgrammerShortTermMemory.WorkingSet = await programmerAgentState.GenerateWorkingSet(programmerAgentState.ProgrammerShortTermMemory.WorkingSetEntries);
                }
            }

            currentGoal.MarkDone();
        }
    }
}
