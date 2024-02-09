using System;
using System.IO;
using System.Xml.Serialization;

namespace BizDevAgent.Utilities
{
    public static class XmlSerializationHelper
    {
        public static string SerializeToXml<T>(T obj)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var stringWriter = new StringWriter())
            {
                serializer.Serialize(stringWriter, obj);
                return stringWriter.ToString();
            }
        }

        public static T DeserializeFromXml<T>(string xml)
        {
            var serializer = new XmlSerializer(typeof(T));
            using (var stringReader = new StringReader(xml))
            {
                return (T)serializer.Deserialize(stringReader);
            }
        }
    }
}
