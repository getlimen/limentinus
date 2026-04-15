namespace Limen.Contracts.Common;

/// <summary>
/// Universal wrapper around every control-plane message. Carries the message
/// type discriminator, an optional config version for idempotency/dedup, and
/// the serialized payload.
/// </summary>
public sealed record Envelope<T>(
    string Type,
    ConfigVersion Version,
    T Payload);
