
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Live.Helper
{
    public class ErrorDetector
    {
        private readonly List<string> _errors = new List<string>();
        private bool _isMonitoring = false;
        private bool _isPaused = false;
        private readonly string[] _ignoredErrors = new[]
        {
        "Application Insights",
        "visualstudio.com",
        "favicon",
        "favicon.ico",
    };
        private readonly int[] _ignoredStatusCodes = new[] { 401 };

        public void StartMonitoring(IPage page)
        {
            if (_isMonitoring) return;

            page.Console += (_, msg) =>
            {
                if (_isPaused) return;

                try
                {
                    if (msg.Type == "error")
                    {
                        bool isIgnored = _ignoredErrors.Any(ignore => msg.Text.Contains(ignore, StringComparison.OrdinalIgnoreCase));

                        bool urlIgnored = !string.IsNullOrEmpty(msg.Location) &&
                                          _ignoredErrors.Any(ignore => msg.Location.Contains(ignore, StringComparison.OrdinalIgnoreCase));

                        bool statusIgnored = _ignoredStatusCodes.Any(code => msg.Text.Contains(code.ToString()));

                        if (!isIgnored && !urlIgnored && !statusIgnored)
                        {
                            // Try to extract structured error details from the console text (if present)
                            if (TryExtractErrorDetailsFromJson(msg.Text, out var application, out var errorCode, out var message))
                            {
                                var parts = new List<string>();
                                if (!string.IsNullOrEmpty(application)) parts.Add($"App:{application}");
                                if (errorCode.HasValue) parts.Add($"Code:{errorCode.Value}");
                                if (!string.IsNullOrEmpty(message)) parts.Add($"Message:{message}");

                                var detail = parts.Count > 0 ? $"[{string.Join(" | ", parts)}]" : string.Empty;
                                lock (_errors) { _errors.Add($"[JS Error] {detail} Raw: {msg.Text}"); }
                            }
                            else
                            {
                                lock (_errors) { _errors.Add($"[JS Error] {msg.Text}"); }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ErrorDetector][ConsoleHandler] exception: {ex.Message}");
                }
            };

            page.Response += (_, response) =>
            {
                try
                {
                    bool isIgnoredUrl = _ignoredErrors.Any(ignore =>
                        response.Url.Contains(ignore, StringComparison.OrdinalIgnoreCase));

                    // Treat non-2xx as problem if not explicitly ignored.
                    if (!isIgnoredUrl && response.Status >= 400 && !_ignoredStatusCodes.Contains(response.Status))
                    {
                        // Read response body asynchronously so we don't block the event
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                string body = string.Empty;
                                try
                                {
                                    body = await response.TextAsync();
                                }
                                catch
                                {
                                    // ignore failures reading body
                                }

                                if (!string.IsNullOrEmpty(body) && TryExtractErrorDetailsFromJson(body, out var application, out var errorCode, out var message))
                                {
                                    var parts = new List<string> { $"Status:{response.Status}" };
                                    if (!string.IsNullOrEmpty(application)) parts.Add($"App:{application}");
                                    if (errorCode.HasValue) parts.Add($"Code:{errorCode.Value}");
                                    if (!string.IsNullOrEmpty(message)) parts.Add($"Message:{message}");

                                    var detail = string.Join(" | ", parts);
                                    lock (_errors) { _errors.Add($"[Network Error] {detail} on {response.Url}"); }
                                }
                                else
                                {
                                    lock (_errors) { _errors.Add($"[Network Error] {response.Status} on {response.Url}"); }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ErrorDetector][ResponseHandler Task] exception: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ErrorDetector][ResponseHandler] exception: {ex.Message}");
                }
            };

            page.RequestFailed += (_, request) =>
            {
                if (_isPaused) return;

                try
                {
                    string failureText = request.Failure ?? "Unknown";

                    bool isIgnored = _ignoredErrors.Any(ignore => failureText.Contains(ignore, StringComparison.OrdinalIgnoreCase))
                                     || _ignoredErrors.Any(ignore => request.Url.Contains(ignore, StringComparison.OrdinalIgnoreCase));

                    if (!isIgnored)
                    {
                        // Try to parse post data for structured error details
                        if (!string.IsNullOrEmpty(request.PostData) && TryExtractErrorDetailsFromJson(request.PostData, out var application, out var errorCode, out var message))
                        {
                            var parts = new List<string>();
                            if (!string.IsNullOrEmpty(application)) parts.Add($"App:{application}");
                            if (errorCode.HasValue) parts.Add($"Code:{errorCode.Value}");
                            if (!string.IsNullOrEmpty(message)) parts.Add($"Message:{message}");

                            var detail = parts.Count > 0 ? $"[{string.Join(" | ", parts)}]" : string.Empty;
                            lock (_errors) { _errors.Add($"[Request Failed] {failureText} on {request.Url} {detail}"); }
                        }
                        else
                        {
                            lock (_errors) { _errors.Add($"[Request Failed] {failureText} on {request.Url}"); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ErrorDetector][RequestFailed] exception: {ex.Message}");
                }
            };

            _isMonitoring = true;
        }
        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;
        public bool HasErrors() => _errors.Count > 0;

        public List<string> GetErrors()
        {
            lock (_errors) { return new List<string>(_errors); }
        }

        public void Clear()
        {
            lock (_errors) { _errors.Clear(); }
        }

        // Attempts to extract application, code and message from JSON or JS object-like text.
        // Returns true if any meaningful field was found.
        private static bool TryExtractErrorDetailsFromJson(string text, out string application, out int? code, out string message)
        {
            application = string.Empty;
            code = null;
            message = string.Empty;

            if (string.IsNullOrWhiteSpace(text)) return false;

            // try to find JSON-like substring between first { and last }
            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return false;

            string candidate = text.Substring(start, end - start + 1).Trim();

            // first attempt: direct parse
            if (TryParseJsonDocument(candidate, out var doc) && doc != null)
            {
                ExtractFromJsonElement(doc.RootElement, ref application, ref code, ref message);
                return !(string.IsNullOrEmpty(application) && !code.HasValue && string.IsNullOrEmpty(message));
            }

            // Try lightweight normalization for JS object literal -> valid JSON:
            // - replace single quotes with double quotes
            // - quote unquoted property names
            string normalized = candidate.Replace('\'', '"');

            try
            {
                // Quote unquoted property names: e.g. {application: "x"} -> {"application": "x"}
                normalized = Regex.Replace(normalized, @"(?<=[{\s,])([A-Za-z0-9_]+)\s*:", @"""$1"":", RegexOptions.Compiled);
            }
            catch
            {
                // ignore regex failures and try parsing anyway
            }

            if (TryParseJsonDocument(normalized, out var doc2) && doc2 != null)
            {
                ExtractFromJsonElement(doc2.RootElement, ref application, ref code, ref message);
                return !(string.IsNullOrEmpty(application) && !code.HasValue && string.IsNullOrEmpty(message));
            }

            return false;
        }

        private static bool TryParseJsonDocument(string json, out JsonDocument? doc)
        {
            doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                doc?.Dispose();
                doc = null;
                return false;
            }
        }

        private static void ExtractFromJsonElement(JsonElement element, ref string application, ref int? code, ref string message)
        {
            try
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("application", out var appProp) && appProp.ValueKind == JsonValueKind.String)
                    {
                        application = appProp.GetString() ?? string.Empty;
                    }

                    if (element.TryGetProperty("code", out var codeProp))
                    {
                        if (codeProp.ValueKind == JsonValueKind.Number && codeProp.TryGetInt32(out var codeVal))
                        {
                            code = codeVal;
                        }
                        else if (codeProp.ValueKind == JsonValueKind.String && int.TryParse(codeProp.GetString(), out var codeVal2))
                        {
                            code = codeVal2;
                        }
                    }

                    if (element.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
                    {
                        message = msgProp.GetString() ?? string.Empty;
                    }
                    else if (element.TryGetProperty("displayMessage", out var displayProp) && displayProp.ValueKind == JsonValueKind.String)
                    {
                        message = displayProp.GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // swallow parsing exceptions - extraction is best-effort
            }
        }
    }
}