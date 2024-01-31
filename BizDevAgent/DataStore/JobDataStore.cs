using BizDevAgent.Model;
using Newtonsoft.Json;

namespace BizDevAgent.DataStore
{
    public class JobDataStore : FileDataStore<Job>
    {
        public JobDataStore(string path, IServiceProvider serviceProvider)
            : base(path, CreateJsonSerializerSettings(serviceProvider), forceRemote: false)
        {
        }

        protected override async Task<List<Job>> GetRemote()
        {
            return new List<Job>();
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
