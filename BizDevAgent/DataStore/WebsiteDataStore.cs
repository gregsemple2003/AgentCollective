using BizDevAgent.Model;
using RocksDbSharp;

namespace BizDevAgent.DataStore
{
    public class WebsiteDataStore
    {
        /// <summary>
        /// For debugging purposes, use to clear the cache.
        /// </summary>
        public static readonly bool ShouldWipeDatabase = false;

        private RocksDb _pageCacheDb;

        public WebsiteDataStore(string path)
        {
            var options = new DbOptions().SetCreateIfMissing(true);
            Directory.CreateDirectory(path);
            _pageCacheDb = RocksDb.Open(options, path);

            if (ShouldWipeDatabase)
            {
                using (var iterator = _pageCacheDb.NewIterator())
                {
                    iterator.SeekToFirst();
                    while (iterator.Valid())
                    {
                        _pageCacheDb.Remove(iterator.Key());
                        iterator.Next();
                    }
                }
            }
        }
        public async Task<Website> Load(string rootUrl, string tag = "")
        {
            var priorityKeywords = new List<string> { "career", "contact", "jobs", "work", "who", "press" };
            var crawler = new PuppeteerWebsiteCrawler(priorityKeywords, tag, _pageCacheDb);
            await crawler.Start(rootUrl);

            return new Website
            {
                ExtractedEmails = crawler.ExtractedEmails
            };
        }
    }
}
