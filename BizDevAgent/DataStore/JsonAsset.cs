using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.DataStore
{
    [TypeId("JsonAsset")]
    public class JsonAsset : Asset
    {
    }

    public class JsonAssetFactory : IAssetFactory
    {
        private readonly JsonSerializerSettings _settings;

        public JsonAssetFactory(JsonSerializerSettings settings)
        {
            _settings = settings;
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
