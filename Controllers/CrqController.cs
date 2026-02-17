using Live.Api.Services;
using Live.Api.Services;
using Live.Tests;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Live.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("AllowAll")]
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

        // SSE endpoint streams console logs (line-by-line).
        [HttpGet("getSSE")]
        public async Task GetSSE()
        {
            HttpContext.Response.Headers.Add("Cache-Control", "no-cache");
            HttpContext.Response.Headers.Add("X-Accel-Buffering", "no"); // disable buffering for nginx-like proxies
            HttpContext.Response.ContentType = "text/event-stream";

            var token = HttpContext.RequestAborted;
            var subscriber = ConsoleBroadcaster.Subscribe(sendHistory: true);

            // helper: determine log level from text using simple heuristics (emoji/keywords).
            static string DetermineLevel(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return "info";
                var lower = text.ToLowerInvariant();
                if (lower.Contains("❌") || lower.Contains("error") || lower.Contains("failed")) return "error";
                if (lower.Contains("⚠") || lower.Contains("warning") || lower.Contains("warn")) return "warning";
                if (lower.Contains("✅") || lower.Contains("success") || lower.Contains("finished")) return "success";
                return "info";
            }

            // Format a LogEntry to SSE (single data: <json>\n\n)
            static string ToSseJson(string text)
            {
                var entry = new
                {
                    id = Guid.NewGuid().ToString("D"),
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    level = DetermineLevel(text),
                    message = text ?? string.Empty
                };
                string json = JsonSerializer.Serialize(entry);
                // SSE line: single data: <json>\n\n
                return $"data: {json}\n\n";
            }

            try
            {
                await foreach (var line in subscriber.Reader.ReadAllAsync(token))
                {
                    // Each broadcasted "line" may contain embedded newlines; preserve them in message.
                    var payload = ToSseJson(line);
                    await HttpContext.Response.WriteAsync(payload, token);
                    await HttpContext.Response.Body.FlushAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // client disconnected (expected)
            }
            finally
            {
                ConsoleBroadcaster.Unsubscribe(subscriber);
            }
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
            if (mode.Equals("live", System.StringComparison.OrdinalIgnoreCase) && content.Contains(summaryMarker))
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
