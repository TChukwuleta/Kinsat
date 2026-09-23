# Contributing to Kinsat

## Repo layout

- `src/Kinsat.Sdk` — open source contracts. If you're implementing a custodian, a scoring
  signal, an identity backend, or subscribing to the event bus, this is what you build
  against. Changes here are the highest-impact, since anything implementing these
  contracts depends on them.
- `src/Kinsat.Core` — open source default implementations of every Sdk contract, plus
  `Security/Guard.cs`, which is not an extension point.
- `tests/Kinsat.Core.Tests` — the real test suite (xUnit). Every new feature in
  `Kinsat.Core` needs tests here before merge.
- `tests/Kinsat.Core.Smoketest` — a dependency-free console check that runs without
  network access, useful for offline or restricted build environments.

## What goes where: the triage rule

When you're adding a feature, ask: would any lender using Kinsat want this, or is it
specific to one institution's rules, integrations, or jurisdiction?

- Generically useful → belongs in `Kinsat.Core` (or a new interface in `Kinsat.Sdk` if it
  needs to be pluggable).
- Institution-specific → belongs in a merchant extension, a separate closed source
  package built against the Sdk, not in this repo.

If you're not sure which side a change falls on, open an issue describing the behaviour
before writing code — it's much cheaper to redirect a proposal than a pull request.

## Before you open a PR

```bash
dotnet build
dotnet test tests/Kinsat.Core.Tests/Kinsat.Core.Tests.csproj
dotnet run --project tests/Kinsat.Core.Smoketest
```

All three should pass. If you're adding a new provider interface to `Kinsat.Sdk`, treat
it as a public contract from the moment it merges: once something could plausibly be
built against it, renaming or restructuring it later is a breaking change for every
extension author, not just this repo.

## Style

- XML doc comments on every public interface and non-trivial public type, explaining
  intent (why the contract exists, what it's for) rather than restating the signature.
- Every operation that touches tenant data takes a `CallContext` and goes through
  `Kinsat.Core.Security.Guard`. There is no back door around this, including for new
  features — if something feels like it needs one, that's a sign the `Role` enum or the
  guard logic needs to grow, not that the check should be skipped.
