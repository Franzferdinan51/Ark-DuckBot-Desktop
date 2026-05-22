using System.Windows;
using ArkDuckBot.Services;

namespace ArkDuckBot.Views
{
    public partial class SettingsModal : Window
    {
        private bool _isInitialized = false;

        public string McpHost { get; set; } = "localhost";
        public string McpPort { get; set; } = "8443";
        public string AiProvider { get; set; } = "openrouter";

        public SettingsModal()
        {
            InitializeComponent();
            LoadSettings();
            _isInitialized = true;
        }

        private void LoadSettings()
        {
            ChkAutoStart.IsChecked = TrackingService.AutoStartEnabled;
            ChkStartMinimized.IsChecked = TrackingService.StartMinimizedEnabled;
            ChkAutoConnect.IsChecked = TrackingService.AutoConnectEnabled;
            ChkCloseToTray.IsChecked = TrackingService.CloseToTrayEnabled;
            ChkBackgroundTracking.IsChecked = TrackingService.IsBackgroundTrackingEnabled;
            ChkAutoLoadShops.IsChecked = TrackingService.AutoLoadShops;
            ChkHideConsole.IsChecked = TrackingService.HideConsole;

            // Load MCP settings
            McpHost = TrackingService.McpHost ?? "localhost";
            McpPort = (TrackingService.McpPort ?? 8443).ToString();
            AiProvider = TrackingService.AiProvider ?? "openrouter";

            TxtMcpHost.Text = McpHost;
            TxtMcpPort.Text = McpPort;

            // Set AI provider combobox
            foreach (System.Windows.Controls.ComboBoxItem item in CmbAiProvider.Items)
            {
                if (item.Tag?.ToString() == AiProvider)
                {
                    CmbAiProvider.SelectedItem = item;
                    break;
                }
            }
        }

        private void OnSettingChanged(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            TrackingService.AutoStartEnabled = ChkAutoStart.IsChecked == true;
            TrackingService.StartMinimizedEnabled = ChkStartMinimized.IsChecked == true;
            TrackingService.AutoConnectEnabled = ChkAutoConnect.IsChecked == true;
            TrackingService.CloseToTrayEnabled = ChkCloseToTray.IsChecked == true;
            TrackingService.IsBackgroundTrackingEnabled = ChkBackgroundTracking.IsChecked == true;
            TrackingService.AutoLoadShops = ChkAutoLoadShops.IsChecked == true;
            TrackingService.HideConsole = ChkHideConsole.IsChecked == true;
        }

        private void OnMcpSettingChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_isInitialized) return;

            McpHost = TxtMcpHost.Text;
            if (int.TryParse(TxtMcpPort.Text, out var port))
            {
                TrackingService.McpPort = port;
            }
            TrackingService.McpHost = McpHost;
            UpdateMcpStatus();
        }

        private void OnMcpSecretChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_isInitialized) return;
            TrackingService.McpSecret = TxtMcpSecret.Password;
            UpdateMcpStatus();
        }

        private void OnAiProviderChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            if (CmbAiProvider.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                AiProvider = item.Tag?.ToString() ?? "openrouter";
                TrackingService.AiProvider = AiProvider;
            }
        }

        private void UpdateMcpStatus()
        {
            if (TrackingService.IsMcpConnected)
            {
                TxtMcpStatus.Text = $"Connected to {McpHost}:{McpPort}";
                TxtMcpStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
            }
            else
            {
                TxtMcpStatus.Text = "Not connected";
                TxtMcpStatus.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public string RequestAction { get; private set; }

        private void BtnModifyChatAlerts_Click(object sender, RoutedEventArgs e)
        {
            RequestAction = "ModifyChatAlerts";
            Close();
        }

        private void BtnChatCommands_Click(object sender, RoutedEventArgs e)
        {
            RequestAction = "ChatCommands";
            Close();
        }

        protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }
    }
}