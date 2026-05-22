using System.Windows.Controls;
using System.Windows.Input;
using ArkDuckBot.ViewModels;

namespace ArkDuckBot.Views.MainWindow.AI;

public partial class AIChatPanel : UserControl
{
    public AIChatPanel()
    {
        InitializeComponent();
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
}