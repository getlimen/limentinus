using Limentinus.Domain.Node;

namespace Limentinus.Application.Common.Interfaces;

public interface IIdentityStore
{
    Task<NodeIdentity?> LoadAsync(CancellationToken ct);
    Task SaveAsync(NodeIdentity id, CancellationToken ct);
}
