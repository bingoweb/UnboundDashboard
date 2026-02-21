using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Renci.SshNet;

namespace UnboundDashboard
{
    public partial class LoginDialog : Window
    {
        // Results — read by caller after ShowDialog
        public string SshHostname { get; private set; } = "";
        public int SshPort { get; private set; } = 22;
        public string SshUsername { get; private set; } = "";
        public string SshPassword { get; private set; } = "";
        public bool SaveRequested { get; private set; }

        public LoginDialog()
        {
            InitializeComponent();
            LoadExistingConfig();
        }

        private void LoadExistingConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    TxtHostname.Text = ExtractValue(json, "hostname") ?? "192.168.1.123";
                    TxtPort.Text = ExtractValue(json, "port") ?? "22";
                    TxtUsername.Text = ExtractValue(json, "username") ?? "root";
                    var password = ExtractValue(json, "password");
                    if (!string.IsNullOrEmpty(password))
                        TxtPassword.Password = password;
                }
                else
                {
                    TxtHostname.Text = "192.168.1.123";
                    TxtPort.Text = "22";
                    TxtUsername.Text = "root";
                }
            }
            catch
            {
                TxtHostname.Text = "192.168.1.123";
                TxtPort.Text = "22";
                TxtUsername.Text = "root";
            }
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void OnTestConnection(object sender, RoutedEventArgs e)
        {
            ShowStatus("Bağlantı test ediliyor...", "#22d3ee", "#0c1a2e", "#162d4a");

            var hostname = TxtHostname.Text.Trim();
            var portStr = TxtPort.Text.Trim();
            var username = TxtUsername.Text.Trim();
            var password = TxtPassword.Password;

            if (string.IsNullOrEmpty(hostname) || string.IsNullOrEmpty(username))
            {
                ShowStatus("⚠ Sunucu adresi ve kullanıcı adı gerekli.", "#fbbf24", "#1a1708", "#3d2e0a");
                return;
            }

            if (!int.TryParse(portStr, out var port)) port = 22;

            var result = await Task.Run(() => TestSshConnection(hostname, port, username, password));

            if (result.success)
                ShowStatus("✓ Bağlantı başarılı! Sunucuya erişim sağlandı.", "#34d399", "#0a2118", "#1a3d2e");
            else
                ShowStatus($"✕ Bağlantı başarısız: {result.error}", "#fb7185", "#2a0f15", "#4a1520");
        }

        private (bool success, string? error) TestSshConnection(string hostname, int port, string username, string password)
        {
            try
            {
                var connInfo = new ConnectionInfo(hostname, port, username,
                    new PasswordAuthenticationMethod(username, password));
                connInfo.Timeout = TimeSpan.FromSeconds(8);

                using var client = new SshClient(connInfo);
                client.Connect();
                var connected = client.IsConnected;
                client.Disconnect();
                return (connected, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private void OnSaveConnect(object sender, RoutedEventArgs e)
        {
            var hostname = TxtHostname.Text.Trim();
            var username = TxtUsername.Text.Trim();
            var password = TxtPassword.Password;

            if (string.IsNullOrEmpty(hostname) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowStatus("⚠ Tüm alanları doldurun.", "#fbbf24", "#1a1708", "#3d2e0a");
                return;
            }

            if (!int.TryParse(TxtPort.Text.Trim(), out var port)) port = 22;

            SshHostname = hostname;
            SshPort = port;
            SshUsername = username;
            SshPassword = password;
            SaveRequested = ChkRemember.IsChecked == true;

            // Save to config if requested
            if (SaveRequested)
            {
                SaveConfig(hostname, port, username, password);
            }

            DialogResult = true;
            Close();
        }

        private void SaveConfig(string hostname, int port, string username, string password)
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                var json = "{\n" +
                           "    \"ssh\": {\n" +
                           $"        \"hostname\": \"{EscapeJson(hostname)}\",\n" +
                           $"        \"port\": {port},\n" +
                           $"        \"username\": \"{EscapeJson(username)}\",\n" +
                           $"        \"password\": \"{EscapeJson(password)}\",\n" +
                           "        \"keyPath\": null\n" +
                           "    }\n" +
                           "}";
                File.WriteAllText(configPath, json);
            }
            catch { /* Config yazılamazsa sessizce devam et */ }
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private void ShowStatus(string message, string textColor, string bgColor, string borderColor)
        {
            StatusBorder.Visibility = Visibility.Visible;
            StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor));
            StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderColor));
            TxtStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor));
            TxtStatus.Text = message;
        }

        private string? ExtractValue(string json, string key)
        {
            var keyPattern = $"\"{key}\"";
            var idx = json.IndexOf(keyPattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var colonIdx = json.IndexOf(':', idx + keyPattern.Length);
            if (colonIdx < 0) return null;

            var afterColon = json.Substring(colonIdx + 1).TrimStart();
            if (afterColon.StartsWith("null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (afterColon.StartsWith("\""))
            {
                var endQuote = afterColon.IndexOf('"', 1);
                return endQuote > 0 ? afterColon.Substring(1, endQuote - 1) : null;
            }

            // Numeric value
            var end = afterColon.IndexOfAny(new[] { ',', '}', '\n', '\r' });
            return end > 0 ? afterColon.Substring(0, end).Trim() : afterColon.Trim();
        }
    }
}
