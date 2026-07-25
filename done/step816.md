# Step 816 - Issue 775 HTTP Static TCP Response QEMU Preset

## Summary
Added the opt-in `http-static-tcp-response` QEMU marker preset and CI dry-run
lane for future tiny HTTP static TCP response smoke images.

## Why
Issue #775 needed marker-contract tooling that can require
`HTTP_STATIC_TCP_RESPONSE_READY` and `HTTP_STATIC_TCP_RESPONSE` without changing
default QEMU smoke behavior or claiming live HTTP server behavior.

## Files changed
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step816.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset http-static-tcp-response`
- `.\scripts\run-ci-smoke.ps1 -DryRun`
- Scoped `git diff --check` over the issue #775 files.
- Scoped trailing-whitespace scan over the issue #775 files.

## Evidence
- Build log: `artifacts/logs/issue775-host-build-r2.log`
- Host-test log: `artifacts/logs/issue775-host-tests.log`
- QEMU dry-run log: `artifacts/logs/issue775-qemu-dryrun.log`
- CI smoke dry-run log: `artifacts/logs/issue775-ci-smoke-dryrun.log`
- Scoped diff-check log: `artifacts/logs/issue775-diff-check.log`
- Scoped trailing-whitespace log: `artifacts/logs/issue775-trailing-whitespace.log`
- Required markers observed:
  - `TEST_PASS qemu.smoke.preset http static tcp response includes marker requirements`
  - `TEST_PASS qemu.smoke.preset http static tcp response accepts aliases`
  - `TEST_PASS ci.smoke.dryrun includes http static tcp response marker preset`

## Failures
None in the saved #775 verification logs.

## Deferred work
Real guest support remains out of scope. This step does not add or claim live
HTTP server behavior, sockets, hosted .NET, ASP.NET Minimal API, Kestrel, cloud
readiness, or production readiness.

## Evidence boundary
This is marker-contract tooling only. The preset and CI lane prove required
marker wiring and dry-run artifact shape only; boot proof still requires a real
guest image that emits the required serial markers on the tested boot path.

