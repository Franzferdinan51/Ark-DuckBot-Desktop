using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

/// <summary>
/// WebSocket client for DuckBot MCP Bridge.
/// Connects to the Python bridge server that provides AI chat via LLM providers.
///
/// Protocol: https://github.com/Franzferdinan51/DuckBot-For-ark/tree/main/mcp-bridge
/// </summary>
public class McpBridgeClient : IDisposable
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private string _host = "localhost";
    private int _port = 8443;
    private int _adminPort = 8444;
    private string _sharedSecret = "";
    private readonly Queue<Func<JsonElement, Task>> _pendingRequests = new();
    private int _requestId = 0;

    // Authenticated player context
    public string? PlayerId { get; private set; }
    public string? PlayerName { get; private set; }
    public string? PlayerTier { get; private set; }
    public string? TribeId { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public event EventHandler<string>? MessageReceived;
    public event EventHandler<string>? ConnectionStatusChanged;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<AiResponseEventArgs>? AiResponseReceived;
    public event EventHandler<string>? ThinkingStateChanged;
    public event EventHandler<GameEventArgs>? GameEventReceived;
    public event EventHandler<AuthSuccessEventArgs>? AuthSuccessReceived;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(string? host = null, int? port = null, int? adminPort = null, string? sharedSecret = null, CancellationToken ct = default)
    {
        if (host != null) _host = host;
        if (port != null) _port = port.Value;
        if (adminPort != null) _adminPort = adminPort.Value;
        if (sharedSecret != null) _sharedSecret = sharedSecret;

        Disconnect();

        _webSocket = new ClientWebSocket();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Use admin_port (8444) for desktop companion app connection
        var uri = new Uri($"ws://{_host}:{_adminPort}");

        try
        {
            ConnectionStatusChanged?.Invoke(this, $"Connecting to DuckBot MCP Bridge on port {_adminPort}...");
            await _webSocket.ConnectAsync(uri, _cts.Token);

            // Send authentication
            await SendAuthAsync();

            _ = ReceiveLoopAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"MCP Bridge connection failed: {ex.Message}");
            throw;
        }
    }

    private async Task SendAuthAsync()
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected");

        var payload = new
        {
            type = "auth",
            token = _sharedSecret,
            player = new
            {
                id = "desktop_client",
                name = "ArkDuckBot",
                tier = "admin"
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? default);
    }

    public void Disconnect()
    {
        IsAuthenticated = false;
        try { _cts?.Cancel(); } catch { }
        try { _webSocket?.Dispose(); } catch { }
        _webSocket = null;
        _cts = null;
    }

    public async Task<string> SendAiRequestAsync(string prompt, string? context = null, CancellationToken ct = default)
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("MCP Bridge not connected");

        if (!IsAuthenticated)
            throw new InvalidOperationException("Not authenticated with MCP Bridge");

        var requestId = Interlocked.Increment(ref _requestId);
        var payload = new
        {
            type = "player_message",
            id = requestId,
            message = prompt,
            context = context ?? "",
            sender = new
            {
                id = PlayerId ?? "desktop_client",
                name = PlayerName ?? "ArkDuckBot",
                tier = PlayerTier ?? "admin"
            }
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

    public async Task SendChatMessageAsync(string message, CancellationToken ct = default)
    {
        if (_webSocket?.State != WebSocketState.Open || !IsAuthenticated)
            throw new InvalidOperationException("MCP Bridge not connected or authenticated");

        var payload = new
        {
            type = "player_message",
            id = Interlocked.Increment(ref _requestId),
            message = $"/{message}",
            sender = new
            {
                id = PlayerId ?? "desktop_client",
                name = PlayerName ?? "ArkDuckBot",
                tier = PlayerTier ?? "admin"
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    public async Task SendPositionUpdateAsync(double x, double y, double z, CancellationToken ct = default)
    {
        if (_webSocket?.State != WebSocketState.Open || !IsAuthenticated)
            return;

        var payload = new
        {
            type = "position_update",
            position = new { x, y, z }
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    public async Task SendGameEventAsync(string eventType, Dictionary<string, object> eventData, CancellationToken ct = default)
    {
        if (_webSocket?.State != WebSocketState.Open || !IsAuthenticated)
            return;

        var payload = new
        {
            type = "event",
            event = eventType,
            data = eventData
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    public async Task SendPingAsync()
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;

        var payload = new { type = "ping" };
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? default);
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
                    IsAuthenticated = false;
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
                case "auth_success":
                    // Authentication successful, store player context
                    if (root.TryGetProperty("player", out var player))
                    {
                        PlayerId = player.TryGetProperty("id", out var id) ? id.GetString() : null;
                        PlayerName = player.TryGetProperty("name", out var name) ? name.GetString() : null;
                        PlayerTier = player.TryGetProperty("tier", out var tier) ? tier.GetString() : null;
                        TribeId = player.TryGetProperty("tribe", out var tribe) ? tribe.GetString() : null;
                    }
                    IsAuthenticated = true;
                    ConnectionStatusChanged?.Invoke(this, $"Connected as {PlayerName} ({PlayerTier})");
                    AuthSuccessReceived?.Invoke(this, new AuthSuccessEventArgs(PlayerTier ?? "user"));
                    break;

                case "thinking":
                    // AI is processing
                    ThinkingStateChanged?.Invoke(this, "Thinking...");
                    break;

                case "reply":
                    // AI response with stats
                    var id = root.TryGetProperty("id", out var reqId) ? reqId.GetInt32() : 0;
                    var response = root.TryGetProperty("response", out var resp) ? resp.GetString() ?? "" : "";
                    var stats = root.TryGetProperty("stats", out var st) ? st : default;
                    ThinkingStateChanged?.Invoke(this, "");
                    AiResponseReceived?.Invoke(this, new AiResponseEventArgs(id, response));
                    MessageReceived?.Invoke(this, $"[AI] {response}");
                    break;

                case "error":
                    if (root.TryGetProperty("message", out var errMsg))
                    {
                        ErrorOccurred?.Invoke(this, errMsg.GetString() ?? "Unknown MCP error");
                    }
                    ThinkingStateChanged?.Invoke(this, "");
                    break;

                case "pong":
                    break;

                case "event":
                    // Game event from plugin (dino tamed, player joined, etc.)
                    if (root.TryGetProperty("event", out var evt) && root.TryGetProperty("data", out var data))
                    {
                        GameEventReceived?.Invoke(this, new GameEventArgs(evt.GetString() ?? "", data.GetRawText()));
                    }
                    break;

                case "player_message":
                    // Echo of our own message or broadcast
                    if (root.TryGetProperty("message", out var msg))
                    {
                        MessageReceived?.Invoke(this, msg.GetString() ?? "");
                    }
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

public class AuthSuccessEventArgs : EventArgs
{
    public string PlayerTier { get; }
    public AuthSuccessEventArgs(string playerTier)
    {
        PlayerTier = playerTier;
    }
}

public class GameEventArgs : EventArgs
{
    public string EventType { get; }
    public string EventData { get; }
    public GameEventArgs(string eventType, string eventData)
    {
        EventType = eventType;
        EventData = eventData;
    }
}