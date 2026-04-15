using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Services;
using Limentinus.Domain.Node;

namespace Limentinus.API;

public sealed class AgentWorker : BackgroundService
{
    private readonly EnrollmentService _enroll;
    private readonly ILimenControlClient _client;
    private readonly IWireGuardClient _wg;
    private readonly ILogger<AgentWorker> _log;

    public AgentWorker(EnrollmentService enroll, ILimenControlClient client, IWireGuardClient wg, ILogger<AgentWorker> log)
    {
        _enroll = enroll;
        _client = client;
        _wg = wg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var hostname = Environment.MachineName;
        var roles = (Environment.GetEnvironmentVariable("LIMEN_ROLES") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var provisioningKey = Environment.GetEnvironmentVariable("LIMEN_PROVISIONING_KEY") ?? string.Empty;

        _log.LogInformation("Limentinus starting; hostname={Hostname}, roles={Roles}", hostname, string.Join(',', roles));

        var outcome = await _enroll.EnsureEnrolledAsync(provisioningKey, hostname, roles,
            Environment.OSVersion.Platform.ToString(), "0.1.0", ct);

        if (outcome.Wireguard is not null)
        {
            try { await _wg.BringUpAsync(outcome.Wireguard, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "Failed to bring up WG tunnel; continuing in clear-WS dev mode"); }
        }
        else
        {
            _log.LogInformation("No fresh WG config (identity persisted from previous run); continuing with existing tunnel if any");
        }

        _log.LogInformation("Enrolled as agentId={AgentId}; starting control loop", outcome.Identity.AgentId);
        await _client.RunAsync(outcome.Identity, ct);
    }
}
