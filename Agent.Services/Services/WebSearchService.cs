using HtmlAgilityPack;
using PuppeteerSharp;
using static RocksDbSharp.ColumnFamilies;

namespace Agent.Services
{
    public class SearchResult
    {
        public string LinkName { get; set; }
        public string LinkUrl { get; set; }
        public string LinkDescription { get; set; }
    }

    public class WebSearchService : Service
    {
        private readonly WebBrowsingService _browsingService;

        public WebSearchService(WebBrowsingService browsingService)
        {
            _browsingService = browsingService;
        }

        public async Task<List<SearchResult>> Search(string keyword)
        {
            List<SearchResult> searchResults = new List<SearchResult>();
            string escapedQuery = Uri.EscapeDataString(keyword);
            string searchEngineUrl = $"https://www.google.com/search?q={escapedQuery}";

            var browseResult = await _browsingService.BrowsePage(searchEngineUrl);

            if (browseResult.IsFailed)
            {
                //var html = await page.GetContentAsync();
                var htmlDoc = new HtmlDocument();
                if (htmlDoc.DocumentNode.OuterHtml.Contains("captcha"))
                {
                    // TODO gsemple: shut down browser window, wait for x seconds, retry?
                    Console.WriteLine("");
                }
            }

            if (browseResult.IsSuccess)
            {
                IPage page = browseResult.Value.Page;

                // Assuming search results are in an element identified by '.search-result'
                var results = await page.QuerySelectorAllAsync(".search-result");
                await page.WaitForSelectorAsync("div");
                var html = await page.GetContentAsync();
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // Define the XPath to find the specific parent div.
                var parentDivXPath = $"//div[@data-async-context='query:{escapedQuery}']";
                var parentDivNode = htmlDoc.DocumentNode.SelectSingleNode(parentDivXPath);

                if (parentDivNode != null)
                {
                    // Find all child divs with class "MjjYud" under the specific parent div.
                    var childDivs = parentDivNode.SelectNodes(".//div[contains(@class, 'MjjYud')]");

                    if (childDivs != null)
                    {
                        foreach (var childDiv in childDivs)
                        {
                            // For each child div, find all 'a' tags and print their 'href' attribute.
                            var links = childDiv.SelectNodes(".//a");
                            if (links != null)
                            {
                                foreach (var link in links)
                                {
                                    var href = link.GetAttributeValue("href", string.Empty);
                                    searchResults.Add(new SearchResult { LinkUrl = href });
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No child divs with class 'MjjYud' were found.");
                    }
                }
                else
                {
                    Console.WriteLine("Parent div not found.");
                }

                await page.CloseAsync();
            }

            return searchResults;
        }
    }
}
