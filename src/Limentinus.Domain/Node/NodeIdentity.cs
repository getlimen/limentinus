namespace Limentinus.Domain.Node;

public sealed class NodeIdentity
{
    public Guid AgentId { get; set; }
    public string Secret { get; set; } = string.Empty;
}
