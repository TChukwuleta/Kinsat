using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Kinsat.Core.Identity;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Tests;

public class CallContextGateTests
{
    [Fact]
    public void CallContext_has_no_public_constructor()
    {
        var publicCtors = typeof(CallContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(publicCtors);
    }

    [Fact]
    public void CallContext_has_no_public_static_factory()
    {
        var factories = typeof(CallContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(CallContext));
        Assert.Empty(factories);
    }

    [Fact]
    public void System_actor_only_ever_holds_the_automated_system_role()
    {
        var context = SystemActor.ForTenant("mfi-lagos");

        Assert.Equal(Role.AutomatedSystem, context.Role);
        Assert.False(context.CanAccessTenant("mfi-abuja"));
    }
}
