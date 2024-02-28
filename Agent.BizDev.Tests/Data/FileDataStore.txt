using Newtonsoft.Json;
using BizDevAgent.Utilities;
using System.Reflection;

namespace BizDevAgent.DataStore
{
    /// <summary>
    /// A data store where all entities are fetched from a remote endpoint (like a website) but 
    /// cached locally in a single JSON file.
    /// </summary>
    public abstract class FileDataStore<TEntity>
        where TEntity : class
    {
        private readonly string _fileName;

        public List<TEntity> All { get; private set; }

        private readonly JsonSerializerSettings _settings;

        public FileDataStore(string fileName, JsonSerializerSettings settings = null)
        {
            _fileName = fileName;

            All = new List<TEntity>();

            if (settings == null)
            {
                var contractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                {
                    IgnoreSerializableInterface = true,
                    IgnoreSerializableAttribute = true
                };
                contractResolver.DefaultMembersSearchFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                settings = new JsonSerializerSettings
                {
                    ContractResolver = contractResolver
                };
            }

            _settings = settings;
        }

        /// <summary>
        /// Load all the entities in this data store.  If forceRemote == true, fetch entities from the remote source.
        /// Otherwise fetch from the local cache.
        /// </summary>
        public async Task<List<TEntity>> LoadAll(bool forceRemote = false)
        {
            // Check cache and return if exists
            if (!forceRemote)
            {
                All = await GetLocal();
                if (All != null)
                {
                    return All;
                }
            }

            // Get from the remote source
            var updated = await GetRemote();

            // We don't want to lose any data that we've built-up with the existing objects, and stored
            // after long-running jobs have completed since its expensive to rebuild this information.
            // So we only update non-default fields/properties from the list, otherwise we leave the 
            // existing data untouched.
            foreach (var newObj in updated)
            {
                var existingObj = All.Find(o => GetKey(newObj) == GetKey(o));
                if (existingObj != null)
                {
                    ObjectMerger.Merge(newObj, existingObj);
                }
                else
                {
                    All.Add(newObj);
                }
            }

            // Save locally to speed up the next request
            await SaveLocal();

            return All;
        }

        public async Task SaveAll()
        {
            // Save locally to speed up the next request
            await SaveLocal();
        }

        private async Task SaveLocal()
        {
            var json = JsonConvert.SerializeObject(All, Formatting.Indented, _settings);
            await File.WriteAllTextAsync(_fileName, json);
        }

        private async Task<List<TEntity>> GetLocal()
        {
            // Check if the file exists
            if (!File.Exists(_fileName))
            {
                // Return an empty list or handle accordingly
                return null;
            }

            // Read the file content
            var json = await File.ReadAllTextAsync(_fileName);

            // Deserialize the JSON content back into a List<Company>
            var all = JsonConvert.DeserializeObject<List<TEntity>>(json, _settings);
            return all;
        }

        protected abstract Task<List<TEntity>> GetRemote();

        protected abstract string GetKey(TEntity entity);
    }
}
