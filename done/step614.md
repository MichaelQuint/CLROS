# Step 614 - CPU Fault Panic Readiness Boundary

## Summary

Added a host-tested CPU fault panic readiness contract that decodes
caller-owned fault-entry frame bytes and flushes a deterministic
`OS_SMOKE_FAIL` register dump through the existing panic serial reporter. Added
an opt-in `BootSequence` marker helper for the same readiness boundary.

## Why

The diagnostics path already had separate contracts for future CPU fault-entry
frame storage and panic-safe serial formatting. This step composes those
contracts so a future live handler has a host-tested boundary from captured
frame bytes to panic text before any IDT, stub execution, or QEMU fault probe is
enabled.

## Files changed

- `src/OS.Kernel/Diagnostics/CpuFaultPanicReadiness.cs`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `tests/OS.HostTests/CpuFaultPanicReadinessTests.cs`
- `docs/diagnostics.md`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/completion-gates.md`
- `done/step614.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|diagnostics\.cpu_fault_panic|boot\.sequence cpu fault panic|host\.tests'`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe *> .\artifacts\logs\host-tests-step614.log`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

The host-test project build passed with 0 warnings and 0 errors.

Focused host-test output and `artifacts/logs/host-tests-step614.log` emitted:

```text
TEST_PASS diagnostics.cpu_fault_panic readiness emits os smoke fail
TEST_PASS diagnostics.cpu_fault_panic readiness rejects invalid frame
TEST_PASS diagnostics.cpu_fault_panic readiness reports reporter failure
TEST_PASS diagnostics.cpu_fault_panic readiness default result is not success
TEST_PASS diagnostics.cpu_fault_panic readiness result contains no managed references
TEST_PASS boot.sequence cpu fault panic readiness emits ready marker
TEST_PASS boot.sequence cpu fault panic readiness emits blocked marker
TEST_PASS boot.sequence cpu fault panic readiness preserves default markers
TEST_PASS host.tests
```

`scripts/check-naming.ps1` emitted:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

`git diff --check` passed with line-ending normalization warnings only.

## Failures

None in this slice.

## Deferred Work

- No live IDT, GDT, TSS, or IST setup is installed.
- No CPU fault handler captures registers.
- No CR2/CR3 reads occur in a live handler.
- No owned COM1 panic path is invoked.
- No QEMU null-dereference or invalid-opcode panic proof is claimed.

## Evidence Boundary

This is host-only composition evidence from caller-owned fault-frame bytes to a
panic reporter byte sink. It does not satisfy Gate 3 fault diagnostics and does
not change the default boot path.
