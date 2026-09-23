# Kinsat

Bitcoin-backed micro-loan engine for microfinance institutions. Open source core, closed
source merchant extensions built against a published SDK. See `KINSAT_BRD.pdf` for the
full business requirements and `ROADMAP.md` for what's built versus what's next.

License: MIT (see `LICENSE`). Contributing: see `CONTRIBUTING.md`.

## Solution layout

```
Kinsat.sln
src/
  Kinsat.Sdk/      Open source contracts extensions build against: identity, custody,
                    scoring, the event bus, and tiered configuration. No implementation
                    here, only interfaces and the shapes that cross them.
  Kinsat.Core/      Open source default implementations of every Sdk contract, plus the
                    one piece that is NOT an extension point: Security/Guard.cs, the
                    tenant-isolation and role check every core operation runs through.
tests/
  Kinsat.Core.Tests/       The real test suite (xUnit): identity, configuration engine,
                            and event bus, one test class per Kinsat.Core feature, plus
                            PendingCoverage.cs, which deliberately skips a test per
                            unbuilt feature (custody, scoring, loan lifecycle, end-to-end)
                            so missing coverage shows up in CI output instead of being
                            silently absent.
  Kinsat.Core.Smoketest/    A dependency-free console app covering the same ground as
                            Kinsat.Core.Tests, for environments without NuGet access
                            (this repo was originally built in one — see note below).
```

## What's here so far

This is the repo scaffold plus the config engine foundation, the starting point we agreed
on, with authentication folded in because it touches the same contracts:

- **Identity is a provider interface (`IIdentityProvider`)**, not a hardcoded auth
  mechanism. `Kinsat.Core.Identity.StandaloneIdentityProvider` is the default,
  self-hosting implementation. A merchant extension implements the same interface against
  their own SSO, core banking IAM, or KYC vendor's session system, and nothing else in
  core has to change.
- **Every core operation is gated by `CallContext`**, which has no public constructor.
  The only way to get one is through `IIdentityProvider.AuthenticateAsync` or
  `ValidateAsync` succeeding. This makes "every call carries a validated identity and
  tenant scope" a compile-time guarantee rather than a rule extension authors have to
  remember.
- **`ConfigurationEngine` resolves product scope over tenant scope over system scope**,
  and every read or write runs through `Security.Guard`, so one tenant's configuration is
  structurally unreachable from another tenant's context, and only a `SystemAdmin` can
  set platform-wide defaults.
- **`InProcessEventBus`** is the default `IEventBus`: `LoanOriginated`,
  `CollateralDeposited`, `LtvBreached`, `MarginCallTriggered`, `LiquidationExecuted`,
  `RepaymentReceived`, `CreditScoreUpdated` are all defined in `Kinsat.Sdk.Events` and
  ready for the loan engine to publish and extensions to subscribe to.
- **Custody (`ICustodyProvider`) and scoring (`IScoringSignalProvider`) contracts are
  defined** but not yet implemented — BTCPay Server as the default custody
  implementation, and the scoring aggregator, are the natural next pieces.

## Running it

```bash
dotnet build
dotnet test tests/Kinsat.Core.Tests/Kinsat.Core.Tests.csproj
dotnet run --project tests/Kinsat.Core.Smoketest
```

The xUnit suite should show passing tests for identity, configuration, and the event bus,
plus explicitly skipped tests naming each unbuilt feature. The smoke test should print all
`PASS` and exit 0.

## Note on this sandbox

`Kinsat.Core.Tests` was written and reviewed here but not compiled here: this build
environment's network allowlist doesn't reach nuget.org, so `Microsoft.NET.Test.Sdk` and
`xunit` couldn't be restored to verify it. `Kinsat.Sdk`, `Kinsat.Core`, and
`Kinsat.Core.Smoketest` have no external package dependencies, so those were built and run
here and are confirmed working. Run `dotnet test` on a machine with normal internet
access, or let CI do it (`.github/workflows/ci.yml` runs both suites on every push and PR)
before merging anything that touches `Kinsat.Core.Tests`.

## Next steps

1. `ICustodyProvider` default implementation against BTCPay Server's Greenfield API.
2. Scoring aggregator in `Kinsat.Core` combining `IScoringSignalProvider` contributions
   by weight, with the core's own on-chain/repayment score registered as the always-on
   default provider.
3. The loan lifecycle state machine itself, publishing the domain events already defined
   in `Kinsat.Sdk.Events` at each transition.
4. Swap `ConfigurationEngine`'s in-memory dictionaries for a real persistence backend
   once there's a storage decision.
