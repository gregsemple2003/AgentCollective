using BizDevAgent.DataStore;
using BizDevAgent.Agents;

namespace BizDevAgent.Jobs
{
    /// <summary>
    /// For each game in the database, update game stats such as 
    ///     - review count
    ///     - engine type
    ///  Use the steam store page.
    /// </summary>
    [TypeId("ResearchCompanyJob")]
    public class ResearchCompanyJob : Job
    {
        private readonly CompanyDataStore _companyDataStore;
        private readonly WebSearchAgent _webSearchAgent;

        private const bool _force = true;

        public ResearchCompanyJob(CompanyDataStore companyDataStore, WebSearchAgent webSearchAgent)
        {
            _companyDataStore = companyDataStore;
            _webSearchAgent = webSearchAgent;
        }

        public override Task UpdateScheduledRunTime()
        {
            ScheduledRunTime = ScheduledRunTime.AddDays(1);
            return Task.CompletedTask;
        }

        public async override Task Run()
        {
            var batchCount = 0;
            var companies = await _companyDataStore.LoadAll(forceRemote: true);
            for (int i = 0; i < companies.Count; i++)
            {
                var company = companies[i];

                Console.WriteLine($"[{i} / {companies.Count}] Researching company '{company.Name}'");

                // Research company linkedin page
                if (!company.Tags.Contains("linkedincompany") || _force)
                {
                    var results = await _webSearchAgent.Search($"{company.Name} linkedin");
                    if (results.Count > 0)
                    {
                        var url = results[0].LinkUrl;
                        if (url.Contains("linkedin.com/company"))
                        {
                            company.LinkedInUrl = url;

                            Console.WriteLine($"[{i} / {companies.Count}] Found linkedin URL '{company.LinkedInUrl}'");
                        }
                        else
                        {
                            company.LinkedInFounderUrl = url;

                            Console.WriteLine($"[{i} / {companies.Count}] ERROR: not a company linkedin URL '{url}'");
                        }

                        company.Tags.AddUnique("linkedincompany");
                    }
                }

                batchCount++;
                if (batchCount > 10)
                {
                    await _companyDataStore.SaveAll();
                }
            }

            await _companyDataStore.SaveAll();
        }
    }
}
