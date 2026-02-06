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
            await page.GotoAsync(loginUrl, new PageGotoOptions { Timeout = 60000 });

            Console.WriteLine("👉 Please click GamesGlobalOkta manually...");

            // ⏳ Wait until user clicks GGL Okta and Okta page loads
            await page.WaitForURLAsync(
                url => url.Contains("/login") || url.Contains("/oauth2"),
                new() { Timeout = 120000 }
            );

            Console.WriteLine("✅ GamesGlobalOkta clicked, continuing login...");
            await page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(username);
            await page.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
            await page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Select to enter a code from" }).ClickAsync();
            await Task.Delay(15000);
            await page.GetByRole(AriaRole.Button, new() { Name = "Verify" }).ClickAsync();
            await Task.Delay(5000);

            var ggoBtn = page.GetByRole(AriaRole.Button, new() { Name = "GamesGlobalOkta" });
            if (await ggoBtn.IsVisibleAsync())
                await ggoBtn.ClickAsync();

            await page.WaitForURLAsync("**/games**", new PageWaitForURLOptions { Timeout = 5000 });

            Console.WriteLine("Logged in Sucessfully");
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
