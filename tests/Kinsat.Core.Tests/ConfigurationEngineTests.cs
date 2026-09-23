using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Core.Configuration;
using Kinsat.Sdk.Configuration;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Tests;

public class ConfigurationEngineTests
{
    private static CallContext ContextFor(string tenantId, Role role, string subject = "user") =>
        CallContext.From(new KinsatPrincipal(subject, tenantId, role, subject));

    [Fact]
    public async Task Product_scope_overrides_tenant_and_system()
    {
        var config = new ConfigurationEngine();
        var root = ContextFor("platform", Role.SystemAdmin);
        var admin = ContextFor("mfi-lagos", Role.MerchantAdmin);

        await config.Set(root, ConfigScope.System, "ltv.warning-threshold", 0.70m);
        await config.Set(admin, ConfigScope.Tenant, "ltv.warning-threshold", 0.75m);
        await config.Set(admin, ConfigScope.Product, "ltv.warning-threshold", 0.80m, productId: "starter-loan");

        var resolved = await config.Resolve<decimal>(admin, "ltv.warning-threshold", productId: "starter-loan");

        Assert.Equal(0.80m, resolved);
    }

    [Fact]
    public async Task Tenant_scope_applies_when_product_has_no_override()
    {
        var config = new ConfigurationEngine();
        var admin = ContextFor("mfi-lagos", Role.MerchantAdmin);

        await config.Set(admin, ConfigScope.Tenant, "ltv.warning-threshold", 0.75m);

        var resolved = await config.Resolve<decimal>(admin, "ltv.warning-threshold", productId: "unconfigured-product");

        Assert.Equal(0.75m, resolved);
    }

    [Fact]
    public async Task Loan_officer_cannot_write_tenant_scope_config()
    {
        var config = new ConfigurationEngine();
        var officer = ContextFor("mfi-lagos", Role.LoanOfficer);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            config.Set(officer, ConfigScope.Tenant, "ltv.warning-threshold", 0.99m));
    }

    [Fact]
    public async Task Merchant_admin_cannot_write_system_scope_config()
    {
        var config = new ConfigurationEngine();
        var admin = ContextFor("mfi-lagos", Role.MerchantAdmin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            config.Set(admin, ConfigScope.System, "ltv.warning-threshold", 0.99m));
    }

    [Fact]
    public async Task One_tenants_write_never_leaks_into_another_tenants_resolution()
    {
        var config = new ConfigurationEngine();
        var lagosAdmin = ContextFor("mfi-lagos", Role.MerchantAdmin);
        var abujaAdmin = ContextFor("mfi-abuja", Role.MerchantAdmin);

        await config.Set(lagosAdmin, ConfigScope.Tenant, "ltv.warning-threshold", 0.75m);
        await config.Set(abujaAdmin, ConfigScope.Tenant, "ltv.warning-threshold", 0.65m);

        var lagosResolved = await config.Resolve<decimal>(lagosAdmin, "ltv.warning-threshold", productId: "n/a");
        var abujaResolved = await config.Resolve<decimal>(abujaAdmin, "ltv.warning-threshold", productId: "n/a");

        Assert.Equal(0.75m, lagosResolved);
        Assert.Equal(0.65m, abujaResolved);
    }

    [Fact]
    public async Task Missing_value_without_fallback_throws()
    {
        var config = new ConfigurationEngine();
        var admin = ContextFor("mfi-lagos", Role.MerchantAdmin);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            config.Resolve<decimal>(admin, "never-set-key"));
    }

    [Fact]
    public async Task Missing_value_with_fallback_returns_fallback()
    {
        var config = new ConfigurationEngine();
        var admin = ContextFor("mfi-lagos", Role.MerchantAdmin);

        var resolved = await config.Resolve<decimal>(admin, "never-set-key", fallback: 0.5m);

        Assert.Equal(0.5m, resolved);
    }
}
