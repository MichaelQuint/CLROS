# Step 830 - M00-006 Firewall and Profile Test Locators

## Summary

Migrated source-firewall and workload-profile tests to the shared CLROS locator.

## Why

The identity guard must fail closed when `CLROS.slnx` is absent instead of
silently succeeding against no solution.

## Files changed

- `tests/OS.HostTests/SourceFirewallTests.cs`
- `tests/OS.HostTests/WorkloadProfileTests.cs`
- `docs/repo-audit.md`
- `docs/workload-profiles.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The guard emits `TEST_PASS naming.guard` and `TEST_PASS source.firewall`; the
integrated baseline is in Step 845.

## Failures

A permission-ledger sentence initially resembled a product-identity claim for
the reference. It was clarified as reference-source reuse and retained as a
regression.

## Deferred work

None within root discovery.

## Evidence boundary

This proves fail-closed identity and profile test location, not workload boot.
