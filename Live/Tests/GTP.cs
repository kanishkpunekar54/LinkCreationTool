using DotNetEnv;
using Microsoft.Playwright;
using Live.Helper;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Live.Tests
{
    public class GTP
    {
        private readonly Dictionary<string, List<string>> _marketToVariantsMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _variantToVersion = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> VariantToVersion => _variantToVersion;

        private static readonly Dictionary<string, string> MarketAliasMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "BAC", "Argentina - Buenos Aires City" },
            { "BAP", "Argentina - Buenos Aires Province" },
            { "Cordoba", "Argentina - Cordoba Province" },
            { "Buenos Aires City", "Argentina - Buenos Aires City" },
            { "Buenos Aires Province", "Argentina - Buenos Aires Province" },
            
        };
        private static readonly string[] ItsCasinoNames =
        {
            "Zodiac",
            "zodiac_desktop",
            "Luxury_uk",
            "Phoenician",
            "CaptainCooksCasino_CAON",
            "CaptainCooksCasino_CAON_Desktop",
            "luxury",
            "Phoenician_desktop"


        };

        public Dictionary<string, List<string>> MarketToVariantsMap => _marketToVariantsMap;

        public async Task<IPage> SelectGameSystemAndParseMarketsAsync(IPage page, string variant, string gameSystem)
        {
            Console.WriteLine($"🔧 Switching to Game System: {gameSystem}");
            var rgsSwitcher = page.Locator("div[cy-data='rgs-switcher-controls']");
            if (await rgsSwitcher.CountAsync() > 0 && await rgsSwitcher.First.IsVisibleAsync())
            {
                await rgsSwitcher.First.ClickAsync();
                var systemOptions = page.Locator("ul[role='listbox'] > li[cy-data='switcher-menu-item']");
                int systemCount = await systemOptions.CountAsync();

                for (int i = 0; i < systemCount; i++)
                {
                    var option = systemOptions.Nth(i);
                    string text = (await option.InnerTextAsync()).Trim();

                    if (string.Equals(text, gameSystem, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"✅ Found and clicked game system: {text}");
                        await option.ClickAsync();
                        break;
                    }
                }
            }

            await Task.Delay(2000);

            var selectors = new[]
            {
                $"li[cy-data='variant-select-V{variant}-selected-row']",
                $"li[cy-data='variant-select-V{variant}-row']"
                     };

            bool variantFound = false;

            foreach (var selector in selectors)
            {
                var switcherDivs = page.Locator("div[cy-data='switcher-controls']");
                int count = await switcherDivs.CountAsync();

                for (int i = 0; i < count; i++)
                {
                    var currentDiv = switcherDivs.Nth(i);
                    var variantLi = currentDiv.Locator(selector);

                    if (await variantLi.CountAsync() > 0 && await variantLi.First.IsVisibleAsync())
                    {
                        variantFound = true;

                        if (!selector.Contains("selected"))
                        {
                            Console.WriteLine($"🔀 Selecting variant V{variant}");
                            await variantLi.First.ClickAsync();
                        }

                        await Task.Delay(1500);

                        var marketsPara = page.Locator("text=Associated markets").Locator("xpath=following-sibling::p[1]");

                        if (await marketsPara.CountAsync() > 0)
                        {
                            string text = await marketsPara.First.InnerTextAsync();
                            Console.WriteLine($"🔍 Found markets in V{variant}: {text}");
                            var presentMarkets = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                            foreach (var market in presentMarkets)
                            {
                                if (!_marketToVariantsMap.ContainsKey(market))
                                    _marketToVariantsMap[market] = new List<string>();

                                if (!_marketToVariantsMap[market].Contains($"V{variant}"))
                                    _marketToVariantsMap[market].Add($"V{variant}");
                            }
                        }
                        
                            string? version = await HelpfileHelper.GetHelpfileVersionForVariantAsync(page, $"V{variant}");
                            if (version != null)
                            {
                                _variantToVersion[$"V{variant}"] = version;
                            }
                       


                        return page;
                    }
                }
            }

            if (!variantFound)
            {
                Console.WriteLine($"⚠️ Variant V{variant} not found on the page. Will fallback later if needed.");

                // ✅ Attempt to read associated markets even if variant tab is missing
                var marketsPara = page.Locator("text=Associated markets").Locator("xpath=following-sibling::p[1]");

                if (await marketsPara.CountAsync() > 0)
                {
                    string text = await marketsPara.First.InnerTextAsync();
                    Console.WriteLine($"🔍 Found markets in fallback for V{variant}: {text}");
                    var presentMarkets = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var market in presentMarkets)
                    {
                        if (!_marketToVariantsMap.ContainsKey(market))
                            _marketToVariantsMap[market] = new List<string>();

                        if (!_marketToVariantsMap[market].Contains($"V{variant}"))
                            _marketToVariantsMap[market].Add($"V{variant}");
                    }
                }
            }

            return page;
        }
        public static string NormalizeGameNameWithVariant(string extractedGameName, string variant)
        {
            // Remove any trailing V followed by 2 digits (e.g., V97, V96, etc.)
            string cleanedGameName = Regex.Replace(extractedGameName, @"V\d{2}$", "", RegexOptions.IgnoreCase);
            if (variant.Equals("V96", StringComparison.OrdinalIgnoreCase))
                return cleanedGameName;
            // Add the desired variant
            return $"{cleanedGameName}{variant}";
        }
        private static string GetHighestVariant(List<string> variants)
        {
            return variants
                .Where(v => Regex.IsMatch(v, @"^V\d{2}$", RegexOptions.IgnoreCase))
                .Select(v => new
                {
                    Original = v,
                    Number = int.Parse(Regex.Match(v, @"\d{2}").Value)
                })
                .OrderByDescending(v => v.Number)
                .FirstOrDefault()?.Original;
        }


        public static async Task AppendNormalCrqLinksForMarkets(
            Dictionary<string, List<string>> marketToVariantsMap,
            string crqNumber,
            string gameName,
            string clientName,
            string version,
            bool isLive)
        {
            string normalCrqPath = Path.Combine(Directory.GetCurrentDirectory(), "Live", "Master_Links", "PGL.txt");
            string outputType = isLive ? "Live" : "PGL";

            // ✅ Put into subfolder
            string linksFolder = Path.Combine(Directory.GetCurrentDirectory(), "Links", outputType);
            Directory.CreateDirectory(linksFolder);

            string targetFile = Path.Combine(linksFolder, $"{crqNumber}_{outputType.ToLower()}.txt");

            if (!File.Exists(normalCrqPath))
            {
                Console.WriteLine("❌ Master link file not found.");
                return;
            }

            var lines = await File.ReadAllLinesAsync(normalCrqPath);
            var outputLinks = new List<string>();
            string currentMarket = null;
            string formattedVersion = version.Replace('.', '_');

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.EndsWith(":"))
                {
                    currentMarket = line.TrimEnd(':').Trim();
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentMarket) && line.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    if (!marketToVariantsMap.TryGetValue(currentMarket, out var variants) || variants.Count == 0)
                        continue;
                    bool isItsCasino = ItsCasinoNames
                        .Any(name => line.Contains(name, StringComparison.OrdinalIgnoreCase));
                    // Decide which variants to use
                    List<string> variantsToUse;
                    if (isItsCasino)
                    {
                        var highestVariant = GetHighestVariant(variants);
                        if (highestVariant == null)
                            continue;

                        variantsToUse = new List<string> { highestVariant };
                    }
                    else
                    {
                        variantsToUse = variants;
                    }


                    foreach (var variant in variantsToUse)
                    {
                        string gameNameVariant = NormalizeGameNameWithVariant(gameName, variant);
                        string gameNameVariantDesktop = gameNameVariant + "Desktop";

                        string updatedLink = line
                            .Replace("/gamenameDesktop/", $"/{gameNameVariantDesktop}/", StringComparison.OrdinalIgnoreCase)
                            .Replace("=gamenameDesktop&", $"={gameNameVariantDesktop}&", StringComparison.OrdinalIgnoreCase)
                            .Replace("/gamename/", $"/{gameNameVariant}/", StringComparison.OrdinalIgnoreCase)
                            .Replace("=gamename&", $"={gameNameVariant}&", StringComparison.OrdinalIgnoreCase);

                        if (!isLive)
                        {
                            updatedLink = updatedLink
                                .Replace("GameVersion=gamenameDesktop_engine_0_0_0_0", $"GameVersion={gameName}_{clientName}_{formattedVersion}", StringComparison.OrdinalIgnoreCase)
                                .Replace("GameVersion=gamename_engine_0_0_0_0", $"GameVersion={gameName}_{clientName}_{formattedVersion}", StringComparison.OrdinalIgnoreCase)
                                .Replace("gamepath=/mgs/gamename", $"gamepath=/mgs/{gameName}", StringComparison.OrdinalIgnoreCase);
                        }
                        else
                        {
                            updatedLink = Regex.Replace(updatedLink, @"[&?]GameVersion=[^&]+", "", RegexOptions.IgnoreCase);
                            updatedLink = Regex.Replace(updatedLink, @"[&?]gamepath=[^&]+", "", RegexOptions.IgnoreCase);
                            updatedLink = updatedLink.TrimEnd('&', '?');
                        }

                        if (isItsCasino)
                            outputLinks.Add($"[{currentMarket}] ({variant}) (ITS) {updatedLink}");
                        else
                            outputLinks.Add($"[{currentMarket}] ({variant}) {updatedLink}");
                    }

                    outputLinks.Add("");
                }
            }

            if (outputLinks.Count > 0)
            {
                Console.WriteLine("📂 Writing links to CRQ file...");
                await File.AppendAllLinesAsync(targetFile, new[] { "\nNormal CRQ Market Links:" });
                await File.AppendAllLinesAsync(targetFile, outputLinks);
            }
        }

        public static async Task RunAsync(string crqNumber, string mode, string[] variants, string targetUrl)
        {
            Env.Load();

            bool isLive = mode.Equals("live", StringComparison.OrdinalIgnoreCase);
            string outputType = mode.Equals("batch", StringComparison.OrdinalIgnoreCase) ? "Batch" : (isLive ? "Live" : "PGL");

            string? username = Environment.GetEnvironmentVariable("OKTA_USERNAME");
            string? password = Environment.GetEnvironmentVariable("OKTA_PASSWORD");
            string? loginUrl = Environment.GetEnvironmentVariable("GTP_LINK");

            if (string.IsNullOrWhiteSpace(crqNumber) || string.IsNullOrWhiteSpace(targetUrl))
            {
                Console.WriteLine("❌ Missing CRQ number or Target URL.");
                return;
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false
            });

            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            if (mode.Equals("batch", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(username)
                    || string.IsNullOrWhiteSpace(password)
                    || string.IsNullOrWhiteSpace(loginUrl))
                {
                    Console.WriteLine("❌ Missing OKTA credentials or login URL.");
                    return;
                }

                await BatchProcessor.RunBatchAsync(page, crqNumber, targetUrl, username, password, loginUrl, isLive);
                return;
            }
            var loginHelper = new LoginHelper();
            Console.WriteLine("🔐 Logging into GTP...");
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(loginUrl))
            {
                Console.WriteLine("❌ Missing OKTA credentials or login URL.");
                return;
            }
            if (string.IsNullOrWhiteSpace(username) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(loginUrl))
            {
                Console.WriteLine("❌ Missing OKTA credentials or login URL.");
                return;
            }
            await loginHelper.LoginInContextAsync(page, loginUrl, username!, password!);


            var newPage = page;
            Console.WriteLine($"🌐 Navigating to {targetUrl}...");
            await newPage.GotoAsync(targetUrl);

            Console.WriteLine("📝 Clicking 'View signed off artifacts'...");
            var artifactButton = await newPage.WaitForSelectorAsync("button:has(span:text('View Signed Off Artifacts'))");
            if (artifactButton != null) await artifactButton.ClickAsync();

            await Task.Delay(2000);

            var gtp = new GTP();
            foreach (var variant in variants)
                await gtp.SelectGameSystemAndParseMarketsAsync(newPage, variant.Replace("V", ""), "Veyron");

            var devDeployLink = await newPage.QuerySelectorAsync("a[href*='https://devdeploy.mgsops.net/app#/projects/System-Client-']");
            if (devDeployLink == null)
            {
                Console.WriteLine("❌ Dev Deploy link not found.");
                return;
            }

            var devDeployUrl = await devDeployLink.GetAttributeAsync("href");

            if (string.IsNullOrWhiteSpace(devDeployUrl))
            {
                Console.WriteLine("❌ Dev Deploy URL is null or empty.");
                return;
            }

            Console.WriteLine($"✅ Found Dev Deploy URL: {devDeployUrl}");

            var match = Regex.Match(
                devDeployUrl,
                @"System-Client-(?<client>[\w]+)-(?<game>[\w\d\-]+)/deployments/releases/(?<version>[\d\.\-rc]+)"
            );
            if (!match.Success)
            {
                Console.WriteLine("❌ Failed to extract client/game/version from DevDeploy URL.");
                return;
            }

            string clientName = match.Groups["client"].Value;
            string gameNameRaw = match.Groups["game"].Value;
            string releaseVersionRaw = match.Groups["version"].Value;

            string gameName = gameNameRaw.Replace("-", "");
            string releaseVersion = Regex.Match(releaseVersionRaw, @"^\d+(\.\d+)*").Value;

            Console.WriteLine($"📦 Extracted Client: {clientName}, Game: {gameName}, Version: {releaseVersion}");

            // ✅ Ensure correct subfolder
            string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "Links", outputType);
            Directory.CreateDirectory(folderPath);

            string notesFile = Path.Combine(folderPath, $"{crqNumber}_{outputType.ToLower()}.txt");

            using (var writer = new StreamWriter(notesFile))
            {
                await writer.WriteLineAsync($"CRQ Number: {crqNumber}");
                await writer.WriteLineAsync($"Target URL: {targetUrl}");
                await writer.WriteLineAsync($"Game Name: {gameName}");
                await writer.WriteLineAsync($"Client Name: {clientName}");
                await writer.WriteLineAsync($"Release Version: {releaseVersion}");
                await writer.WriteLineAsync($"Variant Selected: {string.Join(", ", variants)}");
                await writer.WriteLineAsync("\nMarket to Variant Map:");

                foreach (var kvp in gtp.MarketToVariantsMap)
                    await writer.WriteLineAsync($"{kvp.Key} -> {string.Join(", ", kvp.Value)}");

                if (gtp.VariantToVersion.Count > 0)
                {
                    await writer.WriteLineAsync("Helpfile Versions from GTP :");
                    foreach (var kvp in gtp.VariantToVersion)
                    {
                        await writer.WriteLineAsync($"{kvp.Key} -> {kvp.Value}");
                    }
                }
            }

            await page.CloseAsync();
            await browser.CloseAsync();
            await AppendNormalCrqLinksForMarkets(gtp.MarketToVariantsMap, crqNumber, gameName, clientName, releaseVersion, isLive);

            if (isLive)
            {
                string crqFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Links", "Live", $"{crqNumber}_live.txt");
                
                await LiveLinkVersionValidator.ValidateLiveLinksAndAppendSummaryAsync(crqFilePath, releaseVersion);
            }
        }
    }
}

