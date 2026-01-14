using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Live.Helper
{
    public static class InputHelper
    {
        public static Dictionary<string, List<string>> GetMarketVariantsFromUser()
        {
            Console.WriteLine("Enter market and variants (e.g., Belgium V94, Spain V94,V98):");
            Console.WriteLine("Enter an empty line to finish input:");

            var marketVariantMap = new Dictionary<string, List<string>>();

            while (true)
            {
                string? line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) break;

                var parts = line.Split(' ', 2);
                if (parts.Length < 2) continue;

                var country = parts[0].Trim();
                var variants = parts[1]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                if (!marketVariantMap.ContainsKey(country))
                    marketVariantMap[country] = new List<string>();

                marketVariantMap[country].AddRange(variants);
            }

            return marketVariantMap;
        }

        public static string GetGtpLinkFromUser()
        {
            Console.Write("Enter GTP link (or leave empty to use .env GTP_LINK): ");
            string? inputGtp = Console.ReadLine()?.Trim();

            return string.IsNullOrWhiteSpace(inputGtp)
                ? Environment.GetEnvironmentVariable("GTP_LINK") ?? ""
                : inputGtp;
        }

        public static void PrintMarketVariantMap(Dictionary<string, List<string>> map)
        {
            Console.WriteLine("\n✅ Parsed Market Variants:");
            foreach (var entry in map)
            {
                Console.WriteLine($"- {entry.Key}: {string.Join(", ", entry.Value)}");
            }
        }
    }
}
