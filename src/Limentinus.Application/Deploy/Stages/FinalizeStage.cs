using Limentinus.Application.Common.Interfaces;

namespace Limentinus.Application.Deploy.Stages;

public sealed class FinalizeStage : IDeployStage
{
    private readonly IDockerDriver _driver;

    public FinalizeStage(IDockerDriver driver) => _driver = driver;

    public string Name => "Finalize";

    public async Task<DeployStageResult> ExecuteAsync(DeployContext ctx, CancellationToken ct)
    {
        try
        {
            if (ctx.NewContainerId is null)
            {
                return DeployStageResult.Fail("No new container to finalize");
            }

            if (ctx.OldContainerId is not null)
            {
                // Step 1: Stop the old container.
                await _driver.StopContainerAsync(ctx.OldContainerId, ct);

                // Step 2: Rename old → temp name to free the target name.
                // Record it on ctx so RollbackStage can rename it back if step 3 fails.
                var deploymentId8 = ctx.Request.DeploymentId.ToString("N")[..8];
                var oldTempName = $"{ctx.Request.ContainerName}-old-{deploymentId8}";
                await _driver.RenameContainerAsync(ctx.OldContainerId, oldTempName, ct);
                ctx.RenamedOldContainerName = oldTempName;

                // Step 3: Rename new → target name (now free).
                await _driver.RenameContainerAsync(ctx.NewContainerId, ctx.Request.ContainerName, ct);

                // Step 4: Remove old container (now safe — new is live under target name).
                await _driver.RemoveContainerAsync(ctx.OldContainerId, ct);
            }
            else
            {
                // No old container: just rename new → target name directly.
                await _driver.RenameContainerAsync(ctx.NewContainerId, ctx.Request.ContainerName, ct);
            }

            return DeployStageResult.Ok();
        }
        catch (Exception ex)
        {
            return DeployStageResult.Fail(ex.Message);
        }
    }
}
