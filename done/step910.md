# Step 910 - M02-006 panic-safe formatting and owned COM1 host route

## Summary

Implemented the M02-006 production C# fault-record formatter and serial
reporting route over the checked, owned `Com1OwnedPortIoBackend`. The production
core uses explicit pointers and lengths so the same code can enter the no-stdlib
EFI closure; host-only `Span` wrappers remain behind `!NOSTDLIB_EFI`. Eleven
focused cases cover exact formatting/reporting, the harmless readiness marker,
delayed transmitter readiness, timeouts, invalid inputs, caller-buffer
atomicity, unmanaged/default result contracts, absence of fallback surfaces,
the byte catalog, and zero managed allocations on the warmed reporting path.

## Why

M02-005 proved one bounded pre-firmware marker could travel through the checked
COM1 backend. M02-006 requires the next dependency-ordered leaf: an
allocation-free, status-coded path that formats a complete panic record into
caller-owned storage and routes those bytes through the same owned backend
without falling back to firmware or managed console output.

## Files changed

- `src/OS.Kernel/Diagnostics/PanicSafeFormattingSerialReportingOwnedCOM1.cs`
- `tests/OS.HostTests/PanicSafeFormattingSerialReportingOwnedCOM1Tests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- `done/step910.md`

The shared EFI/project/tooling/README paths are owned separately by supplemental
issue #455 and are recorded in `done/step911.md`.

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed after the no-stdlib pointer-core conversion with 0 warnings and 0
    errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with 0 warnings and 0 errors.
- The temporary focused M02-006 harness built the current `OS.Kernel` project
  together with the two owned M02-006 test modules and ran
  `artifacts\focused-m02-006\bin\Debug\net10.0\Focused.exe`.
  - Eleven formatter/reporter subtests passed.
  - Four source-bound EFI integration cases passed.
  - `CLROS_M02_006_READY panicFormatting=hostValidated ownedCom1Transport=hostCallbacks guestPortIo=false allocationFree=true`
  - `TEST_PASS m02.006`
  - `TEST_PASS m02.006.efi.integration`
  - `TEST_PASS m02.006.focused`
- The required complete HostTests command is repeated on the final staged tree
  and bound to the pushed review head in Gitea. The earlier complete log is
  retained here only as development evidence because the production source and
  tests changed later.

## Evidence

- Initial complete host log:
  `artifacts/evidence/m02-006/host-tests.log` (generated, not committed),
  874,888 bytes, SHA-256
  `A675033BD054AB13508FBF9BA9E903DF0745170F048E08A3F667D009FD69E9BA`.
  It recorded zero `TEST_FAIL` and terminal `TEST_PASS host.tests`, but is not
  final evidence because it predates the no-stdlib pointer-core/test expansion.
- Focused executable SHA-256:
  `1EDA11379163F911546000AC44548050F47D07BD7ACC895A965D9A47391A2EB9`.
- The exact panic line contains the vector/error-code state, RIP, RSP, RBP,
  RFLAGS, CR2, CR3, and RAX through R15, followed by CR/LF.
- The formatter computes the complete required length before writing, so an
  undersized destination remains byte-for-byte unchanged.
- The reporter configures COM1 through the accepted seven-write sequence,
  polls line-status register `0x3FD` for THRE, and writes only to data register
  `0x3F8` through the checked backend.
- The warmed host report path records zero managed allocation.
- The manifest-listed reference influence
  `references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs` is not
  present in this checkout. No missing reference bytes or source were inferred,
  copied, or attributed; the implementation uses current first-party CLROS C#
  contracts only.

## Failures

- The first EFI linkage attempt exposed use of `Span` in the no-stdlib closure.
  The production implementation was corrected to use an unsafe pointer/length
  core and to compile `Span` wrappers only for the host target. Success was not
  stubbed and no runtime helper was invented.
- The listed SharpOS reference path is unavailable, so reference comparison is
  explicitly not evidence for this slice.
- The initial complete host log is deliberately rejected as final evidence
  because its source/test inputs became stale.

## Deferred work

- Bind the complete source tree, EFI artifact, image, serial, QEMU summary,
  stdout/stderr, and policy evidence to the pushed review commit.
- Post evidence to issues #46 and #455, obtain independent exact-head review,
  merge, then complete the separately reviewed manifest closure and idempotent
  backlog synchronization for M02-006.

## Evidence boundary

This host slice proves bounded panic-safe formatting and checked owned-COM1
routing through safe unmanaged host callbacks. Its host marker explicitly
records `guestPortIo=false`. The separately owned integration in step 911 adds
harmless pre-`ExitBootServices` guest readiness evidence, but neither step proves
a live CPU fault or panic route, real IDT/GDT/TSS/IST installation, output across
real `ExitBootServices`, runtime unwind, packet-loss behavior, cloud cleanup,
provider support, the M02 exit criterion, CloudReady status, Kestrel, or
production readiness.
