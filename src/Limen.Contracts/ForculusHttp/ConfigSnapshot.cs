namespace Limen.Contracts.ForculusHttp;

public sealed record ConfigSnapshot(
    string ServerPrivateKey,
    int ListenPort,
    string InterfaceAddress,
    IReadOnlyList<PeerSpec> Peers);
