using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Kinsat.Core.Security;
using Kinsat.Sdk.Configuration;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Configuration;

public sealed class ConfigurationEngine : IConfigResolver
{
    private readonly ConcurrentDictionary<string, object?> _system = new();
    private readonly ConcurrentDictionary<string, object?> _tenant = new();
    private readonly ConcurrentDictionary<string, object?> _product = new();

    public Task<T> Resolve<T>(CallContext context, string key, string? productId = null, T? fallback = default, CancellationToken cancellationToken = default)
    {
        Guard.RequireTenantAccess(context, context.TenantId);

        if (productId is not null && _product.TryGetValue(ProductKey(context.TenantId, productId, key), out var productValue) && productValue is T typedProduct)
            return Task.FromResult(typedProduct);

        if (_tenant.TryGetValue(TenantKey(context.TenantId, key), out var tenantValue) && tenantValue is T typedTenant)
            return Task.FromResult(typedTenant);

        if (_system.TryGetValue(key, out var systemValue) && systemValue is T typedSystem)
            return Task.FromResult(typedSystem);

        if (fallback is not null)
            return Task.FromResult(fallback);

        throw new KeyNotFoundException($"No configuration found for '{key}' at product, tenant, or system scope, and no fallback was supplied.");
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
