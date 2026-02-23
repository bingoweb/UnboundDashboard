using System;
using System.IO;
using System.Text.Json;

namespace UnboundDashboard.Services
{
    public class SshConfig
    {
        public string Hostname { get; set; } = "192.168.1.123";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "root";
        public string? Password { get; set; }
        public string? KeyPath { get; set; }
    }

    public class AppConfig
    {
        public SshConfig Ssh { get; set; } = new();
    }

    public class ConfigurationService
    {
        private const string ConfigFileName = "appsettings.json";
        private readonly string _configPath;

        public ConfigurationService(string? configPath = null)
        {
            _configPath = configPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        }

        public AppConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    // Create default config if doesn't exist
                    var defaultConfig = new AppConfig();
                    SaveConfig(defaultConfig);
                    return defaultConfig;
                }

                var json = File.ReadAllText(_configPath);

                // Use System.Text.Json for proper parsing
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var config = JsonSerializer.Deserialize<AppConfig>(json, options);
                return config ?? new AppConfig();
            }
            catch (JsonException ex)
            {
                // Malformed JSON - log and return default
                Console.WriteLine($"Config parse error: {ex.Message}");
                return new AppConfig();
            }
            catch (IOException ex)
            {
                // File access error - log and return default
                Console.WriteLine($"Config read error: {ex.Message}");
                return new AppConfig();
            }
            catch (Exception ex)
            {
                // Unexpected error - log and return default
                Console.WriteLine($"Unexpected config error: {ex.Message}");
                return new AppConfig();
            }
        }

        public bool SaveConfig(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_configPath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config save error: {ex.Message}");
                return false;
            }
        }

        public void SaveSshCredentials(string hostname, int port, string username, string? password, string? keyPath)
        {
            var config = LoadConfig();
            config.Ssh.Hostname = hostname;
            config.Ssh.Port = port;
            config.Ssh.Username = username;
            config.Ssh.Password = password;
            config.Ssh.KeyPath = keyPath;
            SaveConfig(config);
        }
    }
}
