using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CrqAutomationApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly string _envFile = Path.Combine(Directory.GetCurrentDirectory(), ".env");

        [HttpPost]
        public async Task<IActionResult> SaveCredentials([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Username and password are required");

            try
            {
                var envLines = new Dictionary<string, string>();

                // Load existing .env if present
                if (System.IO.File.Exists(_envFile))
                {
                    foreach (var line in await System.IO.File.ReadAllLinesAsync(_envFile))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                            envLines[parts[0]] = parts[1];
                    }
                }

                // Update username and password
                envLines["OKTA_USERNAME"] = request.Username;
                envLines["OKTA_PASSWORD"] = request.Password;

                // Ensure GTP_LINK always exists
                if (!envLines.ContainsKey("GTP_LINK"))
                    envLines["GTP_LINK"] = "https://www.gametechnology.io/games";

                // Write back to .env
                var envContent = string.Join("\n", envLines.Select(kv => $"{kv.Key}={kv.Value}"));
                await System.IO.File.WriteAllTextAsync(_envFile, envContent);

                return Ok(new { Message = "✅ Credentials saved successfully in .env" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to save credentials: {ex.Message}");
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
