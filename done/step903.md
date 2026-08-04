# Step 903 - Preserve the Windows x64 Out8 byte argument

## Summary

Corrected the production C# `Out8` stub from
`66 89 CA 88 D0 EE C3` to `88 D0 66 89 CA EE C3`. Under the Windows x64
unmanaged ABI, the value arrives in `DL` and the port in `CX`; the new order
copies `DL` to `AL` before repurposing `DX` for the port. `In8` remains
`66 89 CA EC C3`.

## Why

M02-002's historical evidence was intentionally host-only and never invoked
the privileged entries. The first M02-004 live `Out8` call exposed that the old
sequence destroyed the byte argument before using it. The historical manifest,
Gitea evidence, and immutable ledgers remain unchanged; this supplemental
repair supersedes the old sequence only for current live-use suitability.

## Files changed

- `src/OS.Kernel/Diagnostics/PortIoStubPlan.cs`
- `src/OS.Kernel/Diagnostics/ManagedX64StubStrategyADRUseExistingAudited.cs`
- `src/OS.Kernel/Diagnostics/ExistingIn8Out8StubBytesRWPagesFinalize.cs`
- `src/OS.Kernel/Diagnostics/Com1PortIoBackendPlan.cs`
- `src/OS/Boot/M02SafeStubPageIntegration.cs`
- `tests/OS.HostTests/PortIoStubPlanTests.cs`
- `tests/OS.HostTests/ManagedX64StubStrategyADRUseExistingAuditedTests.cs`
- `tests/OS.HostTests/ExistingIn8Out8StubBytesRWPagesFinalizeTests.cs`
- `tests/OS.HostTests/Com1PortIoBackendPlanTests.cs` (owned direct test;
  existing assertions consume the corrected validator)
- `tests/OS.HostTests/PortIoStubWindowsX64AbiRepairTests.cs`
- `docs/boot.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1`
  - Exit `0`; zero warnings and zero errors.
- Exact no-stdlib EFI preflight with all M01/M02 switches
  - Exit `0`; 65 source-authority inputs and 52 compile items.
- `scripts/build-efi-shell.ps1` with all required M01/M02 switches
  - Exit `0`; artifact verifier passed with no imports.
- Real `scripts/run-qemu-smoke.ps1 -BuildEfi ...` M02-004 profile
  - Exit `0`; both QEMU and raw-observation tests passed.
- Complete HostTests acceptance wave
  - Final exact pushed-head counts and hashes are posted to Gitea.

## Evidence

The focused repair envelope is:

```text
TEST_START m02.out8-abi-repair
CLROS_M02_OUT8_ABI_REPAIR_READY out8Bytes=88D06689CAEEC3 fnv1a64=982E5B7E3EE58E56 sha256=B45339EB93D346D9FE6B32700F227CFB367AB1D10970DD6655A173AFE54705E2 valueBeforePort=true in8Unchanged=true
TEST_PASS m02.out8-abi-repair
```

The exact 4,096-byte page now has:

- Safe entry: `B8 4B 4F 53 4F C3` at offset 0.
- Out8: `88 D0 66 89 CA EE C3` at offset 16.
- In8: `66 89 CA EC C3` at offset 32.
- FNV-1a64: `982E5B7E3EE58E56`.
- Independent SHA-256:
  `B45339EB93D346D9FE6B32700F227CFB367AB1D10970DD6655A173AFE54705E2`.
- Unchanged instruction bytes/occupied extent: 18/37.

The real QEMU run completed seven checked COM1 writes, observed all 50
ordered/unique cumulative markers, and observed no `TEST_FAIL`,
`M02_COM1_CONFIGURATION_FAILED`, or other configured forbidden marker. EFI and
serial SHA-256 values are
`61CA5D6879DA1AE7DC0A6938AA61AD64DCEFBD61AA937C0DF2D8C3BF0809820E`
and
`E452EF725FAF34775EE00C310E86245F217CA2638926B70B3194C711A0F2876D`.

## Failures

- The pre-repair live guest stopped at the first `Out8` after
  `M01_PAGE_ZERO_ABSENT`; it did not emit the M02-004 marker and was rejected.
- The first post-repair complete host run failed 3 of 470 started groups because
  two direct consumers still encoded the old order/page image. Tracker issue
  #442 was amended before `Com1PortIoBackendPlan` was edited. No result from
  that host run was promoted.
- The following complete host attempt exceeded an obsolete 30-minute wrapper
  while still making progress in `test-efi-artifact.ps1`; its verified
  CLROS-only process tree was stopped and the incomplete stream was rejected.
- The next complete host attempt was intentionally terminated for the
  user-requested reboot. Its verified CLROS-only child process tree was stopped
  before restart, and no partial output was accepted as evidence.

## Deferred work

This step proves the ABI-preserving byte order and, together with the real-QEMU
integration, seven pre-`ExitBootServices` COM1 configuration writes. It does
not prove general calling-convention coverage, `In8`, owned serial payload
output, post-EBS diagnostics, physical hardware/provider support, hosted .NET,
Minimal API, Kestrel, CloudReady status, or production readiness.
