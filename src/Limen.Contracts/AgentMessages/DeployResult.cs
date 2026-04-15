namespace Limen.Contracts.AgentMessages;

public sealed record DeployResult(
    Guid DeploymentId,
    bool Success,
    string? RolledBackReason);
