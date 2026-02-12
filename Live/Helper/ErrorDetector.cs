
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

                if (msg.Type == "error")
                {
                    bool isIgnored = _ignoredErrors.Any(ignore => msg.Text.Contains(ignore, StringComparison.OrdinalIgnoreCase));

                    bool urlIgnored = !string.IsNullOrEmpty(msg.Location) &&
                                      _ignoredErrors.Any(ignore => msg.Location.Contains(ignore, StringComparison.OrdinalIgnoreCase));

                    bool statusIgnored = _ignoredStatusCodes.Any(code => msg.Text.Contains(code.ToString()));

                    if (!isIgnored && !urlIgnored && !statusIgnored)
                    {
                        lock (_errors) { _errors.Add($"[JS Error] {msg.Text}"); }
                    }
                }
            };

            page.Response += (_, response) =>
            {
                bool isIgnoredUrl = _ignoredErrors.Any(ignore =>
                    response.Url.Contains(ignore, StringComparison.OrdinalIgnoreCase));

                if (!isIgnoredUrl && response.Status >= 400 && !_ignoredStatusCodes.Contains(response.Status))
                {
                    lock (_errors) { _errors.Add($"[Network Error] {response.Status} on {response.Url}"); }
                }
            };

            page.RequestFailed += (_, request) =>
            {
                if (_isPaused) return;

                string failureText = request.Failure ?? "Unknown";

                bool isIgnored = _ignoredErrors.Any(ignore => failureText.Contains(ignore)) || _ignoredErrors.Any(ignore => request.Url.Contains(ignore));

                if (!isIgnored)
                {
                    lock (_errors) { _errors.Add($"[Request Failed] {failureText} on {request.Url}"); }
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
    }

}