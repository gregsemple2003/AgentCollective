namespace BizDevAgent.DataStore
{
    public interface IDataStore
    {
        /// <summary>
        /// Asynchronously retrieves an entity of a specified type based on a key.
        /// </summary>
        Task<T> Get<T>(string key) where T : class;
    }
}
