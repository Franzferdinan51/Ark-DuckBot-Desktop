using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArkDuckBot.Services;

namespace ArkDuckBot.ViewModels;

public class AiChatViewModel : INotifyPropertyChanged
{
    private McpBridgeClient? _mcpClient;
    private string _currentPrompt = "";
    private string _currentResponse = "";
    private bool _isProcessing;
    private string _selectedProvider = "auto";
    private string _statusMessage = "AI Chat ready";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ChatMessage> Messages { get; } = new();

    // Commands
    public ICommand SendPromptCommand { get; }
    public ICommand ClearChatCommand { get; }

    public AiChatViewModel()
    {
        SendPromptCommand = new SimpleCommand(async _ => await SendPromptAsync(), _ => !IsProcessing && !string.IsNullOrWhiteSpace(CurrentPrompt));
        ClearChatCommand = new SimpleCommand(_ => ClearChat());
    }

    public string CurrentPrompt
    {
        get => _currentPrompt;
        set { _currentPrompt = value; OnPropertyChanged(); }
    }

    public string CurrentResponse
    {
        get => _currentResponse;
        set { _currentResponse = value; OnPropertyChanged(); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotProcessing)); }
    }

    public bool IsNotProcessing => !_isProcessing;

    public string SelectedProvider
    {
        get => _selectedProvider;
        set { _selectedProvider = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string[] AvailableProviders => new[] { "auto", "openrouter", "anthropic", "openai", "gemini" };

    public void Initialize(McpBridgeClient client)
    {
        _mcpClient = client;
        _mcpClient.AiResponseReceived += OnAiResponse;
        _mcpClient.ErrorOccurred += OnMcpError;
        _mcpClient.ConnectionStatusChanged += OnConnectionChanged;
        _mcpClient.ThinkingStateChanged += OnThinkingStateChanged;

        AddSystemMessage("AI Chat initialized. DuckBot MCP Bridge required for AI features.");
    }

    private void OnThinkingStateChanged(object? sender, string state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(state))
            {
                StatusMessage = state;
                IsProcessing = true;
            }
            else
            {
                IsProcessing = false;
                StatusMessage = "Ready";
            }
        });
    }

    private void OnConnectionChanged(object? sender, string status)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = status;
            if (status.Contains("Connected"))
                AddSystemMessage("Connected to DuckBot MCP Bridge");
        });
    }

    private void OnMcpError(object? sender, string error)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Error: {error}";
            AddSystemMessage($"Error: {error}");
        });
    }

    private void OnAiResponse(object? sender, AiResponseEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsProcessing = false;
            CurrentResponse = e.Response;

            if (Messages.Count > 0 && Messages[^1].IsUser == false)
            {
                Messages[^1] = new ChatMessage { IsUser = false, Content = e.Response };
            }
            else
            {
                AddAiMessage(e.Response);
            }
        });
    }

    public async Task SendPromptAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPrompt) || _mcpClient == null || !_mcpClient.IsConnected)
        {
            if (!_mcpClient?.IsConnected ?? true)
                AddSystemMessage("MCP Bridge not connected. Connect to a server first.");
            return;
        }

        var prompt = CurrentPrompt;
        CurrentPrompt = "";
        IsProcessing = true;

        AddUserMessage(prompt);

        try
        {
            var response = await _mcpClient.SendAiRequestAsync(prompt);
            CurrentResponse = response;
        }
        catch (System.Exception ex)
        {
            AddSystemMessage($"Request failed: {ex.Message}");
            IsProcessing = false;
        }
    }

    public void ClearChat()
    {
        Messages.Clear();
        CurrentPrompt = "";
        CurrentResponse = "";
        StatusMessage = "Chat cleared";
    }

    public void AddUserMessage(string content)
    {
        Messages.Add(new ChatMessage { IsUser = true, Content = content });
    }

    public void AddAiMessage(string content)
    {
        Messages.Add(new ChatMessage { IsUser = false, Content = content });
    }

    public void AddSystemMessage(string content)
    {
        Messages.Add(new ChatMessage { IsUser = null, Content = content });
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ChatMessage : INotifyPropertyChanged
{
    private string _content = "";
    private bool? _isUser;

    public bool? IsUser
    {
        get => _isUser;
        set { _isUser = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSystem)); }
    }

    public bool IsSystem => _isUser == null;

    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
