using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Services;

namespace Limentinus.API;

public sealed class AgentWorker : BackgroundService
{
    private readonly EnrollmentService _enroll;
    private readonly ILimenControlClient _client;
    private readonly ILogger<AgentWorker> _log;

    public AgentWorker(EnrollmentService enroll, ILimenControlClient client, ILogger<AgentWorker> log)
    {
        _enroll = enroll;
        _client = client;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var hostname = Environment.MachineName;
        var roles = (Environment.GetEnvironmentVariable("LIMEN_ROLES") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var provisioningKey = Environment.GetEnvironmentVariable("LIMEN_PROVISIONING_KEY") ?? string.Empty;

        _log.LogInformation("Limentinus starting; hostname={Hostname}, roles={Roles}", hostname, string.Join(',', roles));

        var identity = await _enroll.EnsureEnrolledAsync(
            provisioningKey,
            hostname,
            roles,
            Environment.OSVersion.Platform.ToString(),
            "0.1.0",
            ct);

        _log.LogInformation("Enrolled as agentId={AgentId}; starting control loop", identity.AgentId);
        await _client.RunAsync(identity, ct);
    }
}
