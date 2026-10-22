using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Kinsat.Core.Security;
using Kinsat.Sdk.Configuration;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Configuration;
/// <summary>
/// Default, in-memory implementation of the tiered config resolver: product scope
/// overrides tenant scope overrides system scope. A production deployment swaps the
/// storage backing (a database, a config service) behind this same IConfigResolver
/// contract; the resolution order and the tenant-isolation guard here are the actual
/// behaviour every deployment must honour, not just this in-memory default.
/// </summary>
public sealed class ConfigurationEngine : IConfigResolver
{
    private readonly ConcurrentDictionary<string, object?> _system = new();
    private readonly ConcurrentDictionary<string, object?> _tenant = new();
    private readonly ConcurrentDictionary<string, object?> _product = new();

    public Task<T> Resolve<T>(CallContext context, string key, string? productId = null, CancellationToken cancellationToken = default)
    {
        if (TryResolve<T>(context, key, productId, out var value))
            return Task.FromResult(value);

        throw new KeyNotFoundException($"No configuration found for '{key}' at product, tenant, or system scope.");
    }

    public Task<T> ResolveOr<T>(CallContext context, string key, T defaultValue, string? productId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(TryResolve<T>(context, key, productId, out var value) ? value : defaultValue);

    private bool TryResolve<T>(CallContext context, string key, string? productId, out T value)
    {
        Guard.RequireTenantAccess(context, context.TenantId);

        if (productId is not null && _product.TryGetValue(ProductKey(context.TenantId, productId, key), out var p) && p is T tp)
        { value = tp; return true; }

        if (_tenant.TryGetValue(TenantKey(context.TenantId, key), out var t) && t is T tt)
        { value = tt; return true; }

        if (_system.TryGetValue(key, out var sy) && sy is T ts)
        { value = ts; return true; }

        value = default!;
        return false;
    }

    public Task Set<T>(CallContext context, ConfigScope scope, string key, T value, string? productId = null, CancellationToken cancellationToken = default)
    {
        switch (scope)
        {
            case ConfigScope.System:
                Guard.RequireRole(context, Role.SystemAdmin);
                _system[key] = value;
                break;

            case ConfigScope.Tenant:
                Guard.RequireTenantAccess(context, context.TenantId);
                Guard.RequireRole(context, Role.MerchantAdmin, Role.SystemAdmin);
                _tenant[TenantKey(context.TenantId, key)] = value;
                break;

            case ConfigScope.Product:
                if (productId is null)
                    throw new ArgumentException("productId is required when setting product-scoped configuration.", nameof(productId));
                Guard.RequireTenantAccess(context, context.TenantId);
                Guard.RequireRole(context, Role.MerchantAdmin, Role.SystemAdmin);
                _product[ProductKey(context.TenantId, productId, key)] = value;
                break;
        }

        return Task.CompletedTask;
    }

    private static string TenantKey(string tenantId, string key) => $"{tenantId}:{key}";
    private static string ProductKey(string tenantId, string productId, string key) => $"{tenantId}:{productId}:{key}";
}
