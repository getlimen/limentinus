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
        var hostPort = await _driver.GetContainerHostPortAsync(ctx.NewContainerId!, ctx.Request.InternalPort, ct);
        if (hostPort is null)
        {
            return DeployStageResult.Fail("Could not determine host port for HTTP health check");
        }

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(hc.TimeoutSeconds) };

        for (var attempt = 0; attempt < hc.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(hc.IntervalSeconds), ct);
            }

            try
            {
                var resp = await http.GetAsync($"http://127.0.0.1:{hostPort}/", ct);
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

        return DeployStageResult.Fail($"HTTP health check failed after {hc.MaxRetries} attempts");
    }
}
