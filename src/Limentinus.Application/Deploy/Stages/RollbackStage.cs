using Limentinus.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Limentinus.Application.Deploy.Stages;

public sealed class RollbackStage : IRollbackStage
{
    private readonly IDockerDriver _driver;
    private readonly ILogger<RollbackStage> _log;

    public RollbackStage(IDockerDriver driver, ILogger<RollbackStage> log)
    {
        _driver = driver;
        _log = log;
    }

    public async Task ExecuteAsync(DeployContext ctx, CancellationToken ct)
    {
        if (ctx.NewContainerId is not null)
        {
            try
            {
                await _driver.StopContainerAsync(ctx.NewContainerId, ct);
                await _driver.RemoveContainerAsync(ctx.NewContainerId, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to stop/remove new container {ContainerId} during rollback", ctx.NewContainerId);
            }
        }

        if (ctx.OldContainerId is not null)
        {
            try
            {
                await _driver.StartContainerByIdAsync(ctx.OldContainerId, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to restart old container {ContainerId} during rollback", ctx.OldContainerId);
            }
        }
    }
}
