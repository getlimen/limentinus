using Limen.Contracts.AgentMessages;

namespace Limentinus.Application.Deploy;

public sealed class DeployContext
{
    public DeployCommand Request { get; }
    public string? PulledImageId { get; set; }
    public string? NewContainerId { get; set; }
    public string? OldContainerId { get; set; }
    public string TempContainerName { get; }

    /// <summary>
    /// Set by FinalizeStage after renaming the old container to its temporary name
    /// (step 2 of the finalize sequence). If FinalizeStage fails on step 3,
    /// RollbackStage uses this to rename the old container back to its original name
    /// before restarting it.
    /// </summary>
    public string? RenamedOldContainerName { get; set; }

    public DeployContext(DeployCommand request)
    {
        Request = request;
        TempContainerName = $"{request.ContainerName}-{request.DeploymentId.ToString("N")[..8]}";
    }
}
