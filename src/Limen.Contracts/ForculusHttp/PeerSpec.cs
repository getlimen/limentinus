namespace Limen.Contracts.ForculusHttp;

public sealed record PeerSpec(string PublicKey, string AllowedIps, string? PresharedKey = null);
