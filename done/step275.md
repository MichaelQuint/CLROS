# Step 275 - COM1-Derived Owned Serial Readiness

## Summary

Added a host-tested readiness contract that derives OS-owned serial diagnostics readiness from existing COM1 port-I/O backend prerequisites. The guard now has a COM1-derived mark path that only marks owned serial ready after both backend planning and backend address resolution succeeded.

## Why

Gitea issue #275 needed the post-firmware diagnostics guard to stop treating owned serial readiness as an unconditional manual toggle. This slice keeps the boundary status-coded while preserving the current evidence limit: no port I/O, no function-pointer creation, no executable stub invocation, and no QEMU/live serial proof.

## Files changed

- `src/OS.Kernel/Diagnostics/OwnedSerialDiagnosticsReadiness.cs`
- `src/OS.Kernel/Diagnostics/SerialDiagnosticsGuard.cs`
- `tests/OS.HostTests/OwnedSerialDiagnosticsReadinessTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `done/step275.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence

Focused host-test build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The host-test harness emitted the required #275 markers:

```text
TEST_PASS diagnostics owned serial readiness accepts com1 backend contract
TEST_PASS diagnostics owned serial readiness rejects failed backend plan
TEST_PASS diagnostics owned serial readiness rejects failed backend contract
TEST_PASS diagnostics owned serial readiness rejects late firmware unavailable
TEST_PASS host.tests
HOST_TEST_EXIT 0
```

## Failures

None observed in this slice.

## Deferred work

- No COM1 port I/O backend was installed.
- No function pointers or unmanaged delegates were created.
- No generated stubs were executed.
- No QEMU or live post-firmware serial evidence was produced.
