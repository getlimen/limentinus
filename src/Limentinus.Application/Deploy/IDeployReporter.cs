namespace Limentinus.Application.Deploy;

public interface IDeployReporter
{
    Task ReportProgressAsync(Guid deploymentId, string stage, string message, int? percentComplete, CancellationToken ct);
}
