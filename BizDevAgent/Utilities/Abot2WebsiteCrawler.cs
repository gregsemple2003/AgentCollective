using Abot2;
using Abot2.Core;
using Abot2.Poco;
using Abot2.Crawler;
using System;
using System.Threading.Tasks;

public class WebsiteCrawler
{
    public async Task Start(Uri uriToCrawl)
    {
        // Create crawl configuration
        var config = new CrawlConfiguration
        {
            MaxPagesToCrawl = 10, // Set the number of pages to crawl
            MinCrawlDelayPerDomainMilliSeconds = 3000 // Set delay between requests
        };

        // Create the crawler
        var crawler = new PoliteWebCrawler(config);

        // Subscribe to crawler events
        crawler.PageCrawlStarting += OnPageCrawlStartingAsync;
        crawler.PageCrawlCompleted += OnPageCrawlCompletedAsync;
        crawler.PageCrawlDisallowed += OnPageCrawlDisallowedAsync;
        crawler.PageLinksCrawlDisallowed += OnPageLinksCrawlDisallowedAsync;

        // Start the crawl
        try
        {
            var crawlResult = await crawler.CrawlAsync(uriToCrawl);

            // Check if the crawl completed without errors
            if (crawlResult.ErrorOccurred)
                Console.WriteLine($"Crawl of {uriToCrawl} completed with error: {crawlResult.ErrorException.Message}");
            else
                Console.WriteLine($"Crawl of {uriToCrawl} completed without error.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"*** WEB CRAWLER EXCEPTION ***: {ex.Message}");
        }
    }

    private void OnPageCrawlStartingAsync(object sender, PageCrawlStartingArgs e)
    {
        var pageToCrawl = e.PageToCrawl;
        Console.WriteLine($"About to crawl page {pageToCrawl.Uri.AbsoluteUri}");
    }

    private void OnPageCrawlCompletedAsync(object sender, PageCrawlCompletedArgs e)
    {
        var crawledPage = e.CrawledPage;

        if (crawledPage.HttpResponseMessage != null)
            Console.WriteLine($"Crawled page {crawledPage.Uri.AbsoluteUri} completed with status code {crawledPage.HttpResponseMessage.StatusCode}");

        // Here you can process the page content
        // crawledPage.Content.Text
    }

    private void OnPageCrawlDisallowedAsync(object sender, PageCrawlDisallowedArgs e)
    {
        var pageToCrawl = e.PageToCrawl;
        Console.WriteLine($"Crawl disallowed for page {pageToCrawl.Uri.AbsoluteUri}");
    }

    private void OnPageLinksCrawlDisallowedAsync(object sender, PageLinksCrawlDisallowedArgs e)
    {
        var crawledPage = e.CrawledPage;
        Console.WriteLine($"Link crawl disallowed for page {crawledPage.Uri.AbsoluteUri}");
    }
}