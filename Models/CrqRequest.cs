namespace Live.Models
{
    public class CrqRequest
    {
        // 🔢 CRQ number (required for all modes)
        public string CrqNumber { get; set; }

        // 🎯 GTP URL (artifact/test link)
        public string GtpUrl { get; set; }

        // 👤 Okta login creds
        public string Username { get; set; }
        public string Password { get; set; }

        // 🔐 Okta login page
        public string LoginUrl { get; set; }

        // 🔄 Execution type: "Live", "PGL", or "Batch"
        public string Type { get; set; }

        // 📌 If true → Live, if false → PGL
        // (Batch can still use this flag to decide link formatting)
        public bool IsLive { get; set; }

        // 🧩 Variants (only used for Live/PGL; Batch auto-detects)
        public List<string> Variants { get; set; } = new();
    }
}
