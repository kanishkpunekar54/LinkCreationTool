// File: Live/Helper/LoginHelper.cs

using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Live.Helper
{
    public class LoginHelper
    {
        public async Task LoginInContextAsync(IPage page, string loginUrl, string username, string password)
        {
            try
            {
                await page.GotoAsync(loginUrl, new PageGotoOptions { Timeout = 60000 });

                //Console.WriteLine("👉 Please click GamesGlobalOkta manually...");

                //// ⏳ Wait until user clicks GGL Okta and Okta page loads
                //await page.WaitForURLAsync(
                //    url => url.Contains("/login") || url.Contains("/oauth2"),
                //    new() { Timeout = 120000 }
                //);

                // 🔎 Locate GamesGlobalOkta button
                var gglOktaBtn = page.Locator("input[aria-label='GamesGlobalOkta']:visible");

                // ⏳ Wait until visible
                await gglOktaBtn.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 30000
                });

                // 👆 Click automatically
                await gglOktaBtn.ClickAsync();

                Console.WriteLine("✅ GamesGlobalOkta clicked, continuing login...");
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(username);
                await page.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
                await page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(password);
                await page.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
                await page.GetByRole(AriaRole.Link, new() { Name = "Select to get a push notification to the Okta Verify app." }).ClickAsync();
                await page.WaitForNavigationAsync();
                await page.WaitForURLAsync("**/games**", new PageWaitForURLOptions { Timeout = 5000 });

                Console.WriteLine("✅ Logged in Sucessfully");
            }
            catch(TimeoutException te)
            {
                try
                {
                    Console.WriteLine("⚠ Retrying once again due to failure!");
                    await page.WaitForNavigationAsync();
                    await page.WaitForURLAsync("**/games**", new PageWaitForURLOptions { Timeout = 5000 });
                    Console.WriteLine("✅ Logged in Sucessfully");
                }
                catch(Exception e)
                {
                    throw;
                }
            }
            catch(PlaywrightException ex)
            {
                Console.WriteLine("❌ Exception Occured : ", ex.Message);
            }
            catch(Exception e)
            {
                Console.WriteLine("❌ Exception Occured : ", e.Message);
            }
        }

        public async Task<(IBrowserContext context, IPage page)> LoginAndSaveState(
            IBrowser browser, string loginUrl, string storageStatePath, string username, string password)
        {
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await LoginInContextAsync(page, loginUrl, username, password);

            await context.StorageStateAsync(new BrowserContextStorageStateOptions
            {
                Path = storageStatePath
            });

            if (!File.Exists(storageStatePath))
                throw new Exception("Storage state file not found. Please run the login test first.");

            await Task.Delay(2000);
            return (context, page);
        }
    }
}
