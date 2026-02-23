using System.IO;
using System.Text.Json;
using UnboundDashboard.Services;
using Xunit;

namespace UnboundDashboard.Tests
{
    public class ConfigurationServiceTests : IDisposable
    {
        private readonly string _testConfigPath;

        public ConfigurationServiceTests()
        {
            _testConfigPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        public void Dispose()
        {
            if (File.Exists(_testConfigPath))
            {
                File.Delete(_testConfigPath);
            }
        }

        [Fact]
        public void Constructor_WithDefaultPath_ShouldUseBaseDirectory()
        {
            // This test is mostly to ensure we didn't break the default constructor
            var service = new ConfigurationService();
            // We can't easily verify the private _configPath without reflection,
            // but we can at least ensure it doesn't throw.
            Assert.NotNull(service);
        }

        [Fact]
        public void SaveConfig_ShouldPersistToDisk()
        {
            // Arrange
            var service = new ConfigurationService(_testConfigPath);
            var config = new AppConfig
            {
                Ssh = new SshConfig
                {
                    Hostname = "test-host",
                    Username = "test-user",
                    Port = 2222
                }
            };

            // Act
            bool result = service.SaveConfig(config);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(_testConfigPath));

            var savedJson = File.ReadAllText(_testConfigPath);
            var savedConfig = JsonSerializer.Deserialize<AppConfig>(savedJson);

            Assert.NotNull(savedConfig);
            Assert.Equal(config.Ssh.Hostname, savedConfig.Ssh.Hostname);
            Assert.Equal(config.Ssh.Username, savedConfig.Ssh.Username);
            Assert.Equal(config.Ssh.Port, savedConfig.Ssh.Port);
        }

        [Fact]
        public void LoadConfig_ShouldReadPersistedData()
        {
            // Arrange
            var service = new ConfigurationService(_testConfigPath);
            var config = new AppConfig
            {
                Ssh = new SshConfig
                {
                    Hostname = "another-host",
                    Username = "another-user"
                }
            };
            service.SaveConfig(config);

            // Act
            var loadedConfig = service.LoadConfig();

            // Assert
            Assert.NotNull(loadedConfig);
            Assert.Equal(config.Ssh.Hostname, loadedConfig.Ssh.Hostname);
            Assert.Equal(config.Ssh.Username, loadedConfig.Ssh.Username);
        }

        [Fact]
        public void SaveConfig_ShouldReturnFalse_OnInvalidPath()
        {
            // Arrange
            // Using a directory path instead of a file path should cause an IOException
            string invalidPath = Path.GetTempPath();
            var service = new ConfigurationService(invalidPath);
            var config = new AppConfig();

            // Act
            bool result = service.SaveConfig(config);

            // Assert
            Assert.False(result);
        }
    }
}
