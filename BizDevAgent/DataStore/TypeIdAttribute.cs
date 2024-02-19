namespace BizDevAgent.DataStore
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
    public class TypeIdAttribute : Attribute
    {
        public string Id { get; private set; }

        public TypeIdAttribute(string id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Serialize entity references as string key.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class EntityReferenceTypeAttribute : Attribute
    {
        public EntityReferenceTypeAttribute()
        {
        }
    }

}
