using Agent.Core;

namespace Agent.Services
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
