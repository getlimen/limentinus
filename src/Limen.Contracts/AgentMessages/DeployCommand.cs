namespace Limen.Contracts.AgentMessages;

public sealed record DeployCommand(
    Guid DeploymentId,
    Guid ServiceId,
    string Image,
    string ContainerName,
    int InternalPort,
    Dictionary<string, string> Env,
    string[] Volumes,
    HealthCheckSpec HealthCheck,
    string NetworkMode);

public sealed record HealthCheckSpec(
    string? Command,
    int TimeoutSeconds,
    int MaxRetries,
    int IntervalSeconds);
