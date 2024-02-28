using HtmlAgilityPack;
using PuppeteerSharp;
using RocksDbSharp;
using System.Text.RegularExpressions;

public class PuppeteerWebsiteCrawler
{
    /// <summary>
    /// The delay in milliseconds between each page fetch.  Use to throttle the rate at which the crawler fetches pages.
    /// </summary>
    public static readonly TimeSpan PageDownloadDelay = TimeSpan.FromMilliseconds(1000);

    public List<string> ExtractedEmails { get; private set; }

    private readonly List<string> _visitedUrls = new List<string>();
    private readonly List<string> _priorityKeywords;
    private readonly HashSet<string> _excludedExtensions;
    private readonly int _maxPagesToVisit;
    private readonly string _tag;
    private IBrowser _browser;
    private int _pagesVisited = 0;
    private string _rootDomain;
    private string _rootScheme; // Store the scheme (http/https)

    private static RocksDb _db;

    public PuppeteerWebsiteCrawler(List<string> priorityKeywords, string tag, RocksDb db, int maxPagesToVisit = 200)
    {
        _maxPagesToVisit = maxPagesToVisit;
        _priorityKeywords = priorityKeywords;
        _tag = tag;
        _db = db;
        _excludedExtensions = new HashSet<string>
        {
            ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".bmp", ".svg",
            ".mp3", ".mp4", ".avi", ".mov", ".wmv", ".flv",
            ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
            ".zip"
        };

        ExtractedEmails = new List<string>();
    }

    public async Task Stop()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
    }

    public async Task Start(string startUrl)
    {
        try
        {
            var uri = new Uri(startUrl);
            _rootDomain = NormalizeDomain(uri.Host);
            _rootScheme = uri.Scheme;

            await new BrowserFetcher().DownloadAsync();
            var options = new LaunchOptions { Headless = true };
            _browser = await Puppeteer.LaunchAsync(options);

            await VisitPageAsync(_browser, startUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_tag}] *** WEB CRAWLER EXCEPTION ***: {ex.Message}");

            throw ex;
        }
        finally
        {
            await Stop();
        }
    }

    private bool ShouldVisit(string url)
    {
        // Exclude empty URLs
        if (string.IsNullOrWhiteSpace(url)) 
            return false;

        // Check page visit count or if we've already visited this url
        if (_pagesVisited >= _maxPagesToVisit || _visitedUrls.Contains(url))
            return false;

        // Exclude URLs from outside the root domain
        try
        {
            var currentDomain = NormalizeDomain(new Uri(url).Host);
            if (currentDomain != _rootDomain)
                return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_tag}] ShouldVisit caught exception due to invalid url {ex.Message}");

            return false;
        }

        // Exclude non-HTML file extensions
        if (_excludedExtensions.Any(ext => url.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private string ResolveAbsoluteUrl(string url, string baseUrl)
    {
        try
        {
            // If the url is already absolute, return it as is
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                return url;

            // If the url is relative, create an absolute URL using the base URL and scheme
            if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
            {
                baseUrl = $"{_rootScheme}://{baseUrl}";
            }

            if (Uri.TryCreate(new Uri(baseUrl), url, out var absoluteUri))
                return absoluteUri.ToString();

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{_tag}] *** WEB CRAWLER EXCEPTION ***: {ex.Message}");

            throw ex;
        }
    }

    private async Task VisitPageAsync(IBrowser browser, string url)
    {
        url = ResolveAbsoluteUrl(url, _rootDomain);

        if (!ShouldVisit(url))
            return;

        // Check if the page is cached
        string pageContent = _db.Get(url);
        if (pageContent == null)
        {
            // Delay to throttle page loads to avoid being IP banned
            await Task.Delay((int)PageDownloadDelay.TotalMilliseconds);

            var page = await browser.NewPageAsync();
            try
            {
                await page.GoToAsync(url);
                Console.WriteLine($"[{_tag}] Visiting remotely: {url}");
            }
            catch (PuppeteerSharp.NavigationException ex)
            {
                // Store an empty string for navigation exceptions, this can happen on links that point to pdfs, images, etc
                // We want to avoid an HTTP request every time we encounter this data
                Console.WriteLine($"[{_tag}] Navigation to {url} aborted: {ex.Message}");
                pageContent = string.Empty;
            }

            // Get the content of the page
            pageContent = page != null ? await page.GetContentAsync() : string.Empty;

            // Cache the page content
            _db.Put(url, pageContent);

            await page.CloseAsync();
        }
        else
        {
            Console.WriteLine($"[{_tag}] Visiting locally: {url}");
        }

        _visitedUrls.Add(url);
        _pagesVisited++;

        // Extract emails from page content 
        var emails = ExtractEmailsFromContent(pageContent);
        foreach (var email in emails)
        {
            if (!ExtractedEmails.Contains(email))
            {
                ExtractedEmails.Add(email);

                Console.WriteLine($"[{_tag}] Extracted email '{email}'");
            }
        }

        // Extract and prioritize links from the page content
        var links = ExtractLinksFromContent(pageContent);
        var prioritizedLinks = PrioritizeLinks(links);
        foreach (var link in prioritizedLinks)
        {
            await VisitPageAsync(browser, link);
        }

        // Content has mailtos but didn't extract any, error
        if (pageContent.Contains("mailto") && emails.ToList().Count == 0)
        {
            Console.WriteLine($"[{_tag}] Detected mailto without extracting a valid email");
        }
    }

    private IEnumerable<string> ExtractEmailsFromContent(string pageContent)
    {
        var emailPattern = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}";

        var matches = Regex.Matches(pageContent, emailPattern);
        foreach (Match match in matches)
        {
            yield return match.Value;
        }
    }

    private IEnumerable<string> ExtractLinksFromContent(string pageContent)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(pageContent);

        var links = doc.DocumentNode
                       .SelectNodes("//a[@href]")
                       ?.Select(node => node.Attributes["href"].Value)
                       ?? Enumerable.Empty<string>();

        return links;
    }

    private string NormalizeDomain(string domain)
    {
        return domain.StartsWith("www.") ? domain.Substring(4) : domain;
    }

    private IEnumerable<string> PrioritizeLinks(IEnumerable<string> links)
    {
        return links
            .Select(link => new { Link = link, Priority = CalculateLinkPriority(link, _priorityKeywords) })
            .OrderByDescending(item => item.Priority)
            .Select(item => item.Link);
    }

    private int CalculateLinkPriority(string link, List<string> priorityKeywords)
    {
        return priorityKeywords.Count(keyword => link.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
