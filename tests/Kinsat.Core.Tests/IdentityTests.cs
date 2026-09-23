using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Core.Identity;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Tests;

public class IdentityTests
{
    private static StandaloneIdentityProvider NewProviderWithUser(string tenantId, string subject, Role role)
    {
        var provider = new StandaloneIdentityProvider();
        provider.Register(tenantId, subject, "s3cret", role, subject);
        return provider;
    }

    [Fact]
    public async Task Wrong_credential_is_rejected()
    {
        var provider = NewProviderWithUser("mfi-lagos", "officer.ada", Role.LoanOfficer);

        var result = await provider.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "wrong-secret"));

        Assert.False(result.Succeeded);
        Assert.Null(result.Principal);
    }

    [Fact]
    public async Task Correct_credential_authenticates_and_scopes_to_tenant()
    {
        var provider = NewProviderWithUser("mfi-lagos", "officer.ada", Role.LoanOfficer);

        var result = await provider.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "s3cret"));
        var context = CallContext.From(result.Principal!);

        Assert.True(result.Succeeded);
        Assert.Equal("mfi-lagos", context.TenantId);
        Assert.False(context.CanAccessTenant("mfi-abuja"));
    }

    [Fact]
    public async Task Issued_token_revalidates_into_a_fresh_context()
    {
        var provider = NewProviderWithUser("mfi-lagos", "officer.ada", Role.LoanOfficer);
        var authResult = await provider.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "s3cret"));

        var revalidated = await provider.Validate("mfi-lagos", authResult.Token!);

        Assert.NotNull(revalidated);
        Assert.Equal("officer.ada", revalidated!.Principal.Subject);
    }

    [Fact]
    public async Task Token_does_not_revalidate_against_a_different_tenant()
    {
        var provider = NewProviderWithUser("mfi-lagos", "officer.ada", Role.LoanOfficer);
        var authResult = await provider.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "s3cret"));

        var revalidated = await provider.Validate("mfi-abuja", authResult.Token!);

        Assert.Null(revalidated);
    }

    [Fact]
    public void SystemAdmin_context_can_access_any_tenant()
    {
        var principal = new KinsatPrincipal("root.tobe", "platform", Role.SystemAdmin, "Tobe");
        var context = CallContext.From(principal);

        Assert.True(context.CanAccessTenant("mfi-lagos"));
        Assert.True(context.CanAccessTenant("mfi-abuja"));
    }
}
