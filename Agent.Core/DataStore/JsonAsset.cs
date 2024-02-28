using Newtonsoft.Json;

namespace Agent.Core
{
    [TypeId("JsonAsset")]
    public class JsonAsset : Asset
    {
    }

    public class JsonAssetFactory : IAssetFactory
    {
        private readonly JsonSerializerSettings _settings;
        private readonly TypedJsonConverter _converter;

        public JsonAssetFactory(JsonSerializerSettings settings, TypedJsonConverter converter)
        {
            _settings = settings;
            _converter = converter;
        }

        public object Create(string filePath)
        {
            using (var reader = File.OpenText(filePath))
            {
                var fileContent = reader.ReadToEnd();
                var obj = JsonConvert.DeserializeObject<JsonAsset>(fileContent, _settings);
                return obj;
            }
        }
    }
}
