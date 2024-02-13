using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Reflection;

namespace BizDevAgent.DataStore
{
    /// <summary>
    /// Converter which embeds the object type in the JSON.  Use the [TypeId] class attribute to define the type name.
    /// Also allows string references to be looked-up in an IDataStore, if one is provided.
    /// </summary>
    public class TypedJsonConverter : JsonConverter
    {
        private static bool _isInitialized;
        private static readonly Dictionary<string, Type> _typeIdToType;
        private static readonly Dictionary<Type, string> _typeToTypeId;

        public readonly List<DataStoreEntity> PendingPostLoads;

        private readonly IServiceProvider _serviceProvider;
        private readonly IDataStore _dataStore;

        static TypedJsonConverter()
        {
            _typeIdToType = new Dictionary<string, Type>();
            _typeToTypeId = new Dictionary<Type, string>();
        }

        public TypedJsonConverter(IServiceProvider serviceProvider, IDataStore dataStore = null)
        {
            _serviceProvider = serviceProvider;
            _dataStore = dataStore;
            PendingPostLoads = new List<DataStoreEntity>();
        }

        public static string GetTypeId(Type type)
        {
            if (!_isInitialized)
            {
                GatherAllClassesWithTypeId();
                    
                _isInitialized = true;
            }

            if (_typeToTypeId.ContainsKey(type))
            {
                return _typeToTypeId[type];
            }

            return string.Empty;
        }

        public override bool CanConvert(Type type)
        {          
            var typeId = GetTypeId(type);
            return typeId != string.Empty;
        }

        private static void GatherAllClassesWithTypeId()
        {
            _typeIdToType.Clear();
            _typeToTypeId.Clear();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    var attribute = type.GetCustomAttribute<TypeIdAttribute>();
                    if (attribute != null)
                    {
                        if (!_typeIdToType.ContainsKey(attribute.Id))
                        {
                            _typeIdToType.Add(attribute.Id, type);
                            _typeToTypeId.Add(type, attribute.Id);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Duplicate TypeId '{attribute.Id}' found in '{type.FullName}'. It will be ignored.");
                        }
                    }
                }
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var typeAttr = value.GetType().GetCustomAttribute<TypeIdAttribute>();
            var refAttr = value.GetType().GetCustomAttribute<EntityReferenceTypeAttribute>();
            if (typeAttr != null)
            {
                JObject jo = JObject.FromObject(value);
                jo.Add("TypeId", typeAttr.Id);
                jo.WriteTo(writer);
            }
            else if (refAttr != null)
            {
                var enttiy = (DataStoreEntity)value;
            }
        }

        public object ReadJsonWithoutConverter(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Load JObject from the reader
            JObject jo = JObject.Load(reader);

            // Create a new JsonSerializer that doesn't use the converter to avoid recursion
            JsonSerializer newSerializer = new JsonSerializer();
            foreach (var converter in serializer.Converters)
            {
                if (converter != this) // Bypass this converter
                {
                    newSerializer.Converters.Add(converter);
                }
            }

            // Use the new serializer to deserialize the object
            return jo.ToObject(objectType, newSerializer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // References to other objects within the same data store can be resolved by string key.
            if (reader.TokenType == JsonToken.String)
            {
                var key = reader.Value.ToString();
                var obj = _dataStore.Get<object>(key).GetAwaiter().GetResult();
                if (!string.IsNullOrEmpty(key) && obj == null)
                {
                    throw new InvalidOperationException($"Failure to resolve object with key '{key}' in data store {_dataStore.GetType()}");
                }

                PendingPostLoads.Add(obj as DataStoreEntity);
                return obj;
            }

            JObject jo = JObject.Load(reader);
            var typeId = jo["TypeId"]?.Value<string>();

            if (string.IsNullOrEmpty(typeId))
            {
                using (var subReader = jo.CreateReader())
                {
                    // If typeId is null or empty, deserialize using the expected objectType
                    return ReadJsonWithoutConverter(subReader, objectType, existingValue, serializer);
                }
            }

            if (_typeIdToType.TryGetValue(typeId, out Type targetType))
            {
                object instance;

                // Try to resolve the target type from the service provider
                instance = _serviceProvider.GetService(targetType) ?? Activator.CreateInstance(targetType);

                if (instance is DataStoreEntity)
                {
                    PendingPostLoads.Add((DataStoreEntity)instance);
                }

                // Deserialize the JSON data onto the instance
                using (var subReader = jo.CreateReader())
                {
                    serializer.Populate(subReader, instance);
                }

                return instance;
            }
            else
            {
                throw new JsonSerializationException($"Could not resolve type id: '{typeId}'");
            }
        }
    }
}
