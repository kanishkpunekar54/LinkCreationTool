using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace Live.Helper
{
    public static class LiveLinkVersionValidator
    {
        public static async Task ValidateLiveLinksAndAppendSummaryAsync(
            string crqFilePath,
            string expectedVersion)
        {
            Console.WriteLine("\n🚀 Starting Live Link Version Validation...");
            if (!File.Exists(crqFilePath))
            {
                Console.WriteLine("❌ CRQ file not found for validation.");
                return;
            }

            // Screenshot root
            string screenshotsRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Screenshots",
                Path.GetFileNameWithoutExtension(crqFilePath)
            );
            Directory.CreateDirectory(screenshotsRoot);

            // Read and parse file
            var lines = await File.ReadAllLinesAsync(crqFilePath);
            string currentMarket = null;
            var marketLinks = new Dictionary<string, List<string>>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Match [Market] (V94) pattern
                var headerMatch = Regex.Match(
                    line ?? string.Empty,
                    @"\[(?<market>[^\]]+)\]\s*(?:\((?<variant>V\d+)\))?",
                    RegexOptions.IgnoreCase);

                var linkMatch = Regex.Match(
                    line ?? string.Empty,
                    @"(http[s]?:\/\/[^\s]+)",
                    RegexOptions.IgnoreCase);

                if (headerMatch.Success)
                {
                    currentMarket = headerMatch.Groups["market"].Value.Trim();
                    if (!marketLinks.ContainsKey(currentMarket))
                        marketLinks[currentMarket] = new List<string>();
                }

                if (linkMatch.Success && currentMarket != null)
                {
                    string url = linkMatch.Value.Trim();
                    marketLinks[currentMarket].Add(url);
                }
            }

            if (marketLinks.Count == 0)
            {
                Console.WriteLine("⚠️ No market links found in CRQ file.");
                return;
            }

            // Normalize version once
            string normalizedExpected = expectedVersion.Replace('_', '.').Trim();

            // Limit concurrency
            int maxParallel = 4;
            using var semaphore = new SemaphoreSlim(maxParallel);

            var results = new List<string>();

            using var playwright = await Playwright.CreateAsync();

            var tasks = marketLinks.Select(async kvp =>
            {
                await semaphore.WaitAsync();
                try
                {
                    string market = kvp.Key;
                    string marketFolder = Path.Combine(screenshotsRoot, market);
                    Directory.CreateDirectory(marketFolder);

                    var links = kvp.Value;
                    int total = links.Count;
                    int passed = 0;
                    int failed = 0;
                    var failedLinks = new List<string>();

                    Console.WriteLine($"\n🌍 Checking market: {market} ({total} links)");

                    await using var browser = await playwright.Chromium.LaunchAsync(
                        new BrowserTypeLaunchOptions
                        {
                            Headless = true
                        });

                    var context = await browser.NewContextAsync();
                    int counter = 0;
                    foreach (var link in links)
                    {
                        IPage? page = null;
                        try
                        {
                            page = await context.NewPageAsync();

                            Console.WriteLine($"\n🔗 Opening link from file: {link}");

                            page.SetDefaultNavigationTimeout(120000);
                            page.SetDefaultTimeout(120000);
                            await page.GotoAsync(link, new PageGotoOptions
                            {
                                Timeout = 120000,
                                WaitUntil = WaitUntilState.DOMContentLoaded
                            });

                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                            {
                                Timeout = 120000
                            });
                            
                            await page.WaitForTimeoutAsync(3000);

                            int linkIndex = passed + failed + 1;

                            string screenshotPath = Path.Combine(
                                marketFolder,
                                $"{counter+=1}.png");

                            await page.ScreenshotAsync(
                                new PageScreenshotOptions
                                {
                                    Path = screenshotPath,
                                    FullPage = true
                                });

                            string finalUrl = page.Url ?? string.Empty;
                            string actualVersion = "N/A";

                            var matchVer = Regex.Match(
                                finalUrl,
                                @"gameversion=[^=]+?_(?<ver>\d+(_\d+)+)",
                                RegexOptions.IgnoreCase);

                            if (matchVer.Success)
                                actualVersion = matchVer.Groups["ver"].Value;

                            string normalizedActual =
                                actualVersion.Replace('_', '.').Trim();

                            if (normalizedActual.Equals(
                                normalizedExpected,
                                StringComparison.OrdinalIgnoreCase))
                            {
                                passed++;
                            }
                            else
                            {
                                failed++;
                                failedLinks.Add(link);
                            }
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            failedLinks.Add(link);
                            Console.WriteLine($"   ❌ Error loading link: {ex.Message}");
                        }
                        finally
                        {
                            if (page != null)
                                await page.CloseAsync(); // CLOSE TAB
                        }
                    }

                    string marketStatus = failed == 0 ? "PASSED" : "FAILED";
                    string emoji = failed == 0 ? "✅" : "❌";

                    var marketSummary = new List<string>
                    {
                        $"{emoji} {market}: {total} links | Passed: {passed} | Failed: {failed} -> {marketStatus}"
                    };

                    foreach (var badLink in failedLinks)
                        marketSummary.Add($"   ❌ Failed link -> {badLink}");

                    marketSummary.Add(new string('-', 80));

                    lock (results)
                        results.AddRange(marketSummary);

                    await browser.CloseAsync();
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Append summary to file
            await File.AppendAllLinesAsync(
                crqFilePath,
                new[] { "\n===== VERSION VALIDATION SUMMARY =====" });

            await File.AppendAllLinesAsync(crqFilePath, results);

            Console.WriteLine(
                $"\n📄 Summary appended to {Path.GetFileName(crqFilePath)}");
        }
    }
}
