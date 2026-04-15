namespace Limen.Contracts.ProxyMessages;

public sealed record RouteSpec(
    Guid RouteId,
    string Hostname,
    string UpstreamUrl,
    bool TlsEnabled,
    string AuthPolicy);

public sealed record ApplyRouteSet(IReadOnlyList<RouteSpec> Routes);

public sealed record RouteSetAck(ulong AppliedVersion, int RouteCount);

public sealed record ProxyAuth(string ProxyNodeId, string Secret);

public sealed record ProxyAuthResponse(bool Ok, string? Reason);
