
using Kinsat.Core.Configuration;
using Kinsat.Core.Events;
using Kinsat.Core.Identity;
using Kinsat.Sdk.Configuration;
using Kinsat.Sdk.Events;
using Kinsat.Sdk.Identity;

var failures = new List<string>();

void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
    if (!condition) failures.Add(name);
}

// --- Identity: default standalone provider stands in for whatever a merchant would plug in ---
var identity = new StandaloneIdentityProvider();
identity.Register("mfi-lagos", "officer.ada", "s3cret", Role.LoanOfficer, "Ada, Loan Officer");
identity.Register("mfi-lagos", "admin.bola", "s3cret", Role.MerchantAdmin, "Bola, Merchant Admin");
identity.Register("mfi-abuja", "admin.chidi", "s3cret", Role.MerchantAdmin, "Chidi, Merchant Admin");
identity.Register("platform", "root.tobe", "s3cret", Role.SystemAdmin, "Tobe, System Admin");

var rejected = await identity.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "wrong-secret"));
Check("wrong credential is rejected", !rejected.Succeeded);

var officerAuth = await identity.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "s3cret"));
Check("correct credential authenticates", officerAuth.Succeeded && officerAuth.Principal is not null);

var officerContext = CallContext.From(officerAuth.Principal!);
Check("resulting context is scoped to the right tenant", officerContext.TenantId == "mfi-lagos");
Check("a tenant user cannot access another tenant", !officerContext.CanAccessTenant("mfi-abuja"));

var revalidated = await identity.Validate("mfi-lagos", officerAuth.Token!);
Check("an issued token re-validates into a fresh context", revalidated is not null);

// --- Config: tiered resolution, gated entirely by CallContext ---
var config = new ConfigurationEngine();

var adminAuth = await identity.Authenticate(new AuthenticationRequest("mfi-lagos", "admin.bola", "password", "s3cret"));
var adminContext = CallContext.From(adminAuth.Principal!);

var rootAuth = await identity.Authenticate(new AuthenticationRequest("platform", "root.tobe", "password", "s3cret"));
var rootContext = CallContext.From(rootAuth.Principal!);

var deniedSystemWrite = false;
try
{
    // A merchant admin, however senior within their own tenant, still cannot set platform-wide defaults.
    await config.Set(adminContext, ConfigScope.System, "ltv.warning-threshold", 0.99m);
}
catch (UnauthorizedAccessException)
{
    deniedSystemWrite = true;
}
Check("a merchant admin cannot set system-scope configuration", deniedSystemWrite);

await config.Set(rootContext, ConfigScope.System, "ltv.warning-threshold", 0.70m);
await config.Set(adminContext, ConfigScope.Tenant, "ltv.warning-threshold", 0.75m);
await config.Set(adminContext, ConfigScope.Product, "ltv.warning-threshold", 0.80m, productId: "starter-loan");

var productLevel = await config.Resolve<decimal>(adminContext, "ltv.warning-threshold", productId: "starter-loan");
Check("product scope overrides tenant and system", productLevel == 0.80m);

var tenantFallback = await config.Resolve<decimal>(adminContext, "ltv.warning-threshold", productId: "unconfigured-product");
Check("tenant scope is used when the product has no override", tenantFallback == 0.75m);

var sameTenantOfficerRead = await config.Resolve<decimal>(officerContext, "ltv.warning-threshold");
Check("a same-tenant caller resolves the tenant override too", sameTenantOfficerRead == 0.75m);

var deniedWrite = false;
try
{
    await config.Set(officerContext, ConfigScope.Tenant, "ltv.warning-threshold", 0.99m);
}
catch (UnauthorizedAccessException)
{
    deniedWrite = true;
}
Check("a loan officer cannot change tenant-level configuration", deniedWrite);

var deniedCrossTenantWrite = false;
try
{
    var abujaAuth = await identity.Authenticate(new AuthenticationRequest("mfi-abuja", "admin.chidi", "password", "s3cret"));
    var abujaContext = CallContext.From(abujaAuth.Principal!);
    // Abuja's own admin acting on their own tenant is fine...
    await config.Set(abujaContext, ConfigScope.Tenant, "ltv.warning-threshold", 0.65m);
    var abujaLevel = await config.Resolve<decimal>(abujaContext, "ltv.warning-threshold");
    Check("a different tenant's admin can set and read only their own tenant's config", abujaLevel == 0.65m);

    // ...and Lagos's tenant value is untouched by it
    var lagosStillIntact = await config.Resolve<decimal>(adminContext, "ltv.warning-threshold", productId: "unconfigured-product");
    Check("one tenant's config write never leaks into another tenant's resolution", lagosStillIntact == 0.75m);
}
catch
{
    deniedCrossTenantWrite = true;
}
Check("a tenant admin acting only within their own tenant is never blocked", !deniedCrossTenantWrite);

// --- Events: the SDK's event bus, the surface extensions subscribe to ---
var bus = new InProcessEventBus();
var received = new List<string>();
bus.Subscribe<LtvBreached>((e, _) =>
{
    received.Add($"LTV breach on {e.LoanId}: {e.Ltv:P0} ({e.Tier})");
    return Task.CompletedTask;
});

await bus.Publish(new LtvBreached("mfi-lagos", DateTimeOffset.UtcNow, "loan-001", 0.82m, "margin-call-tier-2"));
Check("event bus delivers a published event to its subscriber", received.Count == 1);

Console.WriteLine();
if (failures.Count == 0)
{
    Console.WriteLine("All smoke checks passed.");
    return 0;
}

Console.WriteLine($"{failures.Count} check(s) failed: {string.Join(", ", failures)}");
return 1;
