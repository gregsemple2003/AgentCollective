namespace BizDevAgent.DataStore
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class TypeIdAttribute : Attribute
    {
        public string Id { get; private set; }

        public TypeIdAttribute(string id)
        {
            Id = id;
        }
    }
}
