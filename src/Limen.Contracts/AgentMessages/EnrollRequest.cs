namespace Limen.Contracts.AgentMessages;

public sealed record EnrollRequest(
    string ProvisioningKey,
    string Hostname,
    string[] Roles,
    string Platform,
    string AgentVersion);

public sealed record WireGuardConfig(
    string InterfaceAddress,   // "10.42.0.17/32"
    string PrivateKey,         // base64 Curve25519
    string ServerPublicKey,    // base64 Curve25519 (Forculus public key)
    string ServerEndpoint,     // "203.0.113.42:51820"
    int KeepaliveSeconds);

public sealed record EnrollResponse(
    Guid AgentId,
    string PermanentSecret,
    WireGuardConfig Wireguard);
