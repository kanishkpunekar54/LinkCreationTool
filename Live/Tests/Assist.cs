using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Live.Tests
{
    public class Assist
    {
        public static async Task RunAsync()
        {
            // Credentials
            string basicAuthUsername = "shubhamt";
            string basicAuthPassword = "Password1234$";
            string siteUsername = "shubhamt";
            string sitePassword = "Password1234$";
            string crqNumber = "CRQ1793027";

            using var playwright = await Playwright.CreateAsync();

            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = basicAuthUsername,
                    Password = basicAuthPassword
                }
            });

            var page = await context.NewPageAsync();

            try
            {
                // Step 1: Go directly to CRQ form URL (will redirect to login if not authenticated)
                string crqFormUrl = "http://assist.mgsops.net/arsys/forms/remedyapp/CHG:Infrastructure%20Change/Derivco/";
                await page.GotoAsync(crqFormUrl);
                Console.WriteLine($"Navigated to: {page.Url}");

                Console.WriteLine("Login form detected. Logging in...");
                await page.FillAsync("#user_login", siteUsername);
                await page.FillAsync("#login_user_password", sitePassword);

                await page.ClickAsync("#login-jsp-btn");
                await page.WaitForNavigationAsync(); // wait for login redirect
                Console.WriteLine("New URL after login: " + page.Url);


                // Handle new tab (popup) if it opens after login
                var popupTask = page.WaitForPopupAsync();

                await page.ClickAsync("#login-jsp-btn");

                // Wait for popup
                var popupPage = await popupTask;
                await popupPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

                Console.WriteLine("Login successful. New page opened: " + popupPage.Url);

                // Close the old login tab
                await page.CloseAsync();

                // Work on the new page from now on
                page = popupPage;



                //// Step 3: Search for CRQ
                //Console.WriteLine($"Searching for CRQ: {crqNumber}");
                //await page.FillAsync("input[aria-label='Request ID']", crqNumber);
                //await page.Keyboard.PressAsync("Enter");

                //// Step 4: Wait for result
                //await page.WaitForSelectorAsync($"text={crqNumber}", new PageWaitForSelectorOptions { Timeout = 30000 });

                //var resultText = await page.InnerTextAsync($"text={crqNumber}");
                //Console.WriteLine($"Found CRQ Details: {resultText}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during login or CRQ search: " + ex.Message);
            }
            finally
            {
                //await browser.CloseAsync();

                await Task.Delay(50000);
            }
        }
    }
}
