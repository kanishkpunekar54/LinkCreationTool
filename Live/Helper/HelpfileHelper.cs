using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace Live.Helper
{
    public static class HelpfileHelper
    {
        /// <summary>
        /// Extracts the helfile version for the currently selected variant on the page.
        /// </summary>
        /// <param name="page">The Playwright page instance.</param>
        /// <param name="variant">The variant being checked (e.g., "V97").</param>
        /// <returns>The helfile version as a string, or null if not found.</returns>
        public static async Task<string?> GetHelpfileVersionForVariantAsync(IPage page, string variant)
        {
            var versionLocator = page.Locator("p:has-text('Version') + p span");

            if (await versionLocator.CountAsync() > 0)
            {
                string versionText = (await versionLocator.First.InnerTextAsync()).Trim();
                Console.WriteLine($"🔍 Helpfile version for {variant}: {versionText}");
                return versionText;
            }

            Console.WriteLine($"⚠️ Helpfile version not found for {variant}");
            return null;
        }
    }
}
