# Step 918 - M02-009 owned GDT/TSS terminal EFI integration

## Summary

Integrated the frozen, host-audited M02-009 C# descriptor-transition stream
into the default-off no-stdlib EFI application. The bounded pre-firmware-exit
profile now allocates disjoint code/data pages, validates the exact stream and
context, enters the stream only after `OS_SMOKE_PASS`, loads and reads back its
owned GDT/TSS, emits its terminal transcript through owned COM1, and remains
halted until the evidence runner terminates QEMU.

## Why

Managed issue M02-009/#49 required real QEMU/OVMF evidence in addition to its
four-path host-only leaf. Supplemental issue #475 owns the central EFI,
project, runner, documentation, test, and evidence-record files needed to cross
that boundary without weakening the leaf's exact ownership contract.

## Files changed

- `src/OS/Boot/EfiEntry.cs`
- `src/OS/OS.csproj`
- `scripts/build-efi-shell.ps1`
- `scripts/test-efi-artifact.ps1`
- `scripts/run-qemu-smoke.ps1`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `README.md`
- `docs/efi-build.md`
- `docs/qemu.md`
- `done/step918.md`

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with zero warnings and zero errors after the #480 semantic-guard
    restack.
- Cumulative flagged `dotnet build .\src\OS\OS.csproj` through
  `EnableM02OwnedGdtTssTerminalProbe=true`
  - Passed with zero warnings and zero errors when all M01/M02 prerequisites
    were explicitly enabled.
- Focused source-linked M02-009 EFI integration harness
  - Passed all six groups, including the executable fake-Boot-Services cleanup
    matrix and the persistent/spontaneous/hostile fake-QEMU transcript matrix.
- `scripts/check-naming.ps1`
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- PowerShell AST parse for `scripts/run-qemu-smoke.ps1`
  - Zero parse errors.
- Fresh cumulative QEMU/OVMF command with `-BuildEfi` and every M01/M02
  prerequisite through `-EnableM02OwnedGdtTssTerminalProbe`
  - `TEST_PASS efi.artifact.verify`
  - `TEST_PASS efi.shell.publish`
  - `TEST_PASS imagebuilder.raw`
  - `TEST_PASS qemu.smoke`
- The separately reviewed four-path leaf at
  `f61a2150925703ae88e0d0992fee8447c9c55503` completed its exact-head full
  HostTests gate in 2316.804 seconds with 3,453 starts, 3,454 passes, zero
  failures, and one terminal `TEST_PASS host.tests`; log SHA-256
  `EFE4F3EE180518585F6A49C9379A52C76E097DD21378606EABEBFC39339691EE`.
- `git diff --check`
  - Passed.

## Evidence

- Supplemental regression guard #480 merged through PR #481 as
  `12636e3c8f6f53fc1565df3441de3b86e156d61a` after official exact-head review
  #25 and a 3,452-start/3,453-pass/zero-failure HostTests run.
- The frozen four-path leaf merged through PR #482 as
  `37254d1fbb67432a523ae89ca66432b1c9fc2f3f` after official exact-head review
  #26. Its stable patch ID remained
  `46800d5eba873ed0a23e84373519a4e2906228c2`, and the merged blobs matched the
  reviewed head.
- The corrected integration implementation before this evidence-only record was
  `0bfc8a5a1f670190ed2f7abd5feaad3a1883f068`, based directly on the leaf merge.
- The real runner contract contained exactly 59 required, 59 ordered, and 59
  unique records. All three lists were distinct and sequence-identical. The 43
  forbidden records were distinct and absent.
- Serial evidence contained exactly one `TEST_START m02.009`, one exact
  `CLROS_M02_009_READY ... postExitBootServices=false`, and one
  `TEST_PASS m02.009`, with zero `TEST_FAIL` records.
- The streaming runner reported
  `SERIAL_MARKER_STREAM status=Passed terminal=Success`, zero ordering and
  duplicate-unique failures, then
  `QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1`.
- QEMU: `11.0.50 (v11.0.0-12631-g54e84cdc7a)`.
- OVMF: 3,653,632 bytes; SHA-256
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- EFI: 529,408 bytes; SHA-256
  `A22F5B2AF24682693C09E361C16EE89271CDB9D71FA5D9BBFA32ABA784F8D443`.
- Serializer object: 336 bytes; SHA-256
  `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.
- Raw image: 67,108,864 bytes; SHA-256
  `C4F76528B0B9726165684FF2E5EA5091760BB34DDB116B3156D55FBC9C19EDF1`.
- Serial log: 4,244 bytes; SHA-256
  `3AAF960E6ABCB7F27A4580BF8B8D554FDB66EA165F71816C38C7B81A050B6BAF`.
- Summary log: 16,841 bytes; SHA-256
  `6077DB496B05555BA418FB045DBBABAC2EEA111493FBC4474D0FFE9203EA6632`.
- Summary status was `pass` with `MISSING_MARKERS none`,
  `OUT_OF_ORDER_MARKERS none`, `DUPLICATE_UNIQUE_MARKERS none`,
  `FORBIDDEN_MARKER_HIT none`, and `FAIL_REASON none`.

## Failures

- The first complete M02-009 leaf run was rejected after M02-008's old
  whole-file documentation hashes produced two failures. Issue #480 replaced
  only those mutable-doc hashes with fail-closed semantic guards; no failed
  run was promoted to M02-009 evidence.
- Preliminary review of the original stacked integration found three defects:
  the runner inherited 11 extra presence-only required markers, aliased
  allocation cleanup could double-free, and durable executable hostile runner
  coverage was missing. The corrected implementation replaces default required
  markers only through an explicit ordering-safe runner option, uses an exact
  cleanup plan, and exercises all requested hostile transcripts.
- One diagnostic direct OS build omitted the M01 prerequisite properties and
  correctly failed with
  `EnableM02SafeStubProbe requires EnableM01LoadedImageStackProbe=true`. The
  cumulative flagged build with all prerequisites then passed.
- Real-QEMU output from the pre-correction stacked head was retained only as
  rejected diagnostic evidence. The hashes above come from the corrected,
  rebased implementation.

## Deferred work

- Install an IDT and wire live IST-backed fault entries.
- Make guard pages non-present and enforce executable/write permissions with an
  owned page-table policy.
- Execute a controlled double-fault, page-fault, or NMI probe with bounded
  register capture and panic routing.
- Complete real `ExitBootServices` and prove owned diagnostics after firmware
  services are unavailable.
- Continue the runtime, scheduler, storage, networking, app-model, hosted .NET
  10, and Kestrel prerequisites required for the NativeAOT Minimal API demo.

## Evidence boundary

This step proves only bounded pre-`ExitBootServices` behavior on the recorded
QEMU/OVMF platform: current C# source generated the audited stream, temporary
UEFI code/data pages were prepared, GDTR/TR installation and readback matched
the exact private GDT/TSS contract, terminal records were emitted through the
owned COM1 transport, and the intentionally halted guest was terminated by the
runner. It does not prove page permission enforcement, W^X/NX, an installed
IDT, active IST routing, a triggered or handled CPU fault, real successful
`ExitBootServices`, post-firmware execution, hosted .NET, Kestrel, cloud-provider
support, CloudReady status, security hardening, or production readiness.

