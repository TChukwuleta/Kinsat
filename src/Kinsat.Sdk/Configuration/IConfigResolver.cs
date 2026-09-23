using Kinsat.Sdk.Identity;

namespace Kinsat.Sdk.Configuration;

public interface IConfigResolver
{
    Task<T> Resolve<T>(CallContext context, string key, string? productId = null, T? fallback = default, CancellationToken cancellationToken = default);
    Task Set<T>(CallContext context, ConfigScope scope, string key, T value, string? productId = null, CancellationToken cancellationToken = default);
}
