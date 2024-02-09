using HtmlAgilityPack;
using PuppeteerSharp;
using System.Data;
using Newtonsoft.Json;
using System.Reflection;
using BizDevAgent.Model;
using BizDevAgent.Agents;

namespace BizDevAgent.DataStore
{
    public class CompanyDataStore : SingleFileDataStore<Company>
    {
        public const string CachedFileName = "companies.json";

        public List<Company> Companies { get; private set; }

        private readonly WebBrowsingAgent _browsingAgent;

        public CompanyDataStore(string path, WebBrowsingAgent browsingAgent) : base(path)
        {
            _browsingAgent = browsingAgent;
        }

        protected override async Task<List<Company>> GetRemote()
        {
            // Navigate to the Webpage
            var result = await _browsingAgent.BrowsePage("https://www.gamedevmap.com/index.php?location=&country=United%20States&state=&city=&query=&type=Developer&start=1&count=2000");
            var page = result.Value.Page;

            // Wait for the selector to ensure the elements are loaded
            await page.WaitForSelectorAsync("tr.row1");
            await page.WaitForSelectorAsync("tr.row2");

            // Select and iterate over the elements
            var companyCount = 0;
            var rows1 = await page.QuerySelectorAllAsync("tr.row1");
            var rows2 = await page.QuerySelectorAllAsync("tr.row2");
            var list1 = rows1.ToList();
            var list2 = rows2.ToList();

            // Concatenate the two lists
            var rows = list1.Concat(list2).ToList();

            var companies = new List<Company>();
            foreach (var row in rows)
            {
                var content = await row.EvaluateFunctionAsync<string>("e => e.outerHTML");

                // Parse the HTML content
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(content);

                // Extract the data
                var companyNameNode = htmlDoc.DocumentNode.SelectSingleNode("//a");
                var companyTypeNode = htmlDoc.DocumentNode.SelectSingleNode("//td[3]");
                var locationNode = htmlDoc.DocumentNode.SelectNodes("//td[position() >= 4 and position() <= 6]");

                var companyName = companyNameNode.InnerText.Trim();
                var companyUrl = companyNameNode.GetAttributeValue("href", string.Empty);
                var companyType = companyTypeNode.InnerText.Trim();
                var location = string.Join(", ", locationNode.Select(node => node.InnerText.Trim()));

                var company = new Company
                {
                    Name = companyName,
                    Url = companyUrl,
                    Type = companyType,
                    Location = location
                };
                companies.Add(company);

                Console.WriteLine($"{companyName}, {companyUrl}, {companyType}, {location}");
                companyCount++;
            }
            Console.WriteLine($"{companyCount} companies parsed.");

            return companies;
        }

        protected override string GetKey(Company company)
        {
            return company.Name;
        }
    }
}
