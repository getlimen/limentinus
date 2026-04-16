using Limentinus.Application.Common.Interfaces;

namespace Limentinus.Application.Deploy.Stages;

public sealed class HealthCheckStage : IDeployStage
{
    private readonly IDockerDriver _driver;

    public HealthCheckStage(IDockerDriver driver) => _driver = driver;

    public string Name => "HealthCheck";

    public async Task<DeployStageResult> ExecuteAsync(DeployContext ctx, CancellationToken ct)
    {
        if (ctx.NewContainerId is null)
        {
            return DeployStageResult.Fail("No container started");
        }

        var hc = ctx.Request.HealthCheck;
        if (hc is null)
        {
            return DeployStageResult.Ok();
        }

        if (hc.Command is not null)
        {
            return await RunCommandHealthCheck(ctx, hc, ct);
        }

        return await RunHttpHealthCheck(ctx, hc, ct);
    }

    private async Task<DeployStageResult> RunCommandHealthCheck(DeployContext ctx, Limen.Contracts.AgentMessages.HealthCheckSpec hc, CancellationToken ct)
    {
        for (var attempt = 0; attempt < hc.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(hc.IntervalSeconds), ct);
            }

            var ok = await _driver.ExecHealthCheckAsync(ctx.NewContainerId!, hc.Command!, hc.TimeoutSeconds, ct);
            if (ok)
            {
                return DeployStageResult.Ok();
            }
        }

        return DeployStageResult.Fail($"Health check command failed after {hc.MaxRetries} attempts");
    }

    private async Task<DeployStageResult> RunHttpHealthCheck(DeployContext ctx, Limen.Contracts.AgentMessages.HealthCheckSpec hc, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(hc.TimeoutSeconds) };

        int? resolvedPort = null;

        for (var attempt = 0; attempt < hc.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(hc.IntervalSeconds), ct);
            }

            // Resolve port inside the loop so transient "port not ready" failures are
            // retried along with HTTP probe failures. Cache the result once obtained.
            if (resolvedPort is null)
            {
                resolvedPort = await _driver.GetContainerHostPortAsync(ctx.NewContainerId!, ctx.Request.InternalPort, ct);
            }

            if (resolvedPort is null)
            {
                // Port not yet mapped — retry on next attempt.
                continue;
            }

            try
            {
                var resp = await http.GetAsync($"http://127.0.0.1:{resolvedPort}/", ct);
                if (resp.IsSuccessStatusCode)
                {
                    return DeployStageResult.Ok();
                }
            }
            catch
            {
                // retry
            }
        }

        if (resolvedPort is null)
        {
            return DeployStageResult.Fail("Could not determine host port for HTTP health check");
        }

        return DeployStageResult.Fail($"HTTP health check failed after {hc.MaxRetries} attempts");
    }
}
