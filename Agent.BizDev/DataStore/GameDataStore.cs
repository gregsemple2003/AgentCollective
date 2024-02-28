using HtmlAgilityPack;
using Agent.Services;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Agent.Core;

namespace Agent.BizDev
{
    public class GameDataStore : SingleFileDataStore<Game>
    {
        private readonly GameSeriesDataStore _seriesDataStore;
        private readonly WebBrowsingService _browsingService;

        public GameDataStore(GameSeriesDataStore seriesDataStore, WebBrowsingService browsingService, string path) : base(path)
        {
            _seriesDataStore = seriesDataStore;
            _browsingService = browsingService;
        }

        public async Task UpdateDetails(Game game)
        {
            var url = $"https://store.steampowered.com/app/{game.SteamAppId}";
            var browseResult = await _browsingService.BrowsePage(url);

            // Wait for the selector to ensure the elements are loaded
            var page = browseResult.Value.Page;
            string pageContent = await page.GetContentAsync();

            // Load html document nodes
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(pageContent); // Load your HTML content here

            // Check for age verification form
            bool isAgeVerificationPresent = await page.QuerySelectorAsync(".agegate_birthday_selector") != null;
            if (isAgeVerificationPresent)
            {
                Console.WriteLine($"Filling out age verification.");

                // Fill out the form
                await page.SelectAsync("select[name='ageDay']", "8");   // Select day
                await page.SelectAsync("select[name='ageMonth']", "September"); // Select month
                await page.SelectAsync("select[name='ageYear']", "1970"); // Select year

                // Submit the form
                await page.ClickAsync("#view_product_page_btn");

                // Wait until requests are finished
                //await page.WaitForSelectorAsync("summary", new WaitForSelectorOptions { Timeout = 30000 });
                await Task.Delay(5000);
                Console.WriteLine($"Done waiting for page load after age verification.");

                // Refresh content
                pageContent = await page.GetContentAsync();
                htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(pageContent); // Load your HTML content here
            }

            // Parse the game header image
            var imgTag = htmlDoc.DocumentNode.SelectSingleNode("//img[@class='game_header_image_full']");
            if (imgTag != null)
            {
                string imageUrl = imgTag.GetAttributeValue("src", string.Empty);
                game.SteamHeaderImageUrl = imageUrl;
            }
            else
            {
                Console.WriteLine("UpdateDetails: Image tag not found.");
            }

            // Parse total review count
            int totalReviewCount = 0;
            var metaTag = htmlDoc.DocumentNode.SelectSingleNode("//meta[@itemprop='reviewCount']");
            if (metaTag != null)
            {
                string contentValue = metaTag.GetAttributeValue("content", string.Empty);
                int.TryParse(contentValue, out totalReviewCount);
            }
            else
            {
                Console.WriteLine("UpdateDetails: Meta tag not found.");
            }

            // Parse recent review count
            int recentReviewCount = 0;
            var reviewDiv = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='appReviewsRecent_Detail']");
            if (reviewDiv != null)
            {
                var reviewSpan = reviewDiv.SelectSingleNode(".//span[contains(@class, 'responsive_reviewdesc_short')]");
                if (reviewSpan != null)
                {
                    // Extract the review count from the text
                    var reviewText = reviewSpan.InnerText;
                    var match = Regex.Match(reviewText, @"\((\d+)% of ([\d,]+)\)");

                    if (match.Success && match.Groups.Count > 2)
                    {
                        var percentage = match.Groups[1].Value; // Percentage
                        var reviewCountStr = match.Groups[2].Value.Replace(",", ""); // Removing commas for parsing
                        if (int.TryParse(reviewCountStr, out var reviewCount))
                        {
                            // Use reviewCount as an integer
                        }
                    }
                    else
                    {
                        Console.WriteLine("UpdateDetails: Review RE match not found.");
                    }
                }
                else
                {
                    Console.WriteLine("UpdateDetails: Review span not found.");
                }
            }
            else
            {
                Console.WriteLine("UpdateDetails: Review div not found.");
            }

            // Add time-series data for review count, etc
            var gameSeries = new GameSeries
            {
                TimeGenerated = DateTime.UtcNow,
                AppId = game.SteamAppId,
                RecentReviewCount = recentReviewCount,
                TotalReviewCount = totalReviewCount
            };
            _seriesDataStore.Add(gameSeries);

            var gameSeriesJson = JsonConvert.SerializeObject(gameSeries);
            Console.WriteLine($"Adding game series '{gameSeriesJson}'");

            // Close the page after the work is done, since the page will otherwise sit in the background consuming CPU
            await page.CloseAsync();
        }

        protected override string GetKey(Game game)
        {
            return game.Name;
        }

        protected override async Task<List<Game>> GetRemote()
        {
            var result = await _browsingService.BrowsePage("https://steamdb.info/tech/Engine/Unreal/");
            if (result.IsFailed)
            {
                Console.WriteLine($"ERROR: GetRemote failed to browse page");
            }

            // Wait for the selector to ensure the elements are loaded
            var page = result.Value.Page;

            // Click "Year" column to sort by year descending (most recent first)
            string headerXPath = "//th[contains(text(), 'Year')]";
            await page.WaitForXPathAsync(headerXPath);
            var headerElement = await page.WaitForXPathAsync(headerXPath);
            await headerElement.ClickAsync();
            await page.WaitForSelectorAsync("tr.app");
            await headerElement.ClickAsync();
            await page.WaitForSelectorAsync("tr.app");

            // Select and iterate over the elements
            var gameCount = 0;

            // Getting the content of the page as a string
            string pageContent = await page.GetContentAsync();

            // Click the selector which shows all apps (up to 1000))
            await page.SelectAsync("select[name='table-apps_length']", "-1");

            var rows = await page.QuerySelectorAllAsync("tr.app");

            var games = new List<Game>();
            foreach (var row in rows)
            {
                var content = await row.EvaluateFunctionAsync<string>("e => e.outerHTML");

                var game = ParseGame(content);
                games.Add(game);

                gameCount++;
            }
            Console.WriteLine($"{gameCount} games parsed.");

            return games;
        }

        private static Game ParseGame(string content)
        {
            Game game = null;
            try
            {
                // Load the HTML content
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(content);

                // Parse and extract the data
                HtmlNode tr = htmlDoc.DocumentNode.SelectSingleNode("//tr[@class='app']");
                string appIdStr = tr.GetAttributeValue("data-appid", string.Empty);
                string gameNameStr = tr.SelectSingleNode(".//td[3]/a").InnerText.Trim();
                var developerNode = tr.SelectSingleNode(".//td[4]/a");
                string developerNameStr = developerNode != null ? developerNode.InnerText.Trim() : string.Empty;
                string yearPublishedStr = tr.SelectSingleNode(".//td[5]").InnerText.Trim();
                string userRatingStr = tr.SelectSingleNode(".//td[6]").InnerText.Trim();
                string followerCountStr = tr.SelectSingleNode(".//td[7]").InnerText.Trim();
                string peakUserCountStr = tr.SelectSingleNode(".//td[8]").InnerText.Trim();

                // Parse the elements, using default values for failures.
                int.TryParse(appIdStr, out var appId);
                int.TryParse(yearPublishedStr, out var yearPublished);
                double.TryParse(userRatingStr.TrimEnd('%'), out var userRating);
                int.TryParse(followerCountStr.Replace(",", ""), out var followerCount);
                int.TryParse(peakUserCountStr.Replace(",", ""), out var peakUserCount);

                // Instantiate a new Game object.
                game = new Game
                {
                    Name = gameNameStr,
                    DeveloperName = developerNameStr,
                    YearPublished = yearPublished,
                    Engine = "Engine.Unreal",
                    UserRating = userRating,
                    FollowerCount = followerCount,
                    PeakUserCount = peakUserCount,
                    SteamAppId = appId
                };

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return game;
        }
    }
}
