# Step 826 - M00-002 Canonical Solution Sentinel

## Summary

Changed the shared host-test repository locator to require `CLROS.slnx`.

## Why

The old `OS.slnx` sentinel was rename fallout and aborted repository-wide tests.

## Files changed

- `tests/OS.HostTests/HostToolProcess.cs`
- `docs/repo-audit.md`
- `docs/host-requirements.md`

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Root discovery resolves the repository containing `CLROS.slnx` and rejects the
legacy sentinel. The integrated result is recorded in Step 845.

## Failures

The original harness stopped when it could not find `OS.slnx`; that failure is
now an explicit regression case.

## Deferred work

None within repository discovery.

## Evidence boundary

This proves host-tool root discovery only; it does not rename retained `OS.*`
technical identities.
