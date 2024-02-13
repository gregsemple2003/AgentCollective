using BizDevAgent.Model;
using BizDevAgent.Services;

namespace BizDevAgent.DataStore
{
    public class RepositorySummaryDataStore : RocksDbDataStore<RepositoryNode>
    {
        public RepositorySummaryDataStore(string path) : base(path) 
        { 
        }

        protected override string NormalizeKey(string key)
        {
            return key.Replace('\\', '/');
        }

        protected override string GetKey(RepositoryNode entity)
        {
            return entity.Key;
        }
    }
}
