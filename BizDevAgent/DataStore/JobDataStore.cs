using BizDevAgent.Jobs;
using Newtonsoft.Json;

namespace BizDevAgent.DataStore
{
    public class JobDataStore : SingleFileDataStore<Job>
    {
        public JobDataStore(string path, IServiceProvider serviceProvider)
            : base(path, CreateJsonSerializerSettings(serviceProvider))
        {
        }

        protected override string GetKey(Job job)
        {
            return job.Name;
        }

        protected override Task<List<Job>> GetRemote()
        {
            return Task.FromResult(new List<Job>());
        }

        private static JsonSerializerSettings CreateJsonSerializerSettings(IServiceProvider serviceProvider)
        {
            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new TypedJsonConverter(serviceProvider) }
            };
            return settings;
        }
    }
}
