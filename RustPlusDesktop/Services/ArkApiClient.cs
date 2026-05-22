using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArkDuckBot.Services;

public class ArkApiClient : IDisposable
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private string _host = "";
    private int _port;
    private readonly List<string> _eventHistory = new();

    public event EventHandler<ArkPlayerListEventArgs>? PlayersUpdated;
    public event EventHandler<ArkTribeEventArgs>? TribeUpdated;
    public event EventHandler<ArkChatMessageEventArgs>? ChatMessageReceived;
    public event EventHandler<DinoEventArgs>? DinoUpdated;
    public event EventHandler<string>? ConnectionStatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        _host = host;
        _port = port;

        Disconnect();

        _webSocket = new ClientWebSocket();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var uri = new Uri($"ws://{host}:{port}/api");

        try
        {
            ConnectionStatusChanged?.Invoke(this, "Connecting...");
            await _webSocket.ConnectAsync(uri, _cts.Token);
            ConnectionStatusChanged?.Invoke(this, "Connected");
            _ = ReceiveLoopAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Connection failed: {ex.Message}");
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

    public async Task SendCommandAsync(string command, Dictionary<string, object>? args = null)
    {
        if (_webSocket?.State != WebSocketState.Open)
            throw new InvalidOperationException("Not connected");

        var payload = new
        {
            type = "command",
            command,
            args = args ?? new Dictionary<string, object>()
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? default);
    }

    public async Task RequestPlayerListAsync()
    {
        await SendCommandAsync("get_players");
    }

    public async Task RequestTribeInfoAsync(string playerId)
    {
        await SendCommandAsync("get_tribe", new Dictionary<string, object> { { "player_id", playerId } });
    }

    public async Task RequestServerInfoAsync()
    {
        await SendCommandAsync("get_server_info");
    }

    public async Task SendChatMessageAsync(string message)
    {
        await SendCommandAsync("send_chat", new Dictionary<string, object> { { "message", message } });
    }

    public async Task RequestDinoListAsync()
    {
        await SendCommandAsync("get_dinos");
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();

        try
        {
            while (_webSocket?.State == WebSocketState.Open && !(_cts?.IsCancellationRequested ?? true))
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts?.Token ?? default);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", default);
                    ConnectionStatusChanged?.Invoke(this, "Disconnected");
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
            ErrorOccurred?.Invoke(this, $"Receive error: {ex.Message}");
            ConnectionStatusChanged?.Invoke(this, "Error");
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
                case "players":
                    var players = ParsePlayerList(root);
                    PlayersUpdated?.Invoke(this, new ArkPlayerListEventArgs(players));
                    break;

                case "tribe":
                    var tribe = ParseTribeInfo(root);
                    TribeUpdated?.Invoke(this, new ArkTribeEventArgs(tribe));
                    break;

                case "chat":
                    var chatMsg = ParseChatMessage(root);
                    ChatMessageReceived?.Invoke(this, new ArkChatMessageEventArgs(chatMsg));
                    break;

                case "dino_update":
                    var dino = ParseDinoInfo(root);
                    DinoUpdated?.Invoke(this, new DinoEventArgs(dino));
                    break;

                case "event":
                    _eventHistory.Add(message);
                    if (_eventHistory.Count > 100) _eventHistory.RemoveAt(0);
                    break;

                case "error":
                    if (root.TryGetProperty("message", out var errMsg))
                        ErrorOccurred?.Invoke(this, errMsg.GetString() ?? "Unknown error");
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Parse error: {ex.Message}");
        }
    }

    private List<ArkPlayer> ParsePlayerList(JsonElement root)
    {
        var players = new List<ArkPlayer>();
        if (root.TryGetProperty("players", out var arr))
        {
            foreach (var p in arr.EnumerateArray())
            {
                players.Add(new ArkPlayer
                {
                    Id = p.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Name = p.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    Level = p.TryGetProperty("level", out var lvl) ? lvl.GetInt32() : 0,
                    Tribe = p.TryGetProperty("tribe", out var tribe) ? tribe.GetString() ?? "" : "",
                    IsOnline = p.TryGetProperty("online", out var online) ? online.GetBoolean() : false,
                    X = p.TryGetProperty("x", out var x) ? x.GetDouble() : 0,
                    Y = p.TryGetProperty("y", out var y) ? y.GetDouble() : 0,
                    Z = p.TryGetProperty("z", out var z) ? z.GetDouble() : 0
                });
            }
        }
        return players;
    }

    private ArkTribe ParseTribeInfo(JsonElement root)
    {
        return new ArkTribe
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetInt64() : 0,
            Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Owner = root.TryGetProperty("owner", out var owner) ? owner.GetString() ?? "" : "",
            Members = root.TryGetProperty("members", out var members) ? members.GetInt32() : 0,
            DinoCount = root.TryGetProperty("dinos", out var dinos) ? dinos.GetInt32() : 0
        };
    }

    private ArkChatMessage ParseChatMessage(JsonElement root)
    {
        return new ArkChatMessage
        {
            PlayerId = root.TryGetProperty("player_id", out var pid) ? pid.GetString() ?? "" : "",
            PlayerName = root.TryGetProperty("player_name", out var pname) ? pname.GetString() ?? "" : "",
            Message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
            Timestamp = DateTime.UtcNow
        };
    }

    private ArkDino ParseDinoInfo(JsonElement root)
    {
        return new ArkDino
        {
            Id = root.TryGetProperty("id", out var id) ? id.GetInt64() : 0,
            Name = root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            Species = root.TryGetProperty("species", out var sp) ? sp.GetString() ?? "" : "",
            Level = root.TryGetProperty("level", out var lvl) ? lvl.GetInt32() : 0,
            Health = root.TryGetProperty("health", out var hp) ? hp.GetDouble() : 0,
            Position = root.TryGetProperty("x", out var x) && root.TryGetProperty("y", out var y)
                ? (x.GetDouble(), y.GetDouble()) : (0, 0),
            IsTamed = root.TryGetProperty("tamed", out var tamed) ? tamed.GetBoolean() : false,
            Owner = root.TryGetProperty("owner", out var owner) ? owner.GetString() ?? "" : ""
        };
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}

public class ArkPlayer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string Tribe { get; set; } = "";
    public bool IsOnline { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class ArkTribe
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Members { get; set; }
    public int DinoCount { get; set; }
}

public class ArkChatMessage
{
    public string PlayerId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public class ArkDino
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Species { get; set; } = "";
    public int Level { get; set; }
    public double Health { get; set; }
    public (double X, double Y) Position { get; set; }
    public bool IsTamed { get; set; }
    public string Owner { get; set; } = "";
}

public class ArkPlayerListEventArgs : EventArgs
{
    public List<ArkPlayer> Players { get; }
    public ArkPlayerListEventArgs(List<ArkPlayer> players) => Players = players;
}

public class ArkTribeEventArgs : EventArgs
{
    public ArkTribe Tribe { get; }
    public ArkTribeEventArgs(ArkTribe tribe) => Tribe = tribe;
}

public class ArkChatMessageEventArgs : EventArgs
{
    public ArkChatMessage Message { get; }
    public ArkChatMessageEventArgs(ArkChatMessage msg) => Message = msg;
}

public class DinoEventArgs : EventArgs
{
    public ArkDino Dino { get; }
    public DinoEventArgs(ArkDino dino) => Dino = dino;
}
