namespace Limentinus.Application.Deploy;

public interface IRollbackStage
{
    Task ExecuteAsync(DeployContext ctx, CancellationToken ct);
}
