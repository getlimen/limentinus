using Limentinus.Application.Common.Interfaces;

namespace Limentinus.Application.Deploy.Stages;

public sealed class CaptureOldStage : IDeployStage
{
    private readonly IDockerDriver _driver;

    public CaptureOldStage(IDockerDriver driver) => _driver = driver;

    public string Name => "CaptureOld";

    public async Task<DeployStageResult> ExecuteAsync(DeployContext ctx, CancellationToken ct)
    {
        ctx.OldContainerId = await _driver.FindContainerIdByNameAsync(ctx.Request.ContainerName, ct);
        return DeployStageResult.Ok();
    }
}
