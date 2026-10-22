using Kinsat.Sdk.Identity;

namespace Kinsat.Sdk.Configuration;

/// <summary>
/// Resolves a configuration value at the most specific scope available: product overrides
/// tenant overrides system default. Every behavioural parameter in Kinsat (LTV thresholds,
/// margin call ladders, disbursement currency, and so on) is read through this, never
/// hardcoded. Resolution always takes a CallContext, so a tenant can never read or write
/// another tenant's configuration.
/// </summary>
public interface IConfigResolver
{
    /// <summary>Resolves a value, throwing KeyNotFoundException if no scope defines it.
    /// Missing configuration is never silently treated as zero or default.</summary>
    Task<T> Resolve<T>(CallContext context, string key, string? productId = null, CancellationToken cancellationToken = default);

    /// <summary>Resolves a value, returning the explicitly supplied default when no scope defines it.</summary>
    Task<T> ResolveOr<T>(CallContext context, string key, T defaultValue, string? productId = null, CancellationToken cancellationToken = default);

    Task Set<T>(CallContext context, ConfigScope scope, string key, T value, string? productId = null, CancellationToken cancellationToken = default);
}
