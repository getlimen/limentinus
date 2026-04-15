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
                // If FinalizeStage already renamed the old container to a temp name
                // (step 2 succeeded but step 3 failed), restore the original name first
                // so that the container is discoverable under its expected name.
                if (ctx.RenamedOldContainerName is not null)
                {
                    await _driver.RenameContainerAsync(ctx.OldContainerId, ctx.Request.ContainerName, ct);
                }

                await _driver.StartContainerByIdAsync(ctx.OldContainerId, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to restore/restart old container {ContainerId} during rollback", ctx.OldContainerId);
            }
        }
    }
}
