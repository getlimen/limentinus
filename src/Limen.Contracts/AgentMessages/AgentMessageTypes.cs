namespace Limen.Contracts.AgentMessages;

public static class AgentMessageTypes
{
    public const string Enroll = "agent/enroll";
    public const string EnrollResponse = "agent/enrollResponse";
    public const string Heartbeat = "agent/heartbeat";
    public const string HeartbeatAck = "agent/heartbeatAck";
    public const string Disconnecting = "agent/disconnecting";
    public const string Deploy = "agent/deploy";
    public const string DeployProgress = "agent/deployProgress";
    public const string DeployResult = "agent/deployResult";
    public const string StopContainer = "agent/stopContainer";
    public const string Rollback = "agent/rollback";
}
