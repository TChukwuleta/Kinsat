# Roadmap

Tracks against the functional requirements in the BRD (`KINSAT_BRD.pdf`, Section 8).
Checked items have a real implementation and test coverage; unchecked items are Sdk
contracts only, or not started.

## Foundation

- [x] Repo scaffold, solution structure
- [x] `IIdentityProvider` contract + `StandaloneIdentityProvider` default + tests
- [x] `IConfigResolver` contract + `ConfigurationEngine` (tiered, guarded) + tests
- [x] `IEventBus` contract + domain events + `InProcessEventBus` + tests
- [x] `Security.Guard` — tenant isolation and role enforcement

## Custody (FR-4.x)

- [x] `ICustodyProvider` contract
- [ ] BTCPay Server default implementation
- [ ] Confirmation-depth gating on deposit recognition
- [ ] Tests: deposit address generation, balance, release, liquidation transfer

## Scoring (FR-3.x)

- [x] `IScoringSignalProvider` contract
- [ ] Core's own Bitcoin credit score (on-chain + repayment history) as default provider
- [ ] Scoring aggregator combining weighted signal providers
- [ ] Tests: aggregation weighting, provider registration

## Loan lifecycle (FR-2.x, FR-5.x through FR-7.x)

- [ ] Loan application → underwriting routing (auto-approve/reject/manual review)
- [ ] Disbursement, publishing `LoanOriginated`
- [ ] LTV monitoring against a price oracle, publishing `LtvBreached`
- [ ] Margin call issuance and cure window, publishing `MarginCallTriggered`
- [ ] Liquidation execution, publishing `LiquidationExecuted`
- [ ] Repayment processing, publishing `RepaymentReceived`
- [ ] Closure and credit score update, publishing `CreditScoreUpdated`
- [ ] End-to-end integration test: authentication through finalization

## Once the above exists

- [ ] Swap `ConfigurationEngine`'s in-memory store for a real persistence backend
- [ ] API layer (borrower-facing, staff-facing) authenticating through `IIdentityProvider`
- [ ] First merchant extension, built against the Sdk as it stands at that point
- [ ] Publish the Sdk publicly for third-party extension authors (see the versioning and
  compatibility-suite discussion — do this only after the first extension has exercised
  the contracts for real)
