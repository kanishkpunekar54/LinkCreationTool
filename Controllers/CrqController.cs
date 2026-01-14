using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Live.Tests;
using System.Reflection.Metadata.Ecma335;

namespace Live.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CrqController : ControllerBase
    {
        [HttpPost("run-gtp")]
        public async Task<IActionResult> RunGtp([FromBody] CrqRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.CrqNumber) || string.IsNullOrWhiteSpace(request.TargetUrl))
                return BadRequest(new { message = "❌ CRQ Number and Target URL are required." });

            try
            {
                // This will wait until the GTP automation completes
                await GTP.RunAsync(request.CrqNumber, request.Mode, request.Variants, request.TargetUrl);

                // Respond after completion
                return Ok(new { message = $"✅ GTP automation completed for CRQ {request.CrqNumber} in {request.Mode} mode." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"❌ Error while running GTP: {ex.Message}" });
            }
        }


        

        // ✅ New API to download generated file
        [HttpGet("download")]
        public IActionResult DownloadFile(string crqNumber, string mode)
        {
            if (string.IsNullOrWhiteSpace(crqNumber) || string.IsNullOrWhiteSpace(mode))
                return BadRequest("❌ CRQ Number and Mode are required.");

            string root = Directory.GetCurrentDirectory();
            string folder;
            string fileName;

            switch (mode.ToLower())
            {
                case "live":
                    folder = Path.Combine(root, "Links", "Live");
                    fileName = $"{crqNumber}_live.txt";
                    break;
                case "pgl":
                    folder = Path.Combine(root, "Links", "PGL");
                    fileName = $"{crqNumber}_pgl.txt";
                    break;
                case "batch":
                    folder = Path.Combine(root, "Links", "Batch");
                    fileName = $"{crqNumber}_Batch.txt";
                    break;
                default:
                    return BadRequest("❌ Invalid mode. Use 'live', 'pgl', or 'batch'.");
            }

            string filePath = Path.Combine(folder, fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound($"❌ File not found for CRQ {crqNumber} in {mode} mode.");

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "text/plain", Path.GetFileName(filePath));
        }


        [HttpGet("results")]
        public IActionResult GetResults(string crqNumber, string mode)
        {
            if (string.IsNullOrWhiteSpace(crqNumber) || string.IsNullOrWhiteSpace(mode))
                return BadRequest(new { message = "Missing CRQ number or mode." });

            string root = Directory.GetCurrentDirectory();
            string folder;
            string fileName;

            switch (mode.ToLower())
            {
                case "live":
                    folder = Path.Combine(root, "Links", "Live");
                    fileName = $"{crqNumber}_live.txt";
                    break;

                case "pgl":
                    folder = Path.Combine(root, "Links", "PGL");
                    fileName = $"{crqNumber}_pgl.txt";
                    break;

                case "batch":
                    folder = Path.Combine(root, "Links", "Batch");
                    fileName = $"{crqNumber}_Batch.txt";
                    break;

                default:
                    return BadRequest(new { message = "Invalid mode. Use 'live', 'pgl', or 'batch'." });
            }

            string filePath = Path.Combine(folder, fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = $"Result file not found for CRQ {crqNumber} in {mode} mode." });

            string content = System.IO.File.ReadAllText(filePath);
            // ✅ Split “Version Validation Summary” section if present
            string normalContent = content;
            string validationSummary = null;

            const string summaryMarker = "===== VERSION VALIDATION SUMMARY =====";
            if (mode.Equals("live", StringComparison.OrdinalIgnoreCase) && content.Contains(summaryMarker))
            {
                int index = content.IndexOf(summaryMarker);
                normalContent = content.Substring(0, index).Trim();
                validationSummary = content.Substring(index).Trim();
            }

            // ✅ Return both parts
            return new JsonResult(new
            {
                message = "✅ File read successfully",
                content = normalContent,
                validationSummary // Will be null for PGL/Batch
            })
            {
                ContentType = "application/json"
            };
        }

    }

    // Request DTO
    public class CrqRequest
    {
        public string CrqNumber { get; set; }
        public string Mode { get; set; }   // "live", "pgl", or "batch"
        public string[] Variants { get; set; }   // optional in batch
        public string TargetUrl { get; set; }
    }
}
