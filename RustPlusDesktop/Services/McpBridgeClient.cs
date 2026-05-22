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
    public event EventHandler<string>? StreamTokenReceived;

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

        // Match server.py auth format: player_id, display_name, tier, tribe_id, position, facing_yaw
        var payload = new
        {
            type = "auth",
            token = _sharedSecret,
            player = new
            {
                player_id = "desktop_client",
                display_name = "ArkDuckBot",
                tier = "admin",
                tribe_id = (string?)null,
                position = new { x = 0.0, y = 0.0, z = 0.0 },
                facing_yaw = 0.0
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
        // Match server.py player_message format: message, request_id, position, facing_yaw
        var payload = new
        {
            type = "player_message",
            request_id = requestId.ToString(),
            message = prompt
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

        // Match server.py player_message format
        var payload = new
        {
            type = "player_message",
            request_id = Interlocked.Increment(ref _requestId).ToString(),
            message = $"/{message}"
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

        // Match server.py event format: { "type": "event", "data": { "event": "type", ... } }
        var data = new Dictionary<string, object>(eventData) { { "event", eventType } };
        var payload = new
        {
            type = "event",
            data
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
                    // Server sends flat: { "type": "auth_success", "player_id": "...", "tier": "...", "tools_available": N }
                    PlayerId = root.TryGetProperty("player_id", out var pidProp) ? pidProp.GetString() : null;
                    PlayerTier = root.TryGetProperty("tier", out var tierProp) ? tierProp.GetString() : null;
                    PlayerName = PlayerId; // Server doesn't send display_name back
                    IsAuthenticated = true;
                    ConnectionStatusChanged?.Invoke(this, $"Connected as {PlayerName} ({PlayerTier})");
                    AuthSuccessReceived?.Invoke(this, new AuthSuccessEventArgs(PlayerTier ?? "player"));
                    break;

                case "thinking":
                    // AI is processing
                    ThinkingStateChanged?.Invoke(this, "Thinking...");
                    break;

                case "reply":
                    // Server sends: { "type": "reply", "message": "...", "request_id": "xxx", "stats": {...} }
                    var replyId = root.TryGetProperty("request_id", out var reqId) ? reqId.GetInt32() :
                                  root.TryGetProperty("id", out var legacyId) ? legacyId.GetInt32() : 0;
                    var response = root.TryGetProperty("message", out var resp) ? resp.GetString() ?? "" :
                                   root.TryGetProperty("response", out var legacyResp) ? legacyResp.GetString() ?? "" : "";
                    var stats = root.TryGetProperty("stats", out var st) ? st : default;
                    ThinkingStateChanged?.Invoke(this, "");
                    AiResponseReceived?.Invoke(this, new AiResponseEventArgs(replyId, response));
                    MessageReceived?.Invoke(this, $"[AI] {response}");
                    break;

                case "error":
                    if (root.TryGetProperty("message", out var errMsg))
                    {
                        ErrorOccurred?.Invoke(this, errMsg.GetString() ?? "Unknown MCP error");
                    }
                    ThinkingStateChanged?.Invoke(this, "");
                    break;

                case "stream_token":
                    // Incremental LLM output: { "type": "stream_token", "content": "<token>" }
                    if (root.TryGetProperty("content", out var tokenContent))
                    {
                        var token = tokenContent.GetString() ?? "";
                        StreamTokenReceived?.Invoke(this, token);
                        MessageReceived?.Invoke(this, token);
                    }
                    break;

                case "pong":
                    break;

                case "event":
                    // Server sends: { "type": "event", "data": { "event": "dino_tamed", ... } }
                    if (root.TryGetProperty("data", out var data))
                    {
                        var eventName = data.TryGetProperty("event", out var evt) ? evt.GetString() ?? "" : "";
                        GameEventReceived?.Invoke(this, new GameEventArgs(eventName, data.GetRawText()));
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
