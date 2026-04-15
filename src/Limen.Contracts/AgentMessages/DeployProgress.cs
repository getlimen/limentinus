namespace Limen.Contracts.AgentMessages;

public sealed record DeployProgress(
    Guid DeploymentId,
    string Stage,
    string Message,
    int? PercentComplete);
