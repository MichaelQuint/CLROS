# Step 838 - M00-014 First-Party Source Firewall

## Summary

Added a fail-closed firewall against importing reference projects through
solutions, projects, props, or targets.

## Why

Reference study is allowed, but first-party build isolation must be mechanical.

## Files changed

- `scripts/check-naming.ps1`
- `tests/OS.HostTests/SourceFirewallTests.cs`
- `docs/repo-audit.md`
- `docs/reference-map.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Positive and negative fixtures cover `.slnx`, `.csproj`, `.props`, `.targets`,
relative paths, slash/case variants, missing sentinel, and documentation
false-positive resistance.

## Failures

The old guard could report green while the expected solution was absent.

## Deferred work

The firewall does not replace provenance or semantic review.

## Evidence boundary

This proves build-graph isolation for current checked-in project metadata.
