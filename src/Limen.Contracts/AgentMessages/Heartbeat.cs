namespace Limen.Contracts.AgentMessages;

public sealed record Heartbeat(DateTimeOffset Timestamp, string[] ActiveRoles);
public sealed record HeartbeatAck(ulong ServerVersion);
