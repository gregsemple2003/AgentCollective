using Agent.Core;
using Agent.Services;
using FluentResults;

namespace Agent.Programmer
{
    /// <summary>
    /// Process the response from the AI's research results.
    /// </summary>
    [TypeId("ModifyRepositoryCustomization")]
    public class ModifyRepositoryCustomization : AgentGoalCustomization
    {
        private readonly CodeAnalysisService _codeAnalysisService;
        private readonly IServiceProvider _serviceProvider;
        private readonly RepositoryQuerySession _targetRepositoryQuerySession;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;
        private readonly ProgrammerImplementFeatureJob _implementFeatureJob;
        private Result<BuildResult> _buildResult;

        public ModifyRepositoryCustomization(CodeAnalysisService codeAnalysisService, IServiceProvider serviceProvider)
        {
            _codeAnalysisService = codeAnalysisService;
            _serviceProvider = serviceProvider;
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
            _targetRepositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
            _implementFeatureJob = ProgrammerContext.Current.ImplementFeatureJob;
        }

        public override bool ShouldRequestPrompt(AgentState agentState)
        {
            if (agentState.TryGetGoal(out var currentGoal))
            {
                if (currentGoal.Spec.Key == "ModifyWorkingSet") // TODO gsemple: should get post load working so we can bind this goal as reference variable in ctor, or consider putting in goal data
                {
                    //return true;
                }
                else if (currentGoal.Spec.Key == "GetWorkingSetPatch")
                {
                    //return true;
                }
                else if (currentGoal.Spec.Key == "FixBuildErrors")
                {
                    //return _buildResult.IsFailed;
                }
            }

            return false;
        }

        public override void PopulatePrompt(AgentPromptContext promptContext, AgentState agentState)
        {
            if (!agentState.TryGetGoal(out var currentGoal)) return;
            var programmerAgentState = (agentState as ProgrammerAgentState);

            var programmerShortTermMemory = agentState.ShortTermMemory as ProgrammerShortTermMemory;
            promptContext.AdditionalData["CodingTaskStep"] = programmerShortTermMemory.CodingTaskStep;

            // Refresh working set since repo file contents may have changed
            programmerAgentState.UpdateWorkingSet().GetAwaiter().ToResult();
            promptContext.AdditionalData["WorkingSet"] = programmerShortTermMemory.WorkingSet;

            if (currentGoal.Spec.Key == "ModifyWorkingSet")
            {
                //var requiredMethodAttributes = new List<string>() { "WorkingSet" };
                //var agentApiSkeleton = programmerAgentState.GenerateAgentApiSkeleton(requiredMethodAttributes);
                //var agentApiSample = _selfRepositoryQuerySession.FindFileInRepo($"{nameof(RepositoryQueryJob)}.cs");
                //promptContext.AdditionalData["AgentApiSkeleton"] = agentApiSkeleton;
                //promptContext.AdditionalData["AgentApiSample"] = agentApiSample.Contents;
            }
            else if (currentGoal.Spec.Key == "FixBuildErrors")
            {
                //if (_buildResult != null)
                //{
                //    var sb = new StringBuilder();
                //    foreach(var error in _buildResult.Errors)
                //    {
                //        var buildError = (error as BuildError);
                //        if (buildError != null)
                //        {
                //            sb.AppendLine(buildError.RawMessage);
                //        }
                //    }
                //    promptContext.AdditionalData["BuildErrors"] = sb.ToString();
                //}
            }
        }

        public async override Task PrePrompt(AgentState agentState)
        {
            if (!agentState.TryGetGoal(out var currentGoal)) return;

            if (currentGoal.Spec.Key == "FixBuildErrors") // TODO gsemple: should get post load working so we can bind this goal as reference variable in ctor, or consider putting in goal data
            {
                _buildResult = await _implementFeatureJob.BuildCommand.Build(_targetRepositoryQuerySession.LocalRepoPath);
                if (_buildResult.IsSuccess)
                {
                    currentGoal.MarkDone();
                }
            }
            else if (currentGoal.Spec.Key == "RefineFixPlan")
            {
                throw new NotImplementedException();
            }
        }

        public override async Task ProcessResponse(string prompt, string response, AgentState agentState, ILanguageParser languageModelParser)
        {
            if (!agentState.TryGetGoal(out var currentGoal)) return;

            var snippets = languageModelParser.ExtractSnippets(response);
            var programmerAgentState = (agentState as ProgrammerAgentState);

            if (currentGoal.Spec.Key == "ModifyWorkingSet") // TODO gsemple: should get post load working so we can bind this goal as reference variable in ctor, or consider putting in goal data
            {
                //// Clear working set
                //programmerAgentState.ProgrammerShortTermMemory.WorkingSetEntries.Clear();

                //// Run the agent job and gather output
                //var researchJobOutput = string.Empty;
                //foreach (var snippet in snippets)
                //{
                //    if (snippet.LanguageId == "csharp")
                //    {
                //        var programmerShortTermMemory = agentState.ShortTermMemory as ProgrammerShortTermMemory;
                //        _targetRepositoryQuerySession.WorkingSetEntries.Clear();
                //        await programmerAgentState.RunAgentApiJob(snippet);

                //        programmerAgentState.ProgrammerShortTermMemory.WorkingSetEntries.AddRange(_targetRepositoryQuerySession.WorkingSetEntries);
                //        programmerAgentState.ProgrammerShortTermMemory.WorkingSet = await programmerAgentState.GenerateWorkingSet(programmerAgentState.ProgrammerShortTermMemory.WorkingSetEntries);
                //    }
                //}

                //var responseTokens = languageModelParser.ExtractResponseTokens(response);
                //if (snippets.Count == 0 && !responseTokens.Any(t => t.Contains("NoWorkingSet")))
                //{
                //    throw new Exception($"Invalid response, could not find snippet or token");
                //}

                //if (!string.IsNullOrWhiteSpace(researchJobOutput))
                //{
                //    agentState.Observations.Add(new AgentObservation() { Description = researchJobOutput });
                //}

                //currentGoal.MarkDone();
            }
            else if (currentGoal.Spec.Key == "GetWorkingSetPatch")
            {
                //var repositoryFilePatches = DiffUtils.ParseCustomPatches(response);
                //foreach(var repositoryFilePatch in repositoryFilePatches)
                //{
                //    var repoFile = _targetRepositoryQuerySession.FindFileInRepo(repositoryFilePatch.FileName);
                //    if (repoFile != null)
                //    {
                //        var patchedFileContents = DiffUtils.ApplyCustomPatch(repositoryFilePatch.Patch, repoFile.Contents);
                //        var localRepoPath = _targetRepositoryQuerySession.LocalRepoPath;
                //        var patchedFilePath = Path.Combine(localRepoPath, repoFile.FileName);
                //        patchedFileContents = DiffUtils.FormatRepositoryFile(patchedFileContents);
                //        var updateResult = await _targetRepositoryQuerySession.UpdateFileInRepo(repoFile.FileName, patchedFileContents);
                //        if (updateResult.IsFailed)
                //        {
                //            throw new Exception($"Failure to update file '{repoFile.FileName}' due to '{updateResult}'");
                //        }
                //    }
                //    else if (repoFile == null && repositoryFilePatch.IsNewFile)
                //    {
                //        var newFileContents = DiffUtils.NewFileCustomPatch(repositoryFilePatch.Patch);
                //        newFileContents = DiffUtils.FormatRepositoryFile(newFileContents);
                //        var addResult = await _targetRepositoryQuerySession.AddFileToRepo(repositoryFilePatch.FileName, newFileContents);
                //        if (addResult.IsFailed)
                //        {
                //            throw new Exception($"Failure to add file {addResult}");
                //        }

                //        // Add the file to working set
                //        _targetRepositoryQuerySession.WorkingSetEntries.Clear();
                //        _targetRepositoryQuerySession.PrintFileContents(addResult.Value.FileName);
                //        programmerAgentState.ProgrammerShortTermMemory.WorkingSetEntries.AddRange(_targetRepositoryQuerySession.WorkingSetEntries);

                //        // Figure out how to add the new file to working set
                //        programmerAgentState.ProgrammerShortTermMemory.WorkingSet = await programmerAgentState.GenerateWorkingSet(programmerAgentState.ProgrammerShortTermMemory.WorkingSetEntries);
                //    }
                //}

                //currentGoal.MarkDone();
            }
            else if (currentGoal.Spec.Key == "FixBuildErrors")
            {
                var x = 3;
            }

        }
    }
}
