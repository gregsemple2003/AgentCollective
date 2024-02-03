using BizDevAgent.Model;

namespace BizDevAgent.DataStore
{
    public class SourceSummaryDataStore : RocksDbDataStore<SourceSummary>
    {
        public SourceSummaryDataStore(string path) : base(path) 
        { 
        }

        protected override string GetKey(SourceSummary entity)
        {
            return entity.Key;
        }
    }
}
