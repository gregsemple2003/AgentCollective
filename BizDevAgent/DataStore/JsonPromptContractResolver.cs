using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Reflection;

namespace BizDevAgent.DataStore
{
    public class JsonPromptContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            // Check if the property has the JsonIgnoreInPrompt attribute
            var hasJsonIgnoreInPromptAttribute = member.GetCustomAttribute<JsonIgnoreInPromptAttribute>() != null;

            if (hasJsonIgnoreInPromptAttribute)
            {
                property.ShouldSerialize = _ => false;
            }

            return property;
        }
    }
}