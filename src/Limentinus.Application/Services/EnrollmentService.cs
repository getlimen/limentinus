using Limentinus.Application.Common.Interfaces;
using Limentinus.Domain.Node;

namespace Limentinus.Application.Services;

public sealed class EnrollmentService
{
    private readonly IIdentityStore _store;
    private readonly ILimenControlClient _client;

    public EnrollmentService(IIdentityStore store, ILimenControlClient client) { _store = store; _client = client; }

    public async Task<NodeIdentity> EnsureEnrolledAsync(
        string provisioningKey,
        string hostname,
        string[] roles,
        string platform,
        string version,
        CancellationToken ct)
    {
        var existing = await _store.LoadAsync(ct);
        if (existing is not null)
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(provisioningKey))
        {
            throw new InvalidOperationException("No local identity and no provisioning key provided. Set LIMEN_PROVISIONING_KEY on first run.");
        }

        var id = await _client.EnrollAsync(provisioningKey, hostname, roles, platform, version, ct);
        await _store.SaveAsync(id, ct);
        return id;
    }
}
