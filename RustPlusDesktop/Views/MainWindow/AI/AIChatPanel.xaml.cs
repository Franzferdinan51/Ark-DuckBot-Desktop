using System.Windows.Controls;
using System.Windows.Input;
using ArkDuckBot.Services;
using ArkDuckBot.ViewModels;

namespace ArkDuckBot.Views.Panels;

public partial class AIChatPanel : UserControl
{
    private AiChatViewModel? _viewModel;

    public AIChatPanel()
    {
        InitializeComponent();
        _viewModel = DataContext as AiChatViewModel;
        PromptInput.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                var vm = DataContext as AiChatViewModel;
                if (vm != null)
                {
                    _ = vm.SendPromptAsync();
                    e.Handled = true;
                }
            }
        };
    }

    public void Initialize(McpBridgeClient client)
    {
        _viewModel?.Initialize(client);
    }
}
