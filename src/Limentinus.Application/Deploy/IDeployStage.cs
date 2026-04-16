namespace Limentinus.Application.Deploy;

public interface IDeployStage
{
    string Name { get; }
    Task<DeployStageResult> ExecuteAsync(DeployContext ctx, CancellationToken ct);
}
