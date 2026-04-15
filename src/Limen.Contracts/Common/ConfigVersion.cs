namespace Limen.Contracts.Common;

/// <summary>
/// Monotonic u64 used for deduplication. Limen increments this on every state
/// change that produces an outbound message. Agents/proxies track the last
/// version they applied and reject older ones.
/// </summary>
public readonly record struct ConfigVersion(ulong Value) : IComparable<ConfigVersion>
{
    public static ConfigVersion Zero => new(0);
    public ConfigVersion Next() => new(Value + 1);
    public int CompareTo(ConfigVersion other) => Value.CompareTo(other.Value);
    public static bool operator <(ConfigVersion a, ConfigVersion b) => a.Value < b.Value;
    public static bool operator >(ConfigVersion a, ConfigVersion b) => a.Value > b.Value;
    public static bool operator <=(ConfigVersion a, ConfigVersion b) => a.Value <= b.Value;
    public static bool operator >=(ConfigVersion a, ConfigVersion b) => a.Value >= b.Value;
    public override string ToString() => Value.ToString();
}
