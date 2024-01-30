using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentResults;
using System.Threading.Tasks;

namespace BizDevAgent.Utilities
{
    public static class PuppeteerUtils
    {
        public class ResponseError : Error
        {
            public IResponse ErrorResponse { get; }

            public ResponseError(IResponse response)
            {
                ErrorResponse = response;
            }
        }

        public struct BrowsePageResult
        {
            public IPage Page { get; set; }
            public IBrowser Browser { get; set; }
            public IResponse Response { get; set; }
        }

        public static async Task<Result<BrowsePageResult>> BrowsePage(string url, IBrowser browserOverride = null)
        {
            var result = new BrowsePageResult();

            try
            {
                // Initialize PuppeteerSharp
                if (browserOverride is not null)
                {
                    result.Browser = browserOverride;
                }
                else
                {
                    await new BrowserFetcher().DownloadAsync();
                    result.Browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = false // Set false if you want to see the browser
                    });
                }

                // Navigate to the Webpage
                IPage page = await result.Browser.NewPageAsync();
                result.Page = page;

                // Intercept the request to delete bot headers
                await page.SetRequestInterceptionAsync(true);
                page.Request += async (sender, e) =>
                {
                    var existingHeaders = new Dictionary<string, string>(e.Request.Headers);

                    // Define custom headers which will override the existing headers
                    var customHeaders = new Dictionary<string, string>
                    {
                        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
                        ["Accept-Encoding"] = "gzip, deflate, br",
                        ["Accept-Language"] = "en-US,en;q=0.9",
                        ["Cache-Control"] = "max-age=0",
                        //["Referer"] = "https://steamdb.info/app/1250/info/",
                        ["Referer"] = "https://steamdb.info/app/253230?__cf_chl_tk=XtZgXWG648ZAH8yVPQzjnI8_ZQj2MUYCnjIth4qKiRI-1706292781-0-gaNycGzNEZA",
                        ["Sec-Ch-Ua"] = "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"",
                        ["Sec-Ch-Ua-Arch"] = "\"x86\"",
                        ["Sec-Ch-Ua-Bitness"] = "\"64\"",
                        ["Sec-Ch-Ua-Full-Version"] = "\"120.0.6099.225\"",
                        ["Sec-Ch-Ua-Full-Version-List"] = "\"Not_A Brand\";v=\"8.0.0.0\", \"Chromium\";v=\"120.0.6099.225\", \"Google Chrome\";v=\"120.0.6099.225\"",
                        ["Sec-Ch-Ua-Mobile"] = "?0",
                        ["Sec-Ch-Ua-Model"] = "\"\"",
                        ["Sec-Ch-Ua-Platform"] = "\"Windows\"",
                        ["Sec-Ch-Ua-Platform-Version"] = "\"10.0.0\"",
                        ["Sec-Fetch-Dest"] = "document",
                        ["Sec-Fetch-Mode"] = "navigate",
                        ["Sec-Fetch-Site"] = "same-origin",
                        ["Sec-Fetch-User"] = "?1",
                        ["Upgrade-Insecure-Requests"] = "1",
                        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                    };

                    // Apply custom headers, overriding any existing ones
                    foreach (var header in customHeaders)
                    {
                        existingHeaders[header.Key] = header.Value;
                    }

                    var payload = new Payload { Headers = existingHeaders };
                    await e.Request.ContinueAsync(payload);
                };

                result.Response = await page.GoToAsync(url);
            }
            catch (Exception ex)
            {
                return Result.Fail(new ExceptionalError(ex));
            }

            if (!result.Response.Ok)
            {
                return Result.Fail(new ResponseError(result.Response));
            }

            return result;
        }
    }
}
