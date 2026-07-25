# Step 615 - OS.Std Buffer Operations

## Summary

Added `OS.Std.Memory.BufferOperations` with status-coded byte `MemoryCopy`
support and memmove-style overlap behavior.

## Why

The EFI shell shim has narrow memory-copy helpers, but shared no-runtime code
did not have a host-tested, status-coded buffer-copy primitive that kernel and
runtime planning paths can use without relying on `System.Buffer` compatibility
or exception-driven failure.

## Files changed

- `std/no-runtime/shared/Memory/BufferOperations.cs`
- `tests/OS.HostTests/StdBufferOperationsTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- `done/step615.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|std\.buffer|host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe *> .\artifacts\logs\host-tests-step615.log`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

The host-test project build passed with 0 warnings and 0 errors.

Focused host-test output and `artifacts/logs/host-tests-step615.log` emitted:

```text
TEST_PASS std.buffer memorycopy exact bytes
TEST_PASS std.buffer memorycopy overlap forward
TEST_PASS std.buffer memorycopy overlap backward
TEST_PASS std.buffer memorycopy rejects short destination
TEST_PASS std.buffer memorycopy zero length and layout
TEST_PASS host.tests
```

The solution build passed with 0 warnings and 0 errors.

`scripts/check-naming.ps1` emitted:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

`git diff --check` passed with line-ending normalization warnings only.

## Failures

None in this slice.

## Deferred Work

- No `System.Buffer` compatibility is claimed.
- `BlockCopy`, object arrays, array covariance handling, pointer overloads, and
  managed-reference copying are not implemented.
- Existing kernel-local byte-copy loops are not migrated in this step.

## Evidence Boundary

This is host-tested shared std helper evidence only. It does not complete Gate 4
std/runtime work and does not implement NativeAOT runtime memory helpers.
