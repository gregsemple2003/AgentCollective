using System.Reflection;
using Agent.Services;
using Agent.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Programmer
{
    /// <summary>
    /// Given a feature specification, figure out what code to modify by querying the codebase for related logic.
    /// We use an LLM to make inferences based on the returned information about an implementation plan.
    /// </summary>
    [TypeId("ProgrammerResearchJob")]
    public class ProgrammerResearchJob : Job
    {
        private readonly VisualStudioService _visualStudioService;
        private readonly IServiceProvider _serviceProvider;
        private readonly JobRunner _jobRunner;

        public ProgrammerResearchJob(VisualStudioService visualStudioService, IServiceProvider serviceProvider, JobRunner jobRunner)
        {
            _visualStudioService = visualStudioService;
            _serviceProvider = serviceProvider;
            _jobRunner = jobRunner;
        }

        public async override Task Run()
        {
            var codePath = Path.Combine(Paths.GetSourceControlRootPath(), "BizDevAgent", "Jobs", "Injected", "CodeResearchJob_ImplementXMLSerialization.txt");
            var code = File.ReadAllText(codePath);
            var assembly = _visualStudioService.InjectCode(code);

            // Assuming you know the type name and namespace
            Type jobType = FindJobType(assembly, "CodeResearchJob");
            var job = (Job)ActivatorUtilities.CreateInstance(_serviceProvider, jobType);
            var jobResult = await _jobRunner.RunJob(job);
        }

        private Type FindJobType(Assembly assembly, string jobTypePattern) 
        { 
            foreach (var type in assembly.GetTypes()) 
            { 
                if (type.Name.Contains(jobTypePattern))
                {
                    return type;
                }
            }

            return null;
        }
    }
}
