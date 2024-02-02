using BizDevAgent.DataStore;
using BizDevAgent.Model;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace BizDevAgent.Jobs
{
    /// <summary>
    /// For each company in the database, use a webcrawler to access the company website to gather information:
    ///     - emails
    /// </summary>
    [TypeId("UpdateCompanyWebsitesJob")]
    public class UpdateCompanyWebsitesJob : Job
    {
        private readonly WebsiteDataStore _websiteDataStore;
        private readonly CompanyDataStore _companyDataStore;

        private const int WorkerCount = 30;

        public UpdateCompanyWebsitesJob(WebsiteDataStore websiteDataStore, CompanyDataStore companyDataStore) 
        {
            _websiteDataStore = websiteDataStore;
            _companyDataStore = companyDataStore;
        }

        public override Task UpdateScheduledRunTime()
        {
            ScheduledRunTime = ScheduledRunTime.AddDays(3);
            return Task.CompletedTask;
        }

        public async override Task Run()
        {
            // Process companies until all websites are crawled.
            var companies = await _companyDataStore.LoadAll(forceRemote: true);
            var companyQueue = new ConcurrentQueue<Company>(companies);

            // Create a number of workers which drain the queue of work until empty
            var workerTasks = new List<Task>();
            for (int i = 0; i < WorkerCount; i++)
            {
                workerTasks.Add(Task.Run(async () =>
                {
                    while (companyQueue.TryDequeue(out var company))
                    {
                        var website = await _websiteDataStore.Load(company.Url, $"{company.Index} / {companies.Count}");
                        company.Emails = website.ExtractedEmails;
                    }
                }));
            }

            // Wait until the work queue is done
            await Task.WhenAll(workerTasks);

            // Save any mutations in companies
            await _companyDataStore.SaveAll();
        }
    }
}
