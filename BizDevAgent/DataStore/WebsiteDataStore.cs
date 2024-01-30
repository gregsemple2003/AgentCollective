using BizDevAgent.Model;
using PuppeteerSharp;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDevAgent.DataStore
{
    public class WebsiteDataStore
    {
        /// <summary>
        /// For debugging purposes, use to clear the cache.
        /// </summary>
        public const bool ShouldWipeDatabase = false;

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

            TerminateChromeProcessesForTesting();
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

        private static void TerminateChromeProcessesForTesting()
        {
            // PuppeteerSharp seems to leak chrome.exe processes, so we kill them manually.
            foreach (var process in Process.GetProcessesByName("chrome"))
            {
                try
                {
                    if (process.MainModule.FileVersionInfo.FileDescription == "Google Chrome for Testing")
                    {
                        process.Kill();
                        Console.WriteLine($"Terminated chrome.exe with PID: {process.Id}");
                    }
                }
                catch (Exception ex)
                {
                    // Handle any exceptions, such as access denied
                    Console.WriteLine($"Error terminating process {process.Id}: {ex.Message}");
                }
            }
        }

    }
}
