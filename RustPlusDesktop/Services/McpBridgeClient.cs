using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

public class McpBridgeClient : IDisposable
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private string _host = "localhost";
    private int _port = 27071;
    private readonly Queue<Func<JsonElement, Task>> _pendingRequests = new();
    private int _requestId = 0;

    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? ConnectionStatusChanged;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<AiResponseEventArgs>? AiResponseReceived;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(string? host = null, int? port = null, CancellationToken ct = default)
    {
        if (host != null) _host = host;
        if (port != null) _port = port.Value;

        Disconnect();

        _webSocket = new ClientWebSocket();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var uri = new Uri($"ws://{_host}:{_port}/mcp");

        try
        {
            ConnectionStatusChanged?.Invoke(this, "Connecting to DuckBot MCP Bridge...");
            await _webSocket.ConnectAsync(uri, _cts.Token);
            ConnectionStatusChanged?.Invoke(this, "Connected to DuckBot MCP Bridge");
            _ = ReceiveLoopAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"MCP Bridge connection failed: {ex.Message}");
            throw;
        }
    }

    public void Disconnect()
    {
        try { _cts?.Cancel(); } catch { }
        try { _webSocket?.Dispose(); } catch { }
        _webSocket = null;
        _cts = null;
    }

    public async Task<string> SendAiRequestAsync(string prompt, string? context = null, CancellationToken ct = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("MCP Bridge not connected");

        var requestId = Interlocked.Increment(ref _requestId);
        var payload = new
        {
            type = "ai_request",
            id = requestId,
            prompt,
            context = context ?? "",
            provider = "auto"
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);

        var tcs = new TaskCompletionSource<string>();
        void Handler(object? s, AiResponseEventArgs e)
        {
            if (e.RequestId == requestId)
            {
                AiResponseReceived -= Handler;
                tcs.TrySetResult(e.Response);
            }
        }

        AiResponseReceived += Handler;
        try
        {
            using var registration = ct.Register(() => tcs.TrySetCanceled());
            return await tcs.Task;
        }
        catch
        {
            AiResponseReceived -= Handler;
            throw;
        }
    }

    public async Task SendCommandAsync(string command, Dictionary<string, string>? args = null, CancellationToken ct = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("MCP Bridge not connected");

        var payload = new
        {
            type = "command",
            command,
            args = args ?? new Dictionary<string, string>()
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    public async Task RequestCommandListAsync(CancellationToken ct = default)
    {
        await SendCommandAsync("list_commands", null, ct);
    }

    public async Task ExecuteChatCommandAsync(string chatCommand, CancellationToken ct = default)
    {
        var parts = chatCommand.TrimStart('/').Split(' ', 2);
        var cmd = parts[0];
        var args = parts.Length > 1 ? parts[1] : "";

        await SendCommandAsync("execute", new Dictionary<string, string>
        {
            { "command", cmd },
            { "args", args }
        }, ct);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[16384];
        var messageBuilder = new StringBuilder();

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !(_cts?.IsCancellationRequested ?? true))
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts?.Token ?? default);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", default);
                    ConnectionStatusChanged?.Invoke(this, "Disconnected from MCP Bridge");
                    break;
                }

                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();
                    ProcessMessage(message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"MCP Bridge receive error: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, "MCP Bridge Error");
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var type = typeProp.GetString();

            switch (type)
            {
                case "ai_response":
                    var id = root.TryGetProperty("id", out var reqId) ? reqId.GetInt32() : 0;
                    var response = root.TryGetProperty("response", out var resp) ? resp.GetString() ?? "" : "";
                    AiResponseReceived?.Invoke(this, new AiResponseEventArgs(id, response));
                    break;

                case "command_result":
                    var success = root.TryGetProperty("success", out var ok) && ok.GetBoolean();
                    var resultMsg = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";
                    MessageReceived?.Invoke(this, success ? $"[OK] {resultMsg}" : $"[ERROR] {resultMsg}");
                    break;

                case "error":
                    if (root.TryGetProperty("message", out var errMsg))
                        ErrorOccurred?.Invoke(this, errMsg.GetString() ?? "Unknown MCP error");
                    break;

                case "pong":
                    break;

                default:
                    MessageReceived?.Invoke(this, $"[MCP] {message}");
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"MCP parse error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}

public class AiResponseEventArgs : EventArgs
{
    public int RequestId { get; }
    public string Response { get; }
    public AiResponseEventArgs(int requestId, string response)
    {
        RequestId = requestId;
        Response = response;
    }
}