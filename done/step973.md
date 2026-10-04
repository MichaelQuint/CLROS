# Step 973 - Integrate retained ACPI copies and bounded reference helpers

## Summary

Import the reviewed #539 ACPI adapter and #537 reference-helper integration into
the canonical branch after their complete focused integration initializers and
default/enabled NativeAOT publications pass. The source import compares 49 paths,
changes 44 (including six new C# files), and verifies the complete combined
88-Compile/102-raw/38-property closure. This checkpoint does not close issues or
accept guest execution. PR538 remains a draft pending the common suite, native
review and fresh guest evidence.

## Why

M03-002 needs actual no-stdlib retention before entropy and firmware-exit work.
The existing opt-in ACPI adapter now admits and copies five complete validated
tables into guarded static owned storage, publishing five descriptors only after
copy/readback succeeds. Embedded physical addresses remain unchanged. Numeric
host controls cover the real EFI methods; firmware observation is still pending.

M04-010 needs returning compiler reference helpers before TypeManager work.
The new default-off profile reuses the original retained-page authority and
single allocator cursor. A C# generator emits three special helper bridges;
the ordinary scalar callback and managed bulk entry share a POD-only admission
core. Six allocation roles and a finite 23-event sequence remain noncollecting.
The general GC/WriteBarriers gate remains incomplete.

## Files changed

- `scripts/build-efi-shell.ps1`.
- `scripts/run-qemu-smoke.ps1`.
- `scripts/test-efi-artifact.ps1`.
- `src/OS.Kernel/Diagnostics/CheckedReferenceBarrierProbeAuthority.cs`.
- `src/OS.Kernel/Runtime/NativeAotHelperLedger.cs`.
- `src/OS/Boot/EfiEntry.cs`.
- `src/OS/OS.csproj`.
- `src/OS/Runtime/CheckedReferenceBarrierAdapter.cs`.
- `src/OS/Runtime/MinimalRuntime.cs`.
- `src/OS/Runtime/RhpNewFastAllocation.cs`.
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`.
- `tests/OS.HostTests/CheckedReferenceBarrierThunkGeneratorTests.cs`.
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`.
- `tests/OS.HostTests/M01InstructionPublicationSerializerGeneratorTests.cs`.
- `tests/OS.HostTests/M02CheckedPortIoFunctionPointerEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02Com1OwnedPortIoBackendEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02DoubleFaultIstEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02PageFaultCr2ErrorCodeEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02SafeStubPageEfiEntryTests.cs`.
- `tests/OS.HostTests/M02Ud2VectorStateEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M02UnexpectedVectorPanicEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M03AcpiBootstrapCopyEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M03MemoryMapCaptureEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04ArithmeticConformanceEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04ArrayAllocationEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04CheckedReferenceBarrierEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04LayoutContractEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04ManagedHeapPageRangeEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04RhpNewFastEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04SpanMemoryConformanceEfiIntegrationTests.cs`.
- `tests/OS.HostTests/M04StringAllocationEfiIntegrationTests.cs`.
- `tests/OS.HostTests/NativeAotHelperLedgerTests.cs`.
- `tools/OS.X64TextSerializerGenerator/CheckedReferenceBarrierThunkGenerator.cs`.
- `tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets`.
- `tools/OS.X64TextSerializerGenerator/Program.cs`.
- `docs/boot.md`, `docs/efi-build.md`, `docs/nativeaot-nostdlib-limits.md` and
  `docs/completion-gates.md`: implemented source and exact pending evidence.
- `.gitea/backlog/clros-issues.json`: only the two currentEvidence fields for
  M03-002 and M04-010; no status/dependency promotion.
- `done/step973.md`, reserved by `scripts/new-done-step.ps1 -Reserve` at
  08:36:58.775548-08:36:59.031633 UTC on 2026-10-04, native exit0.

The legacy fixture changes retain strict counts, hashes and equality checks.
Three old source-authority lists add the new generator. The ACPI shared edits
refresh only their admitted source/entry pins. BootSequence, central Probes,
the runtime ledger's general-GC boundary and QemuRunner implementation remain
unchanged by the ACPI lane; the new runtime probe stays false by default.

## Tests run

All following times are UTC on 2026-10-04. Commands and raw streams are retained
under the author worktrees and orchestration artifacts below.

- Reference managed lane: complete eight original module initializers, focused
  build07:32:21.900522-07:32:24.674181 and run07:32:25.998533-08:15:03.447971.
  Native exit0, zero build warnings/errors, 82 TEST_START/83 TEST_PASS/zero
  TEST_FAIL, one final host.tests and empty stderr. All907 before/after inputs
  and14 launch/end binaries match. Separate terminal readback binds all47 owned
  files and102 raw inputs, including the three scripts. The later three source
  list fixes affect excluded fixtures, not this executed dependency closure.
- Reference integration: whole five-group initializer, build08:25:15.703532-
  08:25:18.149058 and run08:25:18.527237-08:31:23.110060. Native exit0,
  5 starts/6 passes/zero failures, host.tests and empty stderr. All910 inputs
  and launch/end binaries match. Actual mapped POD core, transcript mutations,
  profile conflicts and default/enabled NativeAOT publications pass.
- ACPI host core: the prior accepted whole EFI numeric child passes5 starts/
  6 passes/zero failures; eight full ACPI/physical/QemuRunner initializers pass
  221 starts/222 passes/zero failures. Workspace335696 bytes, guarded aggregate
  266272 bytes, payload offset16/capacity266240 and30 descriptor slots were
  measured. See the preserved host-final1 report; no numeric size was guessed.
- ACPI grammar retry4: build08:28:24.344761-08:28:27.844660 and run08:28:28.642913-
  08:29:07.387125, native exit0, 2 starts/3 passes/zero failures. It executes both
  complete source/literal methods, 54 profile children (3 positive/51 negative)
  and329 transcript/lifecycle controls. Normal HostTests Release compilation
  08:29:07.478051-08:29:41.795860 passes0 with zero warnings/errors. All931 inputs
  and6 launch/end records remain unchanged. This selection is not claimed as
  the whole integration initializer.
- ACPI integration: whole four-group initializer, build08:31:44.831144-
  08:31:47.301518 and run08:31:47.907177-08:34:25.198303. Native exit0,
  4 starts/5 passes/zero failures, host.tests and empty stderr. Actual numeric
  child, complete grammar/profile controls and both NativeAOT publications pass.

Focused builds use `dotnet build <focused.csproj> --nologo -m:1
--disable-build-servers -p:UseSharedCompilation=false`; reference builds also
set `-nodeReuse:false`, and ACPI builds use `-c Release`. Runs invoke the exact
built focused DLL with `dotnet`. Normal ACPI compilation uses an isolated
`--artifacts-path`. Exact absolute argv, effective child environment, UTC,
native exits and unmodified raw output are retained in each command/result.

The canonical combined solution build/full HostTests run is the next gate,
not evidence already completed by this step. Independent actual-image review,
QEMU, disk and lifecycle acceptance are also pending.

After import, `scripts/sync-gitea-backlog.ps1 -Offline` passed at
08:40:50.148766-08:40:57.341005, native exit0/empty stderr, validating345
catalog issues,68 labels and16 milestones without API mutation. The naming and
first-party source firewall passed at08:40:50.144553-08:41:40.404278, native
exit0/empty stderr. `git diff --check` also passed. Recorder prefixes
`issue537-539-catalog-offline` and `issue537-539-precommit-naming` preserve
these commands under `artifacts/orchestration-20261001/issue531-final-transaction/`.

## Evidence

Source transfer: root `artifacts/orchestration-20261001/
issue537-539-root-import-preflight/{preflight-v2,import-intent,import-result}.json`.
Reviewed manifest SHA256
`5852AE2F77EDBF9F2AD0DE2FD0212B6D9BBABF7F61633594F4AD61609D70F0E3`.
Import completed08:36:50.260837; all102 combined raw files matched.

Reference author worktree is `m04-reference-barrier-native/CLROS` under the
Codex worktrees directory. Its `artifacts/issue537/final-focused-handoff/report.md`
SHA256 is `4D4E995221ECDFB29063448AB7014CE65958C9B60054D30537EBAF44C150ACD3`.
Managed stdout SHA256
`3BC5FB500F56E6C2330C71ABFEFF3AA5CC2D3D751ACE62F517122464D5A348B2`;
reference integration stdout
`8C5C86F5A17204DDAAD1E2FFE2355FCAE0492227235DBD4705565D8BE1717B78`.
The terminal source readback is
`CA754E3DD386AB76ABA7A4B9C39F92CEFFCD3C707D753317157E20ACBF875546`.
Independent v5 source/managed review SHA256
`67D324C7A38F168E6D632743C256270219AB5E86C7D61A38F63EE433E3B19BAF`.
The later namespace-only fixture repair is independently checked too.

ACPI author worktree is `m03-owned-acpi/CLROS`. Its `artifacts/issue539/
grammar-final1/report.md` SHA256 is
`D145015DC8058242A8AEFCFC487E17867EDD37CA03268A7AD361C0A272431127`.
Grammar stdout SHA256
`A6A2B406A411C8D746C0E7E39CCC68FBFABB3DDBD9D6EF95D9EFFC55F4A4DE62`;
whole integration stdout
`FF03534E1743953F616538814FC447D77CB817FA3D0F30D5CCA7C39552575558`;
whole integration result
`6E1A95ADA561D7AB3BB6AE325E3D1F45C74B9C9638B0139FE588277916713C02`.
Parent read/rehashed the full shared source and each narrow fixture delta,
complete results, command plans, raw markers and both final source inventories.

## Failures

The initial reference managed run failed27 preflight groups because three new
Compile rows preceded the last historical row. V4 restored the original85-row
prefix before appending3 and refreshed derived pins; the full rerun passed.
Three old fixture source lists also omitted the new generator and were fixed
without weakening equality. The first reference integration run failed its
Core child compilation on an unqualified PhysicalAddress; the sole repair uses
OS.Boot.PhysicalAddress. Both failed runs remain preserved.

ACPI's first three grammar runs exposed test-only errors: expecting an explicit
false property node where the existing symbol uses an unset/explicit-true
condition; expecting a build switch in a dry-run that skips building; and a
PowerShell array-expression precedence error that made a negative summary
mutation a no-op. Exact fixture repairs preserve all validation controls; the
last additionally requires two mutation values and changed input. Product
core/script/raw inputs did not change. A proposed35-to27 summary-row review
finding was withdrawn after tracing the actual runner's configured-marker
collector; no product change followed that finding. Packaging-only identity
case/row mistakes and their corrected readbacks remain separate from test runs.

## Deferred work and evidence boundary

Finish independent actual-image metadata/layout/relocation, all23 reference
routes, complete helper/callback ABI, stack and admitted SIMD review. Run the
canonical clean-head full suite, then fresh default/reference/ACPI QEMU/OVMF
captures with complete raw, lifecycle and disk accounting. Merge PR538 and post
tracker closure evidence only after those gates pass. #59/#539/#79/#536/#537
remain open; #60 entropy and #80 TypeManager remain blocked until acceptance.
No general GC, wider SIMD, unwinding, real ExitBootServices, post-firmware
ownership, hosted .NET10/Minimal API/Kestrel, cloud or production readiness is
claimed. Do not treat this source/publication checkpoint as live kernel proof.
