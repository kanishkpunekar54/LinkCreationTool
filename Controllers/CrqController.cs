using Live.Tests;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text;
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

        // C#
        [HttpGet("getSSE")]
        public async Task GetSSE()
        {
            SseBroadcasterService.EnsureConsoleHooked();

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");

            var stream = Response.Body; // or Response.BodyWriter.AsStream()
            var writer = new StreamWriter(stream);
            var clientId = SseBroadcasterService.AddClient(writer);

            try
            {
                await SseBroadcasterService.BroadcastAsync($"Client connected: {clientId}");
                try { await Task.Delay(Timeout.Infinite, HttpContext.RequestAborted); }
                catch (TaskCanceledException) { }
            }
            finally
            {
                SseBroadcasterService.RemoveClient(clientId);
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
    public static class SseBroadcasterService
    {
        private static readonly ConcurrentDictionary<Guid, StreamWriter> _clients = new();
        private static readonly object _consoleHookLock = new();
        private static bool _consoleHooked = false;

        public static Guid AddClient(StreamWriter writer)
        {
            var id = Guid.NewGuid();
            _clients.TryAdd(id, writer);
            return id;
        }

        public static void RemoveClient(Guid id)
        {
            _clients.TryRemove(id, out _);
        }

        public static async Task BroadcastAsync(string message)
        {
            foreach (var client in _clients.Values)
            {
                try
                {
                    await client.WriteAsync($"data: {message}\n\n");
                    await client.FlushAsync();
                }
                catch
                {
                    // Ignore exceptions for disconnected clients
                }
            }
        }

        // Called by the console writer to forward console text to SSE clients
        internal static void BroadcastFromConsole(string message)
        {
            // Fire-and-forget; broadcasting shouldn't block console output
            _ = BroadcastAsync(message);
        }

        // Ensure Console.Out is replaced with a writer that forwards to SSE
        public static void EnsureConsoleHooked()
        {
            if (_consoleHooked) return;

            lock (_consoleHookLock)
            {
                if (_consoleHooked) return;

                var original = Console.Out;
                var hook = new ConsoleBroadcastWriter(original);
                Console.SetOut(hook);
                _consoleHooked = true;
            }
        }
    }
    // Writer that forwards console output to the original console plus SSE broadcaster.
    // Writer that forwards console output to the original console plus SSE broadcaster.
    internal class ConsoleBroadcastWriter : TextWriter
    {
        private readonly TextWriter _original;
        private readonly StringBuilder _buffer = new();

        public ConsoleBroadcastWriter(TextWriter original)
        {
            _original = original ?? throw new ArgumentNullException(nameof(original));
        }

        public override Encoding Encoding => _original.Encoding;

        public override void Write(char value)
        {
            _original.Write(value);

            // Buffer until newline to send complete lines
            _buffer.Append(value);
            if (value == '\n')
            {
                var line = _buffer.ToString().TrimEnd('\r', '\n');
                _buffer.Clear();

                if (!string.IsNullOrEmpty(line))
                {
                    try
                    {
                        SseBroadcasterService.BroadcastFromConsole(line);
                    }
                    catch
                    {
                        // Swallow exceptions to avoid breaking the console stream
                    }
                }
            }
        }

        public override void Write(string value)
        {
            if (value == null)
            {
                return;
            }

            _original.Write(value);

            // If value contains newlines, split and broadcast per line
            int start = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                {
                    _buffer.Append(value.Substring(start, i - start + 1));
                    var line = _buffer.ToString().TrimEnd('\r', '\n');
                    _buffer.Clear();
                    start = i + 1;

                    if (!string.IsNullOrEmpty(line))
                    {
                        try
                        {
                            SseBroadcasterService.BroadcastFromConsole(line);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }

            if (start < value.Length)
            {
                _buffer.Append(value.Substring(start));
            }
        }

        public override void WriteLine(string value)
        {
            _original.WriteLine(value);
            var line = value ?? string.Empty;
            try
            {
                SseBroadcasterService.BroadcastFromConsole(line);
            }
            catch
            {
                // ignore
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _original.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
