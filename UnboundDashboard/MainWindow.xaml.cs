using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using UnboundDashboard.ViewModels;

namespace UnboundDashboard
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Check if SSH credentials are available
            var config = ReadConfig();

            if (string.IsNullOrEmpty(config.password) && string.IsNullOrEmpty(config.keyPath))
            {
                // Show login dialog
                var dialog = new LoginDialog();
                var result = dialog.ShowDialog();

                if (result == true)
                {
                    // User provided credentials
                    DataContext = new DashboardViewModel(
                        dialog.SshHostname, dialog.SshPort,
                        dialog.SshUsername, dialog.SshPassword, null);
                }
                else
                {
                    // User cancelled — close app
                    Application.Current.Shutdown();
                    return;
                }
            }
            else
            {
                // Config has credentials — use them directly
                DataContext = new DashboardViewModel(
                    config.hostname, config.port,
                    config.username, config.password, config.keyPath);
            }

            Title = "UNBOUND DNS MONITOR";
        }

        private (string hostname, int port, string username, string? password, string? keyPath) ReadConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var hostname = ExtractValue(json, "hostname") ?? "192.168.1.123";
                    var portStr = ExtractValue(json, "port");
                    var port = int.TryParse(portStr, out var p) ? p : 22;
                    var username = ExtractValue(json, "username") ?? "root";
                    var password = ExtractValue(json, "password");
                    var keyPath = ExtractValue(json, "keyPath");
                    return (hostname, port, username, password, keyPath);
                }
            }
            catch { }
            return ("192.168.1.123", 22, "root", null, null);
        }

        private string? ExtractValue(string json, string key)
        {
            var keyPattern = $"\"{key}\"";
            var idx = json.IndexOf(keyPattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var colonIdx = json.IndexOf(':', idx + keyPattern.Length);
            if (colonIdx < 0) return null;
            var afterColon = json.Substring(colonIdx + 1).TrimStart();
            if (afterColon.StartsWith("null", StringComparison.OrdinalIgnoreCase)) return null;
            if (afterColon.StartsWith("\""))
            {
                var endQuote = afterColon.IndexOf('"', 1);
                return endQuote > 0 ? afterColon.Substring(1, endQuote - 1) : null;
            }
            var end = afterColon.IndexOfAny(new[] { ',', '}', '\n', '\r' });
            return end > 0 ? afterColon.Substring(0, end).Trim() : afterColon.Trim();
        }



        private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void OnClose(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is DashboardViewModel vm)
                vm.Dispose();
            base.OnClosing(e);
        }
    }
}
