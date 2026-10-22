using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Identity;

public static class SystemActor
{
    public static CallContext ForTenant(string tenantId, string displayName = "Automated System") =>
        CallContext.From(new KinsatPrincipal("system", tenantId, Role.AutomatedSystem, displayName));
}