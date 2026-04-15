using Limen.Contracts.AgentMessages;

namespace Limentinus.Domain.Node;

public sealed record EnrollmentOutcome(NodeIdentity Identity, WireGuardConfig? Wireguard);
