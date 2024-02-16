using AngleSharp.Dom;
using Newtonsoft.Json;
using System.IO;

namespace BizDevAgent.DataStore
{
    public interface IAssetFactory
    {
        object Create(string filePath);
    }

    public class DataStoreEntity
    { 
        public string Key { get; set; }
    }

    public class MultiFileDataStore<TEntity> : IDataStore 
        where TEntity : DataStoreEntity
    {
        protected readonly JsonSerializerSettings _settings;

        private readonly string _baseDirectory;
        private readonly TypedJsonConverter _typedConverter;
        private readonly Dictionary<string, TEntity> _cache = new Dictionary<string, TEntity>();
        private readonly Dictionary<string, string> _filePathCache = new Dictionary<string, string>();
        private readonly Dictionary<string, IAssetFactory> _factories = new Dictionary<string, IAssetFactory>();
        private bool _isCacheBuilt = false;

        public MultiFileDataStore(string baseDirectory, IServiceProvider serviceProvider, JsonSerializerSettings settings = null)
        {
            _baseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory));
            Directory.CreateDirectory(_baseDirectory); // Ensure directory exists

            _typedConverter = new TypedJsonConverter(serviceProvider, this);
            _settings = settings ?? new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { _typedConverter },
                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                {
                    IgnoreSerializableInterface = true,
                    IgnoreSerializableAttribute = true,
                    DefaultMembersSearchFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                }
            };

            RegisterFactory(".json", new JsonAssetFactory(_settings, _typedConverter));
        }

        public void RegisterFactory<TFactory>(string extension, TFactory factory) where TFactory : IAssetFactory
        {
            _factories[extension] = factory;
        }

        protected virtual void PostLoad(TEntity entity) 
        { 
        }

        private int _getRecursionCount;

        public TEntity Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or whitespace.", nameof(key));

            try
            {
                _getRecursionCount++;

                if (_cache.TryGetValue(key, out TEntity cachedEntity))
                {
                    return cachedEntity;
                }

                // Build the file path cache if it hasn't been built yet
                if (!_isCacheBuilt)
                {
                    BuildFilePathCache();
                    _isCacheBuilt = true;
                }

                if (_filePathCache.TryGetValue(key, out string filePath))
                {
                    var extension = Path.GetExtension(filePath).ToLower();
                    if (_factories.TryGetValue(extension, out IAssetFactory factory))
                    {
                        // Create entity
                        var entity = (TEntity)factory.Create(filePath);
                        entity.Key = key;
                        _cache[key] = entity;

                        return entity;
                    }
                }

            }
            finally
            {
                _getRecursionCount--;

                // The last call to Get is responsible for dispatching PostLoads.
                // Execute post loads in reverse load order, children first
                if (_getRecursionCount == 0)
                {
                    foreach (var loadedEntity in _typedConverter.PendingPostLoads)
                    {
                        PostLoad((TEntity)loadedEntity);
                    }
                    _typedConverter.PendingPostLoads.Clear();
                }
            }

            return null; // File not found
        }

        public async Task<TChild> Get<TChild>(string key) where TChild : class
        {
            var result = Get(key);
            var child = result as TChild;
            if (child == null && result != null)
            {
                throw new InvalidCastException($"Cannot convert type '{typeof(TEntity).Name}' to '{typeof(TChild).Name}'.");
            }
            return child;
        }

        private void BuildFilePathCache()
        {
            var jsonFiles = Directory.GetFiles(_baseDirectory, "*.*", SearchOption.AllDirectories);
            foreach (var filePath in jsonFiles)
            {
                var key = Path.GetFileNameWithoutExtension(filePath);
                if (!_filePathCache.ContainsKey(key)) // Consider what to do if there are duplicates
                {
                    _filePathCache[key] = filePath;
                }
            }
        }
    }
}
