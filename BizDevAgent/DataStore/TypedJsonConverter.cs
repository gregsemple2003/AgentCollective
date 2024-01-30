using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;

namespace BizDevAgent.DataStore
{
    /// <summary>
    /// Converter which embeds the object type in the JSON.  Use the [TypeId] class attribute to define the type name.
    /// </summary>
    public class TypedJsonConverter : JsonConverter
    {
        private static bool _isInitialized;
        private static readonly Dictionary<string, Type> _typeIdToType;
        private static readonly Dictionary<Type, string> _typeToTypeId;

        private readonly IServiceProvider _serviceProvider;

        static TypedJsonConverter()
        {
            _typeIdToType = new Dictionary<string, Type>();
            _typeToTypeId = new Dictionary<Type, string>();
        }

        public TypedJsonConverter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
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
            return GetTypeId(type) != string.Empty;
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
            JObject jo = JObject.FromObject(value);
            var typeAttr = value.GetType().GetCustomAttribute<TypeIdAttribute>();
            if (typeAttr != null)
            {
                jo.Add("TypeId", typeAttr.Id);
            }
            jo.WriteTo(writer);
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
