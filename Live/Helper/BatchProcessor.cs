using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Live.Helper
{
    public static class BatchProcessor
    {
        public static async Task AppendBatchLinksForMarkets(
             Dictionary<string, List<string>> marketToVariantsMap,
             string crqNumber,
             string gameName,
             string clientName,
             string version)
        {
            string batchMasterPath = Path.Combine(Directory.GetCurrentDirectory(), "Live", "Master_Links", "Batch.txt");
            string batchOutputPath = Path.Combine(Directory.GetCurrentDirectory(), "Links", "Batch", $"{crqNumber}_Batch.txt");

            if (!File.Exists(batchMasterPath))
            {
                Console.WriteLine("❌ Batch master link file not found.");
                return;
            }

            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Links", "Batch"));

            var lines = await File.ReadAllLinesAsync(batchMasterPath);
            var outputLinks = new List<string>();
            string? currentMarket = null;
            string formattedVersion = version.Replace('.', '_');

            outputLinks.Add("🧩 Extracted Details:");
            outputLinks.Add($"Game Name: {gameName}");
            outputLinks.Add($"Client Name: {clientName}");
            outputLinks.Add($"Version: {version}");
            outputLinks.Add("");

            outputLinks.Add("🌍 Market to Variant Map:");
            foreach (var kvp in marketToVariantsMap)
            {
                outputLinks.Add($"{kvp.Key} → {string.Join(", ", kvp.Value)}");
            }
            outputLinks.Add("");
            outputLinks.Add("🔗 Generated Links:");

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.EndsWith(":"))
                {
                    currentMarket = line.TrimEnd(':').Trim();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(currentMarket) || !marketToVariantsMap.ContainsKey(currentMarket))
                    continue;

                if (line.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var variants = marketToVariantsMap[currentMarket];

                    foreach (var variant in variants)
                    {
                        string gameNameWithoutVersion = Regex.Replace(gameName, @"V\d{2}$", "");
                        string gameNameVariant = variant == "V96" ? gameNameWithoutVersion : $"{gameNameWithoutVersion}{variant}";
                        string gameNameVariantDesktop = variant == "V96" ? $"{gameNameWithoutVersion}Desktop" : $"{gameNameWithoutVersion}{variant}Desktop";

                        string updatedLink = line;

                        // Replace game name placeholders (case-insensitive)
                        updatedLink = Regex.Replace(updatedLink, "/gamenameDesktop/", $"/{gameNameVariantDesktop}/", RegexOptions.IgnoreCase);
                        updatedLink = Regex.Replace(updatedLink, "=gamenameDesktop&", $"={gameNameVariantDesktop}&", RegexOptions.IgnoreCase);
                        updatedLink = Regex.Replace(updatedLink, "/gamename/", $"/{gameNameVariant}/", RegexOptions.IgnoreCase);
                        updatedLink = Regex.Replace(updatedLink, "=gamename&", $"={gameNameVariant}&", RegexOptions.IgnoreCase);

                        // Replace GameVersion placeholders dynamically
                        updatedLink = Regex.Replace(updatedLink,
                            @"GameVersion=.*?_engine_0_0_0_0",
                            $"GameVersion={gameName}_{clientName}_{formattedVersion}",
                            RegexOptions.IgnoreCase);

                        // Replace gamepath dynamically
                        updatedLink = Regex.Replace(updatedLink, @"gamepath=/mgs/gamename", $"gamepath=/mgs/{gameName}", RegexOptions.IgnoreCase);

                        // Fix typo if exists
                        updatedLink = Regex.Replace(updatedLink, "testing=tru\\b", "testing=true", RegexOptions.IgnoreCase);

                        outputLinks.Add($"[{currentMarket}] ({variant}) {updatedLink}");
                    }



                    outputLinks.Add("");
                }
            }

            if (outputLinks.Count > 0)
            {
                Console.WriteLine("📁 Writing links to Batch CRQ file...");
                await File.AppendAllLinesAsync(batchOutputPath, outputLinks);
            }
        }

        public static async Task RunBatchAsync(IPage page, string crqNumber, string targetUrl, string username, string password, string loginUrl, bool isLive)
        {
            var loginHelper = new LoginHelper();
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(loginUrl))
            {
                Console.WriteLine("❌ Missing login credentials or login URL.");
                return;
            }
            await loginHelper.LoginInContextAsync(page, loginUrl, username, password);
            Console.WriteLine("Navigating to target URL...");
            await page.GotoAsync(targetUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            Console.WriteLine("Target URL Opened");


            Console.WriteLine("Checking for game buttons...");
            await page.WaitForSelectorAsync("div[aria-label='game-tabs'] button[role='tab']");

            var gameTabs = page.Locator(
                                "div[aria-label='game-tabs'] button[role='tab']:not([cy-data^='payoutVariant-tab-'])"
                            );


            int totalTabs = await gameTabs.CountAsync();
            Console.WriteLine($"Total tab buttons found in DOM: {totalTabs}");

            var gameNames = new List<string>();

            for (int i = 0; i < totalTabs; i++)
            {
                var tab = gameTabs.Nth(i);

                if (!await tab.IsVisibleAsync())
                    continue;

                var text = (await tab.InnerTextAsync()).Trim();

                // ❌ Skip variant-only tabs like V96, V94, V92
                if (Regex.IsMatch(text, @"^V\d+$"))
                    continue;

                if (!string.IsNullOrWhiteSpace(text))
                    gameNames.Add(text);
            }

            Console.WriteLine($"Total REAL games found: {gameNames.Count}");


            for (int i = 0; i < gameNames.Count; i++)
            {
                string gameTitle = gameNames[i];

                await page.GetByRole(AriaRole.Tab, new() { Name = gameTitle }).ClickAsync();

                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                await page.WaitForTimeoutAsync(3000);



                Console.WriteLine("⏳ Waiting 10 seconds before checking variants...");
                await page.WaitForTimeoutAsync(10000);

                try
                {
                    await page.WaitForSelectorAsync("button[cy-data^='payoutVariant-tab-']", new() { Timeout = 10000 });
                }
                catch
                {
                    Console.WriteLine($"⚠️ Variants not found for game {i + 1}. Skipping...");
                    continue;
                }

                await page.WaitForTimeoutAsync(800);

                //string gameTitle = (await currentButton.InnerTextAsync()).Trim();

                Console.WriteLine($"\n🎮 Game {i + 1}: {gameTitle}");

                var variantButtons = await page.QuerySelectorAllAsync("button[cy-data^='payoutVariant-tab-']");
                var variantMarketMap = new Dictionary<string, List<string>>();

                foreach (var button in variantButtons)
                {
                    var cyData = await button.GetAttributeAsync("cy-data");
                    var variant = cyData?.Replace("payoutVariant-tab-", "")?.Trim();
                    if (string.IsNullOrEmpty(variant)) continue;

                    await button.ClickAsync();
                    await page.WaitForTimeoutAsync(1500);

                    var chipSpans = await page.QuerySelectorAllAsync("div[cy-data$='market-chip'] span");
                    var markets = new List<string>();

                    foreach (var chip in chipSpans)
                    {
                        var text = (await chip.InnerTextAsync()).Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            markets.Add(text);
                    }

                    markets = markets.Distinct().ToList();
                    variantMarketMap[variant] = markets;

                    Console.WriteLine($"{variant} → {string.Join(", ", markets)}");
                }

                var allVariants = variantMarketMap.Keys.ToList();
                var marketToVariants = new Dictionary<string, List<string>>();

                foreach (var variant in allVariants)
                {
                    foreach (var market in variantMarketMap[variant])
                    {
                        if (!marketToVariants.ContainsKey(market))
                            marketToVariants[market] = new List<string>();

                        marketToVariants[market].Add(variant);
                    }
                }

                Console.WriteLine("⏳ Waiting 10 seconds before extracting game details...");
                await page.WaitForTimeoutAsync(10000);

                string gameName = "", clientName = "", version = "";

                try
                {
                    // ✅ Get Game + Client from version string element
                    var versionStringElement = await page.WaitForSelectorAsync(
                        "p[cy-data='mobile-web-game-version string']",
                        new() { Timeout = 5000 }
                    );

                    if (versionStringElement != null)
                    {
                        var fullText = (await versionStringElement.InnerTextAsync()).Trim();
                        // Example: beefUpTheBonusPowerCombo_Lume_1_0_0_70

                        var parts = fullText.Split('_');

                        if (parts.Length >= 2)
                        {
                            gameName = parts[0];
                            clientName = parts[1];
                        }
                    }
                    // ✅ Get version separately
                    var versionElement = await page.WaitForSelectorAsync(
                        "p[cy-data='mobile-web-game-content version']",
                        new() { Timeout = 5000 }
                    );

                    if (versionElement != null)
                    {
                        version = (await versionElement.InnerTextAsync()).Trim();
                        // Example: 1_0_0_68
                    }


                }
                catch
                {
                    Console.WriteLine("⚠️ Failed to extract game name or client name.");
                }


                if (string.IsNullOrEmpty(gameName) || string.IsNullOrEmpty(clientName) || string.IsNullOrEmpty(version))
                {
                    Console.WriteLine("⚠️ Missing extracted info. Skipping link generation.");
                    continue;
                }

                Console.WriteLine($"\n🧩 Extracted Details:");
                Console.WriteLine($"Game Name: {gameName}");
                Console.WriteLine($"Client Name: {clientName}");
                Console.WriteLine($"Version: {version}");

                await AppendBatchLinksForMarkets(marketToVariants, crqNumber, gameName, clientName, version);

                if (i == gameNames.Count - 1)
                {
                    Console.WriteLine("✅ Finished processing all games. Exiting loop.");
                    break;
                }

            }

            Console.WriteLine("Automation finished successfully.");
        }
    }
}