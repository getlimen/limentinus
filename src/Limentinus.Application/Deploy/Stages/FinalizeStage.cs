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
                await _driver.StopContainerAsync(ctx.OldContainerId, ct);
                await _driver.RemoveContainerAsync(ctx.OldContainerId, ct);
            }

            await _driver.RenameContainerAsync(ctx.NewContainerId, ctx.Request.ContainerName, ct);
            return DeployStageResult.Ok();
        }
        catch (Exception ex)
        {
            return DeployStageResult.Fail(ex.Message);
        }
    }
}
