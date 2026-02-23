namespace UnboundDashboard
{
    /// <summary>
    /// Uygulama genelinde kullanılan sabit değerler
    /// </summary>
    public static class AppConstants
    {
        // UI Update Intervals
        public const int MetricsUpdateIntervalSeconds = 2;
        public const int TypewriterCharDelayMs = 90;

        // History and Memory Limits
        public const int MaxQpsHistorySize = 60;
        public const int MaxTerminalLogLines = 60;

        // Terminal Content Ratio
        public const int TerminalFactRatio = 6; // Her 6 satırda bir educational fact

        // Cache Performance Thresholds
        public const double CacheHitLowThreshold = 30.0;
        public const double CacheHitMediumThreshold = 70.0;

        // Response Time Thresholds (milliseconds)
        public const double ResponseTimeFastMs = 30.0;
        public const double ResponseTimeMediumMs = 100.0;

        // System Resource Thresholds
        public const double CpuWarningThreshold = 50.0;
        public const double CpuCriticalThreshold = 75.0;
        public const double RamWarningThreshold = 50.0;
        public const double RamCriticalThreshold = 75.0;

        // SSH Configuration
        public const int SshDefaultPort = 22;
        public const int SshConnectionTimeoutSeconds = 10;
        public const int SshCommandTimeoutSeconds = 10;
        public const int MaxSshReconnectAttempts = 3;

        // External Services
        public const string IpCheckServiceUrl = "https://api.ipify.org";
        public const int IpCheckTimeoutSeconds = 3;

        // Number Formatting
        public const long NumberFormatThousand = 1_000;
        public const long NumberFormatMillion = 1_000_000;

        // Terminal Colors (Hex Codes)
        public const string ColorEmerald = "#10b981";  // Tactical Green CRT
        public const string ColorCyan = "#6b7280";     // Tactical Gray / Muted
        public const string ColorOrange = "#f59e0b";   // Warning Amber
        public const string ColorPurple = "#34d399";   // Bright Green Action
    }
}
