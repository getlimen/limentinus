using Limen.Contracts.AgentMessages;

namespace Limentinus.Application.Common.Interfaces;

public interface IWireGuardClient
{
    Task BringUpAsync(WireGuardConfig cfg, CancellationToken ct);
    Task TearDownAsync(CancellationToken ct);
    bool IsAvailable { get; }
}
