using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using UnboundDashboard.ViewModels;
using UnboundDashboard.Services;

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
            var configService = new ConfigurationService();
            var config = configService.LoadConfig();
            return (config.Ssh.Hostname, config.Ssh.Port, config.Ssh.Username,
                    config.Ssh.Password, config.Ssh.KeyPath);
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
