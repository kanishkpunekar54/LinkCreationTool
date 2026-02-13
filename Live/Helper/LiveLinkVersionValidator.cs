using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace Live.Helper
{
    public static class LiveLinkVersionValidator
    {
        private static readonly ErrorDetector _errorDetector = new ErrorDetector();
        const int InitialTimeoutMs = 30000;
        const int ReloadTimeoutMs = 30000;
        static bool loadSuccess = false;
        public static async Task ValidateLiveLinksAndAppendSummaryAsync(string crqFilePath, string expectedVersion)
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

            // Limit concurrency
            int maxParallel = 1;
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
                            Channel = "chrome",
                            Headless = true
                        });

                    var context = await browser.NewContextAsync();
                    string failureScreenshotPath = null;
                    string passedScreenshotPath = null;
                    foreach (var link in links)
                    {
                        IPage page = await context.NewPageAsync();
                        _errorDetector.StartMonitoring(page);
                        try
                        {
                            var initialLoadTask = WaitForEvent103(page, InitialTimeoutMs);

                            try
                            {
                                Console.WriteLine($"\n🔗 Opening link from file: {link}");
                                await page.GotoAsync(link, new PageGotoOptions
                                {
                                    WaitUntil = WaitUntilState.DOMContentLoaded
                                });

                                page.SetDefaultTimeout(60000);
                                await page.WaitForLoadStateAsync(LoadState.Load);

                                await page.WaitForSelectorAsync("canvas", new PageWaitForSelectorOptions { Timeout = InitialTimeoutMs });

                            }
                            catch (PlaywrightException pex) when (pex.Message.Contains("interrupted") || pex.Message.Contains("net::ERR_ABORTED"))
                            {
                                Console.WriteLine("Navigation interrupted (likely redirect). Continuing wait...");
                            }
                            try
                            {
                                var initialPacket = await initialLoadTask.WithTimeout(InitialTimeoutMs);

                                if (_errorDetector.HasErrors())
                                {
                                    var errors = _errorDetector.GetErrors();
                                    LogFailure("Game Loaded (Event 103 Received) with the following errors:", errors);
                                }

                                Console.WriteLine("Initial Launch Successful (Visual Load + Event 103).");

                                loadSuccess = true;
                                string filename = "Test_Live";
                                passedScreenshotPath = $"{marketFolder}/Pass_{filename}_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.png";
                                await page.ScreenshotAsync(new PageScreenshotOptions { Path = passedScreenshotPath });
                            }
                            catch (TimeoutException)
                            {
                                Console.WriteLine("Initial load timed out. Possible slow network detected. Triggering reload...");

                                _errorDetector.Clear();
                                // pass explicit Playwright timeout for the retry wait
                                var reloadTask = WaitForEvent103(page, ReloadTimeoutMs);

                                _errorDetector.Pause();
                                var retryReload = page.ReloadAsync();
                                await Task.Delay(500);
                                _errorDetector.Resume();
                                await retryReload;

                                await page.WaitForLoadStateAsync(LoadState.Load);

                                await page.WaitForSelectorAsync("canvas", new PageWaitForSelectorOptions { Timeout = InitialTimeoutMs });

                                try
                                {
                                    var reloadPacket = await reloadTask.WithTimeout(ReloadTimeoutMs);

                                    if (_errorDetector.HasErrors())
                                    {
                                        var errors = _errorDetector.GetErrors();
                                        LogFailure("Game re-loaded successfully. Errors detected during RETRY RELOAD", errors);
                                    }

                                    Console.WriteLine("Game launched successfully after reload (slow network recovered).");
                                    loadSuccess = true;
                                }
                                catch (TimeoutException)
                                {
                                    loadSuccess = false;
                                    Console.WriteLine("Game failed to launch even after reload (Taking too long to launch). Network too slow or game issue.");
                                    throw;
                                }
                            }
                        }
                        catch (TimeoutException)
                        {
                            string filename = "Test_Live_1";
                            failureScreenshotPath = $"{marketFolder}/FAIL_{filename}_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.png";
                            await page.ScreenshotAsync(new PageScreenshotOptions { Path = failureScreenshotPath });
                            loadSuccess = false;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            failedLinks.Add(link);
                            Console.WriteLine($"   ❌ Error loading link: (Unexpected error during game launch (Taking too long to launch) ) : {ex.Message}");

                            loadSuccess = false;

                            if (failureScreenshotPath == null)
                            {
                                string filename = "Test_Live_2";
                                failureScreenshotPath = $"{marketFolder}/FAIL_{filename}_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.png";
                                try
                                {
                                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = failureScreenshotPath });
                                }
                                catch (Exception exception)
                                {
                                    Console.WriteLine($"Failed to capture screenshot of the failure: {exception.Message}");
                                }
                            }
                        }
                        finally
                        {
                            if (page != null)
                                await page.CloseAsync();

                            if (loadSuccess)
                            {
                                Console.WriteLine("Game Launches Successfully");
                            }
                            else
                            {
                                Console.WriteLine($"Game didn't launch successfully even after reload (Taking too long to launch). Check screenshot: {failureScreenshotPath}");
                            }

                            await context.CloseAsync();
                            Console.WriteLine($"----- End of process -----");
                        }

                        try
                        {
                            if (loadSuccess)
                            {
                                int linkIndex = passed + failed + 1;

                                string safeFileName =
                                    Regex.Replace(link, @"[^\w]+", "_");

                                string finalUrl = page.Url ?? string.Empty;
                                string actualVersion = "N/A";

                                var matchVer = Regex.Match(finalUrl, @"gameversion=[^=]+?_(?<ver>\d+(_\d+)+)", RegexOptions.IgnoreCase);

                                if (matchVer.Success)
                                    actualVersion = matchVer.Groups["ver"].Value;

                                if (actualVersion.Equals(expectedVersion, StringComparison.OrdinalIgnoreCase))
                                {
                                    passed++;
                                }
                                else
                                {
                                    failed++;
                                    failedLinks.Add(link);
                                }
                            }
                            else
                            {
                                failed++;
                                failedLinks.Add(link);
                            }

                            _errorDetector.GetErrors().ForEach(err => Console.WriteLine($"[Console Errors]: {err}"));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error during version validation or screenshot capture: {ex.Message}");
                            failed++;
                            failedLinks.Add(link);
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
        public static Task<IRequest> WaitForEvent103(IPage page, int timeoutMs)
        {
            return page.WaitForRequestAsync(request =>
            {
                if (!request.Url.Contains("h5events/messages")) return false;
                string? body = request.PostData;
                if (string.IsNullOrEmpty(body)) return false;

                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(body))
                    {
                        if (doc.RootElement.TryGetProperty("EventInfo", out var eventInfo) &&
                            eventInfo.TryGetProperty("EventType", out var typeVal) &&
                            typeVal.GetInt32() == 103)
                        {
                            return true;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Failed to parse request body JSON.");
                }
                return false;
            }, new PageWaitForRequestOptions { Timeout = timeoutMs });
        }

        private static void LogFailure(string contextMessage, List<string> errors)
        {
            Console.WriteLine($"FAILURE: {contextMessage}");
            foreach (var err in errors)
            {
                Console.WriteLine($"  - {err}");
            }
        }
    }
}
