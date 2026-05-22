using ArkDuckBot.Models;
using System.Threading;
using System.Threading.Tasks;
// using static ArkDuckBot.Services.ArkClientReal;

namespace ArkDuckBot.Services;


    public interface IArkClient
{
    Task ConnectAsync(ServerProfile profile, CancellationToken ct = default);
    Task DisconnectAsync();
    Task ToggleSmartSwitchAsync(long entityId, bool on, CancellationToken ct = default);
    Task<bool?> GetSmartSwitchStateAsync(uint entityId);
    string? Host { get; }

    // NEU:
    Task<EntityProbeResult> ProbeEntityAsync(uint entityId, CancellationToken ct = default);
}
