using Newtonsoft.Json;
using RocksDbSharp;
using System.Reflection;

namespace BizDevAgent.DataStore
{
    public abstract class RocksDbDataStore<TEntity>
        where TEntity : class
    {
        /// <summary>
        /// All the series that have been loaded.
        /// </summary>
        public List<TEntity> All;

        protected readonly RocksDb _db; // private?
        private readonly bool _shouldWipe;
        private readonly JsonSerializerSettings _settings;

        protected RocksDbDataStore(string path, JsonSerializerSettings settings = null, bool shouldWipe = false)
        {
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
            _shouldWipe = shouldWipe;

            var options = new DbOptions().SetCreateIfMissing(true);
            Directory.CreateDirectory(path);
            _db = RocksDb.Open(options, path);

            if (_shouldWipe)
            {
                ClearDatabase();
            }
        }

        protected abstract string GetKey(TEntity entity);

        public void Add(TEntity entity, bool shouldOverwrite = false)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Generate a key for the entity
            var key = GetKey(entity);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("The key for the entity cannot be null or whitespace.", nameof(entity));
            }

            // Check for key conflict in the in-memory list
            var existingEntity = All.FirstOrDefault(e => GetKey(e) == key);
            if (existingEntity != null)
            {
                if (shouldOverwrite)
                {
                    All.Remove(existingEntity);
                }
                else
                {
                    throw new InvalidOperationException($"An entity with the key '{key}' already exists in-memory.");
                }
            }

            // Check for key conflict in the database 
            if (!shouldOverwrite && _db.HasKey(key))
            {
                throw new InvalidOperationException($"An entity with the key '{key}' already exists in the database.");
            }

            // Serialize the entity to JSON
            var json = JsonConvert.SerializeObject(entity, _settings);

            // Store the serialized entity in RocksDB
            _db.Put(key, json);

            // Also add to in-memory list for quick access
            All.Add(entity);
        }

        public Task<TEntity> Get(string key)
        {
            // Look first in in-memory cache 
            var existingEntity = All.Find(o => key == GetKey(o));
            if (existingEntity != null)
            {
                return Task.FromResult(existingEntity);
            }

            // Check db
            var json = _db.Get(key);
            if (string.IsNullOrWhiteSpace(key)) 
            {
                return Task.FromResult<TEntity>(null);
            }

            // Deserialize the JSON
            if (json != null)
            {
                var entity = JsonConvert.DeserializeObject<TEntity>(json, _settings);
                All.Add(entity);
                return Task.FromResult(entity);
            }

            return Task.FromResult<TEntity>(null);
        }

        protected void ClearDatabase()
        {
            using (var iterator = _db.NewIterator())
            {
                iterator.SeekToFirst();
                while (iterator.Valid())
                {
                    _db.Remove(iterator.Key());
                    iterator.Next();
                }
            }
        }
    }
}
