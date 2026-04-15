using Limentinus.Application.Common.Interfaces;

namespace Limentinus.Application.Deploy.Stages;

public sealed class StartNewStage : IDeployStage
{
    private readonly IDockerDriver _driver;

    public StartNewStage(IDockerDriver driver) => _driver = driver;

    public string Name => "StartNew";

    public async Task<DeployStageResult> ExecuteAsync(DeployContext ctx, CancellationToken ct)
    {
        try
        {
            var req = ctx.Request;
            var spec = new StartContainerSpec(
                Image: req.Image,
                Name: ctx.TempContainerName,
                InternalPort: req.InternalPort,
                Env: req.Env,
                Volumes: req.Volumes,
                NetworkMode: req.NetworkMode);

            ctx.NewContainerId = await _driver.StartContainerAsync(spec, ct);
            return DeployStageResult.Ok();
        }
        catch (Exception ex)
        {
            return DeployStageResult.Fail(ex.Message);
        }
    }
}
