# Step 416 - Owned COM1 diagnostics readiness aggregate

## Summary

Added a host-tested owned COM1 diagnostics readiness composer that aggregates
the existing COM1 backend plan, backend address contract, owned-serial
readiness derivation, serial diagnostics guard transition, and post-firmware
diagnostics marker boundary into one value-type result.

## Why

Issue #401 needed a small readiness slice before any live port-I/O serial path
exists. The new aggregate keeps the post-firmware diagnostics gate explicit and
preserves nested status metadata without configuring COM1, invoking generated
stubs, calling firmware, or claiming live post-firmware output.

## Files changed

- `src/OS.Kernel/Diagnostics/OwnedCom1DiagnosticsReadiness.cs`
- `tests/OS.HostTests/OwnedCom1DiagnosticsReadinessTests.cs`
- `docs/diagnostics.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step416.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`

## Evidence

- Host test build passed with 0 warnings and 0 errors.
- Host test run ended with `TEST_PASS host.tests`.
- Full solution build passed with 0 warnings and 0 errors.
- New host markers:
  - `TEST_PASS diagnostics.owned com1 readiness composes`
  - `TEST_PASS diagnostics.owned com1 readiness rejects backend plan`
  - `TEST_PASS diagnostics.owned com1 readiness rejects backend contract`
  - `TEST_PASS diagnostics.owned com1 readiness emits post firmware marker metadata`

## Failures

- None.

## Deferred work

- This is host-tested metadata only. It does not configure COM1, invoke
  generated port-I/O stubs, call real `ExitBootServices`, shut down firmware
  services, or prove live serial output after firmware shutdown.

