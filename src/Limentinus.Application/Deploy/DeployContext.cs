using Limen.Contracts.AgentMessages;

namespace Limentinus.Application.Deploy;

public sealed class DeployContext
{
    public DeployCommand Request { get; }
    public string? PulledImageId { get; set; }
    public string? NewContainerId { get; set; }
    public string? OldContainerId { get; set; }
    public string TempContainerName { get; }

    public DeployContext(DeployCommand request)
    {
        Request = request;
        TempContainerName = $"{request.ContainerName}-{request.DeploymentId.ToString("N")[..8]}";
    }
}
