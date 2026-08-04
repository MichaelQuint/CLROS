# Step 904 - Execute checked COM1 configuration in the EFI guest

## Summary

Integrated M02-004 into the explicit no-stdlib EFI closure behind the
default-false `M02_COM1_OWNED_PORT_IO_PROBE`. After the accepted M02-017 safe
entry, inherited M01 restoration audits, and M02-003 checked binding, the guest
constructs the owned backend, calls `ConfigureDefault()` once, requires seven
completed writes, and emits one exact readiness marker through UEFI serial.

## Why

Issue #44's four owned paths could implement and host-test the backend but could
not link it into `src/OS`, change the project closure, or produce the required
real-QEMU evidence. Supplemental issue #441 claimed that serialized shared-file
lane explicitly.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/Boot/M02CheckedPortIoFunctionPointerIntegration.cs`
- `src/OS/Boot/M02Com1OwnedPortIoBackendIntegration.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/run-qemu-smoke.ps1`
- `scripts/test-efi-artifact.ps1`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `docs/efi-build.md`
- `docs/qemu.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
  - Exit `0`; zero warnings and zero errors after all direct consumers were
    corrected.
- `scripts/build-efi-shell.ps1 -PreflightOnly` with all required switches
  - Exit `0`; `EFI_PREFLIGHT_OK`, 65 inputs, 52 compile items.
- `scripts/build-efi-shell.ps1` with all required switches
  - Exit `0`; `TEST_PASS efi.artifact.verify` and
    `TEST_PASS efi.shell.publish`.
- Real `scripts/run-qemu-smoke.ps1 -BuildEfi` with all required switches,
  QEMU `C:\Program Files\qemu\qemu-system-x86_64.exe`, OVMF
  `C:\Program Files\qemu\share\edk2-x86_64-code.fd`, and 30-second timeout
  - Exit `0`; `TEST_PASS qemu.smoke` and
    `TEST_PASS m01.026.raw-observations`.
- Complete HostTests acceptance wave
  - Final exact pushed-head counts and hashes are posted to Gitea.

## Evidence

The explicit closure reported:

```text
EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=65
EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3 compileItems=52 prerequisiteGap=UefiMemoryType
EFI_PUBLISH_POST_RESTORE_EVALUATION status=ok compile=52 projectReferences=0 references=0 analyzers=2
```

The verifier accepted PE32+ AMD64, EFI subsystem 10, exact `EfiMain`, no import,
bound-import, IAT, or delay-import directories, and the linked instruction
serializer. Artifact evidence:

- EFI: 485,888 bytes, SHA-256
  `61CA5D6879DA1AE7DC0A6938AA61AD64DCEFBD61AA937C0DF2D8C3BF0809820E`.
- Raw image: 67,108,864 bytes, SHA-256
  `4A49E7F30493BBF59DA14BF9F756B7A349037CD0D2B7AAA941DEA2A45E49D522`.
- Serial: 2,753 bytes/72 lines, SHA-256
  `E452EF725FAF34775EE00C310E86245F217CA2638926B70B3194C711A0F2876D`.
- Summary: 10,675 bytes, SHA-256
  `56162B8BBEE02FCF80C62E4D38456F623EEA558F3D646F1E1654B951DB49E190`.

The runner reported `terminal=Success`, `orderedSeen=50`, `outOfOrder=0`,
`uniqueSeen=50`, and `duplicateUnique=0`. The guest emitted exactly once:

```text
CLROS_M02_004_READY configurationWrites=7 bindingReady=true guestPortIo=true guestIn8=false markerTransport=uefiSerial postExitBootServices=false
```

No `TEST_FAIL`, `M02_COM1_CONFIGURATION_FAILED`, or configured forbidden marker
appeared. Raw observation hashes remained independently captured by the
existing M01-026 shim.

## Failures

- A static integration-result field changed the layout-sensitive stage-three
  path and stopped at `M01_PRE_CR3`; rejected and replaced by a local result.
- With that fixed, the guest reached every M01 restoration marker through
  `M01_PAGE_ZERO_ABSENT` but stopped on the old clobbering Out8 sequence;
  rejected and repaired under supplemental issue #442.
- The first post-repair complete host wave found three stale exact-order/page
  consumers; rejected until those source-bound checks were corrected.
- The subsequent complete host attempt exceeded an obsolete 30-minute wrapper
  while still progressing through `test-efi-artifact.ps1`; its verified
  CLROS-only process tree was stopped, and the incomplete stream was rejected.
- The next complete host attempt was deliberately terminated for the
  user-requested reboot. Its verified CLROS-only child process tree was stopped
  before restart; no partial output was promoted.
- A focused-wave wrapper later misclassified the QEMU runner's declarative
  `FORBIDDEN_MARKERS` line because that configuration text contains the token
  `TEST_FAIL`. The underlying dry-run exited `0` with
  `TEST_PASS qemu.runner.dryrun`; the aggregate wrapper result was rejected and
  the parser was narrowed to actual line-leading failure markers.
- The first complete exact-head host wave after reboot was stopped after 390
  starts, 385 passes, and three line-leading failures. All three were
  `BuildEfiShellPreflightTests` process timeouts at its fixed 30-second limit;
  adjacent compile cases passed, stderr was empty, and the standalone exact
  preflight completed in 12.348 seconds. The owned test-process envelope was
  increased to a still-bounded 120 seconds with the existing whole-tree kill
  on timeout; no partial result from the rejected wave was promoted.
- The next exact-head wave passed all three repaired preflight cases, then was
  stopped after 648 starts, 646 passes, and one failure when the valid linked
  serializer fixture reached the verifier test's fixed 45-second process limit
  (`elapsedMs=45092`, output only `TEST_START efi.artifact.verify`). Adjacent
  negative verifier cases passed and stderr remained empty. The owned fixture
  process envelope was raised to 120 seconds while preserving kill-on-timeout
  and the verifier suite's independent performance ceilings; the incomplete
  wave was rejected.
- The following complete wave passed both repaired timeout boundaries and
  reached 1,313 starts / 1,311 passes before the inherited M02-003 integration
  test rejected its compile-switch graph. Its broad condition search found the
  new downstream `M02 COM1 -> checked binding` error edge as well as the
  existing `checked binding -> safe stub` prerequisite, while still asserting
  exactly one match. The owned test now requires exactly those two directed
  edges; the 167,654-byte incomplete stdout stream and 294-byte exception-only
  stderr stream were rejected.

## Deferred work

The guest marker is still transported through the UEFI serial protocol. This
step performs no `In8`, no owned serial payload write, no real
`ExitBootServices`, and no post-firmware diagnostics. It does not prove the M02
exit criterion, physical hardware/provider behavior, hosted .NET, Minimal API,
Kestrel, CloudReady status, or production readiness, and it changes no numbered
completion-gate status.
