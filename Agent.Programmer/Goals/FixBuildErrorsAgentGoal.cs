using Agent.Core;
using Agent.Services;
using FluentResults;
using System.Text;

namespace Agent.Programmer
{
    [TypeId("FixBuildErrorsAgentGoal")]
    public class FixBuildErrorsAgentGoal : ProgrammerAgentGoal
    {
        private readonly RepositoryQuerySession _targetRepositoryQuerySession;
        private readonly RepositoryQuerySession _selfRepositoryQuerySession;
        private readonly IBuildCommand _buildCommand;
        private Result<BuildResult> _buildResult;

        public FixBuildErrorsAgentGoal(AgentGoalSpec spec)
            : base(spec)
        {
            _selfRepositoryQuerySession = ProgrammerContext.Current.SelfRepositoryQuerySession;
            _targetRepositoryQuerySession = ProgrammerContext.Current.TargetRepositoryQuerySession;
            _buildCommand = ProgrammerContext.Current.ImplementFeatureJob.BuildCommand;
        }

        protected override async Task PrePromptCustom(AgentState agentState)
        {
            if (!agentState.TryGetGoal(out var currentGoal)) return;

            _buildResult = await _buildCommand.Build(_targetRepositoryQuerySession.LocalRepoPath);
            if (_buildResult.IsSuccess)
            {
                currentGoal.MarkDone();
            }
        }

        protected override void PopulatePromptCustom(AgentPromptContext promptContext, AgentState agentState)
        {
            if (_buildResult != null)
            {
                var sb = new StringBuilder();
                foreach (var error in _buildResult.Errors)
                {
                    var buildError = (error as BuildError);
                    if (buildError != null)
                    {
                        sb.AppendLine(buildError.RawMessage);
                    }
                }
                promptContext.AdditionalData["BuildErrors"] = sb.ToString();
            }
        }

        protected override Task ProcessResponseCustom(string prompt, string response, AgentState agentState, IResponseParser languageModelParser)
        {
            return base.ProcessResponseCustom(prompt, response, agentState, languageModelParser);
        }

        protected override bool ShouldRequestPromptCustom(AgentState agentState)
        {
            return _buildResult.IsFailed;
        }
    }
}
