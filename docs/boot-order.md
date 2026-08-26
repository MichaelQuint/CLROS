# Boot Order

## Current Status

`src/OS.Kernel/Boot/BootSequence.cs` is the managed boot-order boundary. `BootSequencePlan.cs` is the allocation-free marker plan used by both `BootSequence.Run` and the no-stdlib EFI artifact so firmware smoke output cannot bypass the ordered phase markers.

The firmware entry path emits `TEST_START efi.main`, `EFI_CONSOLE_READY`, UEFI memory-map status markers, `BOOT_HANDOFF_READY`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT <count>`, `BOOT_ACPI_RSDP_HANDOFF_READY`, and `UEFI_ACPI_RSDP_READY` before it enters the ordered `boot.sequence` marker plan.

`src/OS.Kernel/Boot/BootPhase.cs` defines the managed phase ledger used by `BootSequence.Run`. Each phase has a stable ID, marker name, precondition, and postcondition. The EFI artifact still uses `BootSequencePlan` byte output because that path must remain no-stdlib and allocation-free.

`BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` now adds host-tested handoff validation and normalized memory-map acceptance boundaries before implemented managed phases. The no-stdlib EFI artifact uses source-linked numeric validators to apply the same handoff and memory-map acceptance categories before it emits `BOOT_HANDOFF_READY` and `BOOT_MEMORY_MAP_ACCEPTED`.

`BootSequence.RunAppPackageReadiness`, `BootSequence.RunAppLaunchDescriptorReadiness`, `BootSequence.RunAppPayloadReadiness`, `BootSequence.RunAppExecutableMappingReadiness`, `BootSequence.RunAppExecutableMemoryReadiness`, `BootSequence.RunAppInvocationReadiness`, `BootSequence.RunAppLifecycleReadiness`, `BootSequence.RunAppSelectionReadiness`, `BootSequence.RunAppSelectionPreflightReadiness`, and `BootSequence.RunAppEntryReadiness` are optional host-tested helpers for the future app-package, selected-app descriptor, selected native payload, executable range planning, executable-memory planning, invocation metadata planning, app lifecycle/log readiness, aggregate app-selection, virtio-blk app-selection preflight, and final pre-entry boundaries. They are not part of the default managed boot order and are not emitted by the no-stdlib EFI artifact.

## Implemented Behavior

Current firmware-visible order:

1. `TEST_START efi.main`
2. `EFI_CONSOLE_READY`
3. `UEFI_MEMORY_MAP_READY` or `UEFI_MEMORY_MAP_FAIL`
4. `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`
5. `UEFI_MEMORY_MAP_OWNED`
6. `BOOT_HANDOFF_READY`
7. `UEFI_MEMORY_MAP_NORMALIZED`
8. `BOOT_MEMORY_MAP_ACCEPTED`
9. `BOOT_MEMORY_REGIONS_READY`
10. `BOOT_MEMORY_REGION_COUNT <count>`
11. `BOOT_ACPI_RSDP_HANDOFF_READY`
12. `UEFI_ACPI_RSDP_READY`
13. `TEST_START boot.sequence`
14. `BOOT_PHASE PhaseA.Audit START`
15. `BOOT_PHASE PhaseA.Audit PASS`
16. `BOOT_PHASE PhaseA.Diagnostics START`
17. `BOOT_PHASE PhaseA.Diagnostics PASS`
18. `OS_BOOT_OK`
19. `TEST_PASS boot.sequence`
20. `OS_SMOKE_PASS`

The ordered `boot.sequence` portion is host-tested through `BootSequence.Run` and QEMU-tested through `EfiMain`.

Current host-tested managed handoff-aware order:

1. `TEST_START boot.sequence`
2. `BOOT_HANDOFF_READY`
3. `BOOT_MEMORY_MAP_ACCEPTED`
4. `BOOT_PHASE PhaseA.Audit START`
5. `BOOT_PHASE PhaseA.Audit PASS`
6. `BOOT_PHASE PhaseA.Diagnostics START`
7. `BOOT_PHASE PhaseA.Diagnostics PASS`
8. `OS_BOOT_OK`
9. `TEST_PASS boot.sequence`
10. `OS_SMOKE_PASS`

If managed handoff validation fails, `BootSequence` emits `BOOT_HANDOFF_FAIL code=<status-code> status=<status-name>` and stops before phase markers. If live EFI handoff validation fails, `EfiMain` emits `BOOT_HANDOFF_FAIL <status-code>` and stops before the ordered boot sequence.

If managed normalized memory-map acceptance fails, `BootSequence` emits `BOOT_MEMORY_MAP_REJECTED <status-name>` and stops before phase markers. If live EFI memory-map acceptance fails, `EfiMain` emits `BOOT_MEMORY_MAP_REJECTED <status-code>` and stops before the ordered boot sequence.

If live EFI boot memory-region materialization fails, `EfiMain` emits `BOOT_MEMORY_REGIONS_FAIL <status-code>` and stops before ACPI RSDP handoff and the ordered boot sequence.

Current implemented managed phase ledger:

| ID | Marker name | Preconditions | Postconditions |
| --- | --- | --- | --- |
| `PhaseA_Audit` | `PhaseA.Audit` | Repository baseline, marker contract, and first-party source boundaries are available. | Boot audit marker emitted and early ordering evidence can be recorded. |
| `PhaseA_Diagnostics` | `PhaseA.Diagnostics` | A serial sink is available and boot audit markers have been emitted. | Basic diagnostics marker emitted and smoke status can be reported. |

Future placeholder phase IDs are present but not emitted yet:

- `PhaseB_Critical`
- `PhaseC_Memory`
- `PhaseD_Runtime`
- `PhaseE_Platform`
- `PhaseF_Probes`
- `PhaseG_Apps`

`BootSequence.RunUefiGoneSimulation` adds a host-tested optional simulation path. The no-stdlib EFI artifact also has a `UEFI_GONE_SIMULATION_PROBE` build variant that emits the same pass markers before the normal ordered `boot.sequence` markers:

1. `UEFI_GONE_SIM_START`
2. `UEFI_GONE_SIM_PLATFORM_DENIED`
3. `POST_FIRMWARE_DIAGNOSTICS_READY` or `POST_FIRMWARE_DIAGNOSTICS_BLOCKED status=<code>`
4. `UEFI_GONE_SIM_PASS` or `UEFI_GONE_SIM_FAIL reason=<code>`

The no-stdlib EFI artifact also has an `EXIT_BOOT_SERVICES_FRESH_MAP_PROBE` build variant that emits the pre-exit probe markers and then skips the real firmware shutdown call:

1. `UEFI_EBS_FRESH_MAP_PROBE_START`
2. `UEFI_EBS_FRESH_MAP_READY` or `UEFI_EBS_FRESH_MAP_FAIL <status-code>`
3. `UEFI_EBS_CALL_SKIPPED`

## Unsupported Behavior

All real hardware phases are pending: OS-owned UEFI snapshot, `ExitBootServices`, owned serial, IDT load, ACPI, PCI, storage, network, manifest loading, and app launch.

The IDT descriptor, IDTR descriptor, GDT descriptor, M02-007 caller-owned GDT encoder, TSS/IST layout, readiness-gate, IDT table-plan, descriptor-table instruction-byte/install-stub/readiness plans, fault-probe stub plans, and fault-vector metadata contracts are host-tested, but no emitted boot phase loads an IDT/GDT/TSS, installs handlers, or triggers controlled CPU faults yet. The serial diagnostics guard, COM1 configuration plan, abstract COM1 writer, port-I/O instruction-byte plan, callable port-I/O stub plan, and COM1 backend-readiness plan are host-tested, but no emitted boot phase has an OS-owned serial driver yet.

M02-009 adds a host-audited caller-owned GDT/TSS context and terminal composite
instruction stream without adding a `BootSequence` phase. A separately merged
default-off EFI integration now links that exact source and has accepted
QEMU/OVMF evidence for its private GDTR/TR load/readback, owned COM1 transcript,
and terminal halt. That profile runs only after `OS_SMOKE_PASS` and does not
change the default managed or firmware-visible marker order.

M02-010 adds one host-audited 4,096-byte page of 256 fixed interrupt-entry
slots. It normalizes hardware-error-code and synthetic-error-code entries to
the same three-slot semantic prefix and checks every relative jump to one
common target. A separately integrated default-off EFI profile now
materializes and reads back the page in temporary LoaderData memory after any
enabled M04 checks and before ordinary memory-map capture. It deliberately adds
no `BootSequence` phase, executable mapping, IDT gate, handler invocation, or
fault probe. The default managed and firmware-visible marker orders remain
unchanged. The final accepted source head is
`b88a485c8562a0022ffaae760fb47ca9d5addba2`; its ownership boundary frees every
EFI-successful allocation exactly once, including a rejected zero-address
result. The opt-in profile's ordered auxiliary subsequence is:

1. `EFI_CONSOLE_READY`
2. `TEST_START m02.010`
3. `CLROS_M02_010_READY ... guestMaterialization=true ... stubExecution=false ...`
4. `TEST_PASS m02.010`
5. `UEFI_MEMORY_MAP_READY`

M02-011 adds one host-audited 4,096-byte IDT page with all 256 gates freshly
derived from the M02-010 slot geometry. Every gate is a present DPL0 interrupt
gate using selector `0x0008`; NMI/#DF/#PF use IST3/IST1/IST2 and all other
vectors use IST0. The API accepts no firmware IDT or gate source, so it copies
zero firmware gates. This four-path leaf is not compiled into the EFI
application, emits only `CLROS_M02_011_HOST_READY`, and adds no auxiliary guest
subsequence or `BootSequence` phase. `CLROS_M02_011_READY` remains reserved for
a separately claimed source-bound EFI/QEMU integration. IDTR loading and
continued execution remain later work.

`BootSequence.RunFaultInfrastructureReadiness` is a host-tested helper only. It emits `FAULT_INFRA_READY` for a successful `FaultInfrastructureReadinessResult` and `FAULT_INFRA_NOT_READY status=<status>` for a failed result. The default boot path does not call it yet.

`BootSequence.RunExitBootServicesTransition` is a host-tested helper only. It consumes a `UefiExitBootServicesPlanResult`, emits `UEFI_EBS_TRANSITION_READY` only for a successful plan, emits `UEFI_EBS_TRANSITION_BLOCKED status=<status>` for blocked plans, and marks `FirmwareServicesGuard` as exited only in the successful case. The default boot path does not call it yet.

`BootSequence.RunExitBootServicesPreflight` is a host-tested helper only. It consumes post-firmware diagnostics readiness and `UefiExitBootServicesPlanResult`, emits `UEFI_EBS_PREFLIGHT_READY` only when both are ready, emits `UEFI_EBS_PREFLIGHT_BLOCKED diagnostics=<status> plan=<status>` when either prerequisite fails, marks `FirmwareServicesGuard` exited only for the ready case, and leaves the default boot path unchanged. It does not call real firmware.

`UefiExitBootServicesLiveWiringPlan` is a host-tested call-order contract only. It records the intended live sequence for a future `EfiEntry` transition: capture a fresh memory map, confirm owned serial diagnostics, emit post-firmware diagnostics readiness, validate the transition plan, call firmware only when a future implementation enables that call, mark firmware services exited after success, and reject firmware-backed calls after the boundary.

`PlatformFirmwareExitBootServicesResult` is a host-tested firmware-call result contract only. It maps guarded future firmware-call outcomes into retry-plan attempt outcomes without enabling a live `ExitBootServices` call or changing the default boot marker order.

`BootSequence.RunEarlyDiagnosticsReadiness` is a host-tested helper only. It consumes `Com1PortIoBackendPlanResult` and `DescriptorTableInstallReadinessResult`, emits `EARLY_DIAGNOSTICS_READY` only when both are ready, emits `EARLY_DIAGNOSTICS_NOT_READY status=<status>` for serial-backend or descriptor-readiness failures, and leaves the default boot path unchanged.

`BootSequence.RunDiagnosticStubMaterializationReadiness` is a host-tested helper only. It consumes `ExecutableStubBundleMaterializerResult`, emits `DIAGNOSTIC_STUBS_MATERIALIZED` only when materialization succeeded and copied bytes are nonzero, emits `DIAGNOSTIC_STUBS_NOT_MATERIALIZED status=<status>` otherwise, and leaves the default boot path unchanged.

`BootSequence.RunPostFirmwareDiagnosticsReadiness` is a host-tested helper only. It consumes `SerialDiagnosticsGuardResult`, emits `POST_FIRMWARE_DIAGNOSTICS_READY` only when OS-owned serial diagnostics are explicitly ready, emits `POST_FIRMWARE_DIAGNOSTICS_BLOCKED status=<status>` for blocked states, and leaves the default boot path unchanged. This is the readiness gate required before a real `ExitBootServices` transition can safely continue past firmware-backed output.

`BootSequence.RunControlledFaultProbeReadiness` is a host-tested helper only. It consumes early diagnostics readiness and `FaultProbeReadinessResult`, emits `CONTROLLED_FAULT_PROBES_READY` only when both are ready, emits `CONTROLLED_FAULT_PROBES_NOT_READY status=<status>` for early-diagnostics or fault-probe-readiness failures, and leaves the default boot path unchanged.

`BootSequence.RunCpuFaultPanicReadiness` is a host-tested helper only. It
consumes `CpuFaultPanicReadinessResult`, emits `CPU_FAULT_PANIC_READY` only
after a caller-owned fault-entry frame has decoded and flushed through
`PanicSerialReporter`, emits `CPU_FAULT_PANIC_NOT_READY ...` with nested frame
and reporter status when blocked, and leaves the default boot path unchanged.

`BootSequence.RunBootPageAllocatorReadiness` is a host-tested helper only. It consumes `BootPageAllocatorStateInitResult`, emits `BOOT_PAGE_ALLOCATOR_READY status=Ready state=Success freeList=Success ...` only when initialized allocator-state metadata contains free nodes and free pages, emits `BOOT_PAGE_ALLOCATOR_NOT_READY status=<status> state=<state-status> freeList=<free-list-status> ...` for failed state initialization, default/uninitialized state, or empty state, and leaves the default boot path unchanged.

`BootSequence.RunAppPackageReadiness` is a host-tested helper only. It consumes `NativeManifestLoadResult`, `OsPackageParseResult`, and `NativeAppLaunchPlanResult`, emits `APP_PACKAGE_READY` only when all three prerequisites succeeded, emits `APP_PACKAGE_NOT_READY manifest=<status> package=<status> launch=<status>` when any prerequisite fails, and leaves the default boot path unchanged.

`BootSequence.RunAppLaunchDescriptorReadiness` is a host-tested helper only. It consumes `NativeAppLaunchDescriptorResult`, emits `APP_LAUNCH_READY` only when a selected app descriptor is ready, emits `APP_LAUNCH_NOT_READY status=<status> launch=<status> packageFile=<status> package=<status>` when descriptor creation fails, and leaves the default boot path unchanged.

`BootSequence.RunAppPayloadReadiness` is a host-tested helper only. It consumes `NativeAppLaunchDescriptorResult`, emits `APP_PAYLOAD_READY` only when the selected app descriptor and payload header are ready, emits `APP_PAYLOAD_NOT_READY descriptor=<status> payload=<status>` when descriptor creation or payload-header validation fails, and leaves the default boot path unchanged.

`BootSequence.RunAppExecutableMappingReadiness` is a host-tested helper only. It consumes `NativeAppPayloadMappingPlanResult`, emits `APP_EXEC_MAPPING_READY` only when selected payload range planning succeeds, emits `APP_EXEC_MAPPING_NOT_READY status=<status> descriptor=<status> payload=<status>` when range planning fails, and leaves the default boot path unchanged.

`BootSequence.RunAppExecutableMemoryReadiness` is a host-tested helper only. It consumes `NativeAppExecutableMemoryPlanResult`, emits `APP_EXEC_MEMORY_READY` only when selected executable-memory planning succeeds, emits `APP_EXEC_MEMORY_NOT_READY status=<status> mapping=<mapping-status> descriptor=<descriptor-status> payload=<payload-status>` when memory planning fails, and leaves the default boot path unchanged.

`BootSequence.RunAppInvocationReadiness` is a host-tested helper only. It consumes `NativeAppInvocationPlanResult`, emits `APP_INVOCATION_READY` only when selected native app invocation metadata planning succeeds, emits `APP_INVOCATION_NOT_READY status=<status> memory=<memory-status> launch=<launch-status>` when invocation planning fails, and leaves the default boot path unchanged.

`BootSequence.RunAppLifecycleReadiness` is a host-tested helper only. It consumes `NativeAppLifecyclePlanResult`, emits `APP_LIFECYCLE_READY` only when invocation metadata and a bounded app-log sink are ready, emits `APP_LIFECYCLE_NOT_READY status=<status> invocation=<status> log=<status>` when a lifecycle prerequisite fails, and leaves the default boot path unchanged.

`BootSequence.RunAppPackageStagingReadiness` is a host-tested helper only. It
consumes `NativeAppPackageStagingReadinessResult`, emits
`APP_PACKAGE_STAGING_READY` only when already-produced manifest/package,
descriptor, staging, executable-memory, and invocation readiness all agree, and
emits `APP_PACKAGE_STAGING_NOT_READY ...` with nested status and byte metadata
when blocked. It leaves the default boot path unchanged.

`BootSequence.RunAppSelectionReadiness` is a host-tested helper only. It consumes `NativeManifestLoadResult`, `OsPackageParseResult`, `NativeAppLaunchPlanResult`, and `NativeAppLaunchDescriptorResult`, emits `APP_SELECTION_READY` only when all app-selection prerequisites are ready, emits `APP_SELECTION_NOT_READY status=<status> manifest=<status> package=<status> launch=<status> descriptor=<status> packageFile=<status> descriptorPackage=<status>` when any prerequisite fails, and leaves the default boot path unchanged. `NativeAppSelectionLoader` can now compose those prerequisite results from caller-buffer GPT ESP manifest/package reads in host tests, but it is not called by the default boot path.

`BootSequence.RunAppSelectionPreflightReadiness` is a host-tested helper only. It consumes `VirtioBlockNativeAppSelectionPreflightResult`, emits `APP_SELECTION_PREFLIGHT_READY` only for an initialized successful preflight, emits `APP_SELECTION_PREFLIGHT_NOT_READY status=<status> initialized=<true|false> preflight=<status> readSession=<status> pipeline=<status> block=<status> scratch=<status> selection=<status> manifest=<status> packageLoad=<status> package=<status> launch=<status> descriptor=<status>` for default/uninitialized or failed preflight results, and leaves the default boot path unchanged.

`BootSequence.RunAppEntryReadiness` is a host-tested helper only. It consumes manifest-load, package-parse, launch-plan, selected descriptor, payload-mapping, executable-memory, and invocation-plan results, emits `APP_ENTRY_READY` only when all pre-entry prerequisites are ready, emits `APP_ENTRY_NOT_READY status=<status> manifest=<status> package=<status> launch=<status> descriptor=<status> mapping=<status> memory=<status> invocation=<status>` when any dependency fails, and leaves the default boot path unchanged.

`FirmwareServicesGuard` is host-tested and has an optional `BootSequence` simulation method. The default QEMU smoke path does not enable the simulation probe; the explicit simulation artifact does.

## Tests

Host tests assert:

- `BootSequencePlan` exposes the expected ordered marker bytes.
- `BootPhaseCatalog` exposes implemented and future phase metadata with non-empty preconditions and postconditions.
- Managed phase marker text matches the no-stdlib `BootSequencePlan` marker bytes for implemented phases.
- `BootSequence.Run` emits exactly the plan lines in order.
- `BootSequence.Run(BootHandoffRecord, ...)` emits `BOOT_HANDOFF_READY` before phase markers when validation passes.
- `BootSequence.Run(BootHandoffRecord, ...)` emits `BOOT_HANDOFF_FAIL` and stops before phase markers when validation fails.
- `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` emits `BOOT_MEMORY_MAP_ACCEPTED` after handoff validation and before phase markers when the normalized summary passes.
- `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` emits `BOOT_MEMORY_MAP_REJECTED` and stops before phase markers for missing or invalid normalized summaries.
- `EfiBootHandoffValidator` accepts valid owned-map handoffs and rejects invalid boot source/capabilities, memory-map ownership issues, and descriptor metadata issues.
- `EfiMemoryMapNormalizer` summarizes owned descriptor bytes and rejects invalid metadata or descriptor arithmetic issues.
- `BootMemoryRegionMaterializer` writes compact boot memory-region records from owned descriptor bytes and rejects destination exhaustion, invalid descriptor metadata, zero-page descriptors, and address/total arithmetic overflow.
- Handoff validation rejects missing required capabilities, source-buffer/owned-buffer aliasing, and descriptor counts above the current supported limit.
- Handoff validation accepts valid ACPI RSDP pointer provenance and rejects inconsistent ACPI RSDP metadata.
- UEFI memory-map result contracts preserve descriptor metadata and failure statuses.
- UEFI memory-map capture-helper tests prove owned-buffer copy success, missing firmware pointer rejection, missing owned-destination rejection, allocation-size overflow rejection, and `UefiMemoryMapCopyPlanStatus` propagation for rejected capture metadata.
- UEFI-gone guard behavior rejects firmware-backed services after the boundary mode changes.
- Platform configuration-table guard tests prove ACPI RSDP scans are allowed before UEFI-gone simulation and denied after simulated or marked firmware shutdown.
- Platform UEFI-gone simulation tests prove guarded console, memory-map, and configuration-table adapters are denied without touching their backing sources.
- UEFI-gone simulation emits `UEFI_GONE_SIM_*` markers and fails status-coded when the firmware mode is already exited.
- Serial diagnostics guard tests prove post-firmware diagnostics are blocked without owned serial and allowed only after owned serial readiness is explicit.
- ExitBootServices preflight tests prove the future firmware-shutdown path is blocked unless post-firmware diagnostics and the transition plan both pass, and prove blocked preflight leaves firmware services available.
- ExitBootServices live-wiring plan tests prove the intended call order and prove the plan stops before a firmware call while the call is disabled.
- ExitBootServices firmware-result tests prove success, stale-map-key retry mapping, missing function pointer, and guard denial remain status-coded before the live call is enabled.
- GDT descriptor tests prove kernel segment and TSS descriptor bytes are deterministic and invalid TSS metadata is rejected.
- M02-007 owned-GDT tests prove the exact 40-byte null/code/data/TSS layout,
  selectors, table limit, full canonical TSS base, TSS-limit bounds, bounded
  caller ownership, byte-level readback/corruption detection, default-result
  rejection, deterministic FNV-1a64 identity, and allocation-free pointer path.
  The host marker explicitly reports `guestEncoding=false`, `guestLgdt=false`,
  and `guestLtr=false`.
- TSS/IST plan tests prove `RSP0` and double-fault IST bytes are deterministic and invalid IST stack metadata is rejected.
- Fault-infrastructure readiness tests prove serial readiness is required before descriptor-plan readiness and each missing plan has a distinct status.
- Boot sequence fault-readiness helper tests prove `FAULT_INFRA_READY` and `FAULT_INFRA_NOT_READY status=<status>` marker behavior without changing the default `BootSequence.Run` marker order.
- IDT contract tests prove deterministic descriptor encoding and required fault-vector error-code metadata.
- IDTR descriptor tests prove a 4096-byte IDT table produces limit `4095`, packed 10-byte IDTR bytes are deterministic, and invalid base/table lengths are rejected.
- IDT table-plan tests prove the required early fault vectors can be bound into a 256-entry table, #DF requires a nonzero IST index, missing required vectors are rejected, and descriptor bytes can be written into caller-owned 4096-byte storage.
- Panic text-buffer tests prove caller-owned ASCII, decimal, hex, overflow, and fault-summary formatting contracts.
- Serial configuration tests prove COM1 defaults, divisor-latch calculation, deterministic initialization port/value planning, and status-coded invalid input handling.
- Port-I/O operation-plan tests prove deterministic `out dx, al` and `in al, dx` opcode bytes and status-coded invalid-input handling.
- Port-I/O stub-plan tests prove callable `Out8` and `In8` stub bytes end in `ret`, use the port-I/O operation plan, and handle invalid inputs without execution.
- M02-001 managed x64-stub strategy tests prove that one demonstrated port-I/O
  family selects the existing exact seven-byte `Out8` and five-byte `In8`
  plans, a generator is ineligible until a synthetic second independent family,
  publication remains write-then-execute, and permanent RWX is forbidden. This
  is an allocation-free host policy result only; it adds no boot phase,
  generator, page transition, or execution.
- M02-002 fixed-page tests prove the exact 4,096-byte safe/`Out8`/`In8` page,
  full-page FNV-1a64 and SHA-256 identities, exhaustive readback rejection,
  zero-reference no-stdlib pointer compile closure, separate exact-source
  `NOSTDLIB_EFI` host pointer execution, and a caller-owned state progression
  from materialized RW+NX metadata through readback to finalized RO+X metadata.
  M02-002 itself adds no `BootSequence` phase, EFI linkage, firmware-visible
  marker, live protection change, or execution.
- M02-017 integrates that exact page only in the existing default-off M01
  stage-three EFI path. Its source-bound QEMU/OVMF tuple materializes and reads
  back the page before the inherited temporary owned-root interval, executes
  only safe offset 0, requires cookie `0x4F534F4B`, restores the inherited
  state, and then emits the six-line M02 envelope ending in
  `portIoExecuted=false`. It adds no `BootSequence` phase. Offsets 16 and 32
  remain data-layout positions and forbidden execution targets.
- Descriptor-table instruction-plan tests prove deterministic `lidt [rip+disp32]`, `lgdt [rip+disp32]`, and `ltr ax` bytes and status-coded invalid-input handling.
- Descriptor-table install-stub tests prove callable `lidt`, `lgdt`, and `ltr` stubs preserve descriptor payloads/selectors and handle invalid inputs without execution.
- Descriptor-table install-readiness tests prove IDT/IDTR/GDT/TSS/IST and install-stub buffer prerequisites are aggregated before any future privileged install.
- Fault-probe stub tests prove invalid-opcode and null-write probe byte plans while keeping destructive probe toggles disabled by default.
- Executable-stub bundle tests prove stable COM1, descriptor-table, and fault-probe slot offsets can be planned without copying or executing bytes.
- Executable-stub bundle materializer tests prove planned diagnostic stub bytes can be copied into caller-owned payload or full memory-region buffers without page-permission changes or execution.
- Executable-stub page-transition tests prove planned materialized bytes can be checked against write-then-execute W^X policy and caller-owned page ranges before any page-permission API exists.
- Diagnostic stub materialization readiness tests prove `DIAGNOSTIC_STUBS_MATERIALIZED` and status-coded not-materialized marker behavior without changing the default `BootSequence.Run` marker order.
- Post-firmware diagnostics readiness tests prove `POST_FIRMWARE_DIAGNOSTICS_READY` and status-coded blocked marker behavior from `SerialDiagnosticsGuardResult` without changing the default `BootSequence.Run` marker order.
- Fault-entry stub-plan tests prove future `#UD` and `#PF` entry stubs can be encoded into caller-owned buffers with validated vector/error-code metadata without installation or execution.
- Fault-probe readiness tests prove disabled destructive defaults, probe stub emission, and executable-stub memory capacity are aggregated before any future controlled fault probe.
- COM1 backend-readiness tests prove serial configuration, port operation/stub plans, and executable-stub memory capacity are aggregated before any future port-I/O backend install.
- COM1 backend contract tests prove planned `Out8` and `In8` stub offsets can be resolved into future backend addresses without invoking function pointers or performing port I/O.
- M02-003 checked-function-pointer tests prove that a production value-only
  binder revalidates callable-plan metadata before casting the exact unmanaged
  `Out8` and `In8` signatures, invokes safe unmanaged host targets with the
  expected ports/value/return byte, and rejects unready, mismatched,
  out-of-payload, overlapping, and out-of-COM1-range inputs without invocation.
  This test surface is not part of the default `BootSequence` and does not
  execute privileged `in`/`out` instructions.
- The default-off M02-003 EFI binding profile compiles that production binder
  into the no-stdlib closure and consumes the accepted M02-017 result only
  after safe offset-zero execution and firmware-state restoration. Fresh
  QEMU/OVMF evidence places exactly one checked-binding readiness marker after
  `TEST_PASS m02.017` and before `TEST_PASS m01.stage3`. The helper never calls
  the binding's invoke methods, so offsets 16/32 remain unexecuted and the
  marker reports `guestInvocation=false portIoExecuted=false`. This adds no
  `BootSequence` phase and does not alter the default probe path.
- The default-off M02-004 EFI profile consumes that checked binding only after
  the inherited M01 restoration audits, invokes the corrected Windows x64
  `Out8` stub exactly seven times for the 115200 8N1 COM1 configuration, and
  emits one `CLROS_M02_004_READY ... guestPortIo=true guestIn8=false` marker
  before `TEST_PASS m01.stage3`. The current cumulative QEMU profile observed
  all 50 ordered and unique markers and no forbidden marker. The marker remains
  UEFI-serial output; there is no new `BootSequence` phase, `In8` polling,
  owned-backend payload write, or post-`ExitBootServices` behavior.
- M02-005 adds an allocation-free production writer that accepts only the
  checked `Com1OwnedPortIoBackend`, repeats the accepted seven-write COM1
  configuration, polls transmit readiness through `In8(0x3FD)`, and emits
  exactly `OWNED_COM1_READY\r\n` through `Out8(0x3F8)`. Host tests currently
  execute safe unmanaged callbacks and prove exact port/byte order, delayed
  readiness, timeout, invalid-limit, unready-binding, default-result, and
  no-firmware-fallback behavior. The separately claimed default-off EFI lane
  links the writer and calls it once after M02-004. A development QEMU/OVMF run
  observed the standalone owned marker and separate detailed evidence marker
  exactly once in the 52-marker cumulative order before `TEST_PASS m01.stage3`,
  with `guestPortIo=true`, `guestIn8=true`, and
  `postExitBootServices=false`. The exact-head implementation and tracker
  closure are merged. This adds no `BootSequence` phase and proves no output
  after firmware exit.
- M02-006 adds a bounded allocation-free fault-record formatter and reporter
  that accepts only caller-owned scratch and the checked
  `Com1OwnedPortIoBackend`. It formats the complete `OS_SMOKE_FAIL` record
  before any I/O, configures COM1, polls THRE through `In8(0x3FD)`, and writes
  each byte through `Out8(0x3F8)` without a firmware fallback. Host callbacks
  prove exact bytes/order, delayed readiness, timeout, invalid-input,
  short-buffer atomicity, status/default, unmanaged-layout, and allocation-free
  behavior. The host marker explicitly reports `guestPortIo=false`. The
  separately claimed default-off EFI integration links the no-stdlib pointer
  core and invokes only its harmless readiness emitter exactly once after the
  accepted M02-005 pair and before `TEST_PASS m01.stage3`. A fresh development
  QEMU/OVMF run observed the direct M02-006 marker and distinct UEFI evidence
  marker exactly once in the 54-marker cumulative order, with seven
  configuration writes, 127 first-ready polls, `guestPortIo=true`,
  `guestIn8=true`, `firmwareFallback=false`, and
  `postExitBootServices=false`. This is not a default `BootSequence` phase,
  does not trigger a fault or panic, and proves no output after firmware exit.
  The exact-head implementation, integration, independent review, tracker
  closure, and manifest closure are merged.
- M02-007 adds a 40-byte caller-owned GDT encoder with a pointer-first
  no-stdlib core and exact readback verification. It fixes the descriptor order
  as null, 64-bit ring-zero code, ring-zero data, and available 64-bit TSS;
  validates canonical TSS metadata before mutation; and never touches
  `BootSequence`, `Probes`, `EfiEntry`, or `OS.csproj` in its implementation
  lane. A separately reviewed default-off EFI lane executed the same encoder and
  exact readback under fresh QEMU/OVMF evidence before the implementation and
  manifest closure merged. The observed guest marker remains pre-firmware-exit
  and explicitly reports no `lgdt` or `ltr`; it proves no active table, handler,
  fault path, or post-`ExitBootServices` diagnostics.
- M02-008 adds a host-tested, allocation-free guarded-IST arena allocator for
  double fault, page fault, and NMI. It deterministically places three adjacent
  guard-plus-stack reservations, fixes IST indices 1/2/3, marks guards absent and
  stacks writable/non-executable in the result metadata, and binds allocation and
  release to an owner token plus monotonic generation. It rejects zero/default or
  noncanonical addresses, canonical-hole crossing, page misalignment, size
  overflow, end wrap, exhaustion,
  reserved-range overlap, RWX metadata, duplicate allocation, wrong/stale owners,
  invalid leases, and double release before mutation. It does not add a
  `BootSequence` phase, map pages, write TSS IST slots, install handlers, or prove
  guest behavior by itself. The separately owned, default-off EFI lane now has
  fresh real-QEMU/OVMF evidence for temporary six-page LoaderData allocation,
  layout validation, stack-only write/readback, lease release, and same-base
  `FreePages` before return. Exact-head independent review and the manifest
  closure are merged, so managed M02-008 is closed. The guest evidence still
  proves no guard mapping, TSS/IDT installation, fault execution, or real
  `ExitBootServices` transition.
- M02-009 tests prove the exact 168-byte GDTR/GDT/TSS context and independently
  decode all 2,987 bytes of a relocation-free Windows-x64 terminal stream. The
  audit covers `cli`, `lgdt`, selector-`0x18` `ltr`, `sgdt`, `str`, full GDTR/TR
  comparisons, the hardware-mutated `0x89`-to-`0x8B` writable TSS descriptor,
  inline COM1 THRE polling/writes, exact success/failure transcripts, and both
  `cli; hlt; jmp` loops. Boundary, overlap, mutation, frozen-hash, and
  zero-allocation tests remain host-only: they never invoke privileged bytes,
  perform port I/O, or change the boot order.
- M02-010 tests prove all 256 fixed 16-byte interrupt-entry slots and all 4,096
  page bytes. They cross-check the nine-vector hardware-error-code mask against
  `FaultVectorCatalog`, including `#SX` vector 30; prove that vectors 128 and
  255 use positive immediate-32 pushes; decode the first, middle, and final
  `rel32` target; mutate every byte; preserve a poison tail; reject every
  pointer, modeled-address, capacity, alignment, canonical-range, wrap,
  overlap, and displacement failure before mutation; compare pointer and
  `Span` output; prove default/POD and zero-allocation behavior; and compile the
  exact pointer source under `NOSTDLIB_EFI` with an empty `UIntPtr` shim. The
  aggregate host marker explicitly reports `guestMaterialization=false`,
  `stubExecution=false`, `idtInstalled=false`, and `faultTriggered=false`.
- M02-010 EFI-integration tests pin the exact frozen source, default-false
  property and symbol, 60-item/73-input closure, profile exclusions, one-page
  allocation/readback/cleanup flow, and absence of generated-slot invocation.
  The accepted source-bound QEMU/OVMF profile observed its exact five-record
  auxiliary subsequence once, reached the unchanged ordinary `OS_SMOKE_PASS`,
  and observed no failure record. This is not a new managed boot phase.
- M02-011 tests independently reconstruct all 256 x64 gate descriptors and all
  4,096 bytes at low and canonical high-half bases, cross-check selector
  `0x0008` and IST3/IST1/IST2 against the existing owned GDT and guarded-IST
  contracts, and prove 256 unique 64-bit handlers inside the M02-010 page with
  zero copied firmware gates. They mutate every byte, preserve a poison tail,
  reject all applicable pointer/model/overlap
  failures before mutation, compare pointer and `Span` output, prove zero
  managed allocations, and compile the exact pointer source under
  `NOSTDLIB_EFI` without BCL references. The stable normalized fingerprint is
  `8514F42EBDFC5D55`; the host marker explicitly reports
  `guestPopulation=false`, `idtrLoaded=false`, `stubExecution=false`, and
  `faultTriggered=false`.
- Fault-entry frame layout tests prove future interrupt-stub storage can round-trip formatter-compatible page-fault and invalid-opcode frames, reject inconsistent error-code metadata, reject undersized buffers, and reject unknown vectors.
- ExitBootServices transition-boundary tests prove success and blocked markers while leaving default boot markers unchanged.
- Early diagnostics readiness tests prove serial backend and descriptor-table readiness are aggregated into stable boot markers while leaving default boot markers unchanged.
- Controlled fault-probe readiness tests prove early diagnostics and destructive-probe readiness are both required before `CONTROLLED_FAULT_PROBES_READY` can be emitted, while leaving default boot markers unchanged.
- CPU fault panic readiness tests prove caller-owned fault-entry frame bytes can
  be decoded and flushed through the panic serial reporter into an
  `OS_SMOKE_FAIL` line, and prove `CPU_FAULT_PANIC_READY` /
  `CPU_FAULT_PANIC_NOT_READY ...` marker behavior while leaving default boot
  markers unchanged.
- App-package readiness tests prove manifest load, package parse, and launch-plan results are aggregated into `APP_PACKAGE_READY` or status-coded `APP_PACKAGE_NOT_READY` markers while leaving default boot markers unchanged.
- App launch descriptor tests prove manifest launch-plan data and package file lookup can be aggregated into `APP_LAUNCH_READY` or status-coded `APP_LAUNCH_NOT_READY` markers while leaving default boot markers unchanged.
- App payload readiness tests prove selected descriptor and payload-header results can be aggregated into `APP_PAYLOAD_READY` or status-coded `APP_PAYLOAD_NOT_READY` markers while leaving default boot markers unchanged.
- App executable mapping readiness tests prove selected payload range-plan results can be aggregated into `APP_EXEC_MAPPING_READY` or status-coded `APP_EXEC_MAPPING_NOT_READY` markers while leaving default boot markers unchanged.
- App executable memory readiness tests prove selected executable-memory plan results can be aggregated into `APP_EXEC_MEMORY_READY` or status-coded `APP_EXEC_MEMORY_NOT_READY` markers while leaving default boot markers unchanged.
- App invocation readiness tests prove selected invocation-plan results can be aggregated into `APP_INVOCATION_READY` or status-coded `APP_INVOCATION_NOT_READY` markers while leaving default boot markers unchanged.
- App lifecycle readiness tests prove invocation-plan and app-log readiness can be aggregated into `APP_LIFECYCLE_READY` or status-coded `APP_LIFECYCLE_NOT_READY` markers while leaving default boot markers unchanged.
- App package staging readiness tests prove
  `NativeAppPackageStagingReadinessResult` can be aggregated into
  `APP_PACKAGE_STAGING_READY` or status-coded `APP_PACKAGE_STAGING_NOT_READY`
  markers while leaving default boot markers unchanged.
- App selection readiness tests prove manifest load, package parse, launch-plan, and launch-descriptor results can be aggregated into `APP_SELECTION_READY` or status-coded `APP_SELECTION_NOT_READY` markers while leaving default boot markers unchanged.
- Virtio-blk app-selection preflight readiness tests prove default/uninitialized, selection-loader failure, and successful preflight results can be aggregated into `APP_SELECTION_PREFLIGHT_READY` or status-coded `APP_SELECTION_PREFLIGHT_NOT_READY` markers without changing the default boot path.
- App entry readiness tests prove the full manifest/package/descriptor/payload-mapping/executable-memory/invocation dependency chain can be aggregated into `APP_ENTRY_READY` or status-coded `APP_ENTRY_NOT_READY` markers while leaving default boot markers unchanged.

QEMU smoke confirms the firmware-visible order in `artifacts/logs/qemu-smoke.log`, including `BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT 102`, `BOOT_ACPI_RSDP_HANDOFF_READY`, and `UEFI_ACPI_RSDP_READY` before `TEST_START boot.sequence`. The simulation variant confirms `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS` in `artifacts/logs/qemu-smoke-sim.log`; the fresh-map probe variant confirms `UEFI_EBS_FRESH_MAP_READY` and `UEFI_EBS_CALL_SKIPPED` in `artifacts/logs/qemu-smoke-ebs-probe.log`; the ACPI physical discovery probe variant confirms `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY` in `artifacts/logs/qemu-smoke-acpi.log`.

## Known Limitations

The current order is still a marker contract, not a full hardware boot sequence. It does not prove owned drivers, interrupts, page tables, storage, networking, or hosted workload launch.

The M02-009 byte contract is not a firmware-visible `BootSequence` phase. Its
host marker correctly reports the live properties as false for the isolated
leaf, while the separately merged default-off EFI profile now provides the
accepted source-bound guest `lgdt`/`ltr`, owned COM1, terminal-halt, and
QEMU/OVMF evidence. That terminal profile remains pre-`ExitBootServices`, runs
after the ordinary smoke-success marker, and proves no IDT installation, live
fault handling, post-firmware operation, or change to the default order.

The M02-010 page is likewise not a firmware-visible `BootSequence` phase. The
leaf's exact host marker reports all four live properties as false. The
separate default-off EFI profile now supplies bounded QEMU/OVMF evidence for
guest page allocation, materialization, production readback, and release before
memory-map capture, but it does not publish executable memory, enter a slot or
common target, install an IDT/IDTR/IST, normalize a live CPU frame, or trigger a
fault. Its auxiliary profile cannot be promoted to a change in the default
boot order.

The M02-011 IDT page is also not a firmware-visible `BootSequence` phase. Its
host marker proves deterministic caller-owned bytes and readback only. The leaf
does not link into EFI, allocate guest storage, publish executable slots, load
IDTR, activate GDT/TSS/IST state, enter a slot/common target, or trigger a
fault. Real QEMU/OVMF population evidence requires a separately owned
integration lane; even that future population-only lane will not prove IDTR
load or interrupt delivery.

The `BOOT_HANDOFF_READY`, `UEFI_MEMORY_MAP_NORMALIZED`, `BOOT_MEMORY_MAP_ACCEPTED`, `BOOT_MEMORY_REGIONS_READY`, `BOOT_MEMORY_REGION_COUNT`, `BOOT_ACPI_RSDP_HANDOFF_READY`, and `UEFI_ACPI_RSDP_READY` markers are now QEMU-proven through the live EFI artifact. The live EFI artifact still emits the allocation-free `BootSequencePlan` directly after handoff validation, normalization, acceptance, region materialization, and ACPI RSDP scan; it does not execute the managed `BootSequence.Run(BootHandoffRecord, EfiMemoryMapSummary, ...)` overload.

`APP_PACKAGE_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove a disk-backed package load, and does not prove native-tier or hosted .NET app launch.

`APP_LAUNCH_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove a disk-backed package load, and does not prove native-tier or hosted .NET app launch.

`APP_PAYLOAD_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove a disk-backed package load, complete executable validation, executable mapping, or native-tier or hosted .NET app launch.

`APP_EXEC_MAPPING_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove disk-backed package load, executable page allocation, W^X/NX enforcement, relocation, or native-tier or hosted .NET app launch.

`APP_EXEC_MEMORY_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove disk-backed package load, executable page allocation, live W^X/NX enforcement, payload copying, relocation, or native-tier or hosted .NET app launch.

`APP_INVOCATION_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove disk-backed package load, stack allocation, stack switching, thread creation, final app ABI, relocation, or native-tier or hosted .NET app launch.

`APP_LIFECYCLE_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove disk-backed package load, supervisor state, live stdout/stderr routing, restart policy, exit-code capture, stack allocation, thread creation, or native-tier or hosted .NET app launch.

`APP_PACKAGE_STAGING_READY` is managed host-test planning only. It is not part
of the firmware-visible marker order, does not prove live virtio-blk reads,
boot-time package loading, boot-owned staging memory, executable page mapping,
permission changes, relocation, or native-tier or hosted .NET app launch.

`APP_SELECTION_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove a disk-backed package load, and does not prove native-tier or hosted .NET app launch.

`APP_SELECTION_PREFLIGHT_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove live virtio-blk reads, boot-time package loading, boot-owned scratch allocation, native-tier app execution, or hosted .NET app launch.

`APP_ENTRY_READY` is managed host-test planning only. It is not part of the firmware-visible marker order, does not prove disk-backed package load, executable page allocation, W^X/NX enforcement, stack allocation, stack switching, final app ABI dispatch, relocation, or native-tier or hosted .NET app launch.

## Next Steps

1. Wire the transition marker helper only after the live EFI path can perform a real `ExitBootServices` call and preserve diagnostics afterward.
2. Add an owned serial diagnostics phase only after a real serial writer exists.
3. Preserve the separately owned M02-005 and M02-006 pre-firmware direct COM1
   marker proofs, keep their UEFI evidence lines distinct from the direct
   transport, and do not treat either as the future post-firmware diagnostic
   path.
4. Preserve the accepted default-off M02-010 aligned LoaderData-page
   materialization, readback, release, and distinct guest marker. Add M02-011
   guest IDT population/readback only through a separately claimed default-off
   EFI lane. Stub/common-entry execution, executable publication, IDTR load,
   active IST routing, and fault triggering remain later owned boundaries.
5. Wire the early diagnostics readiness helper only after generated stubs can be copied to executable memory and safely invoked under W^X policy.
6. Wire the controlled fault-probe readiness helper only after early diagnostics can survive generated-stub execution and fault handling.
7. Wire the app-selection readiness helper only after live block-device package reads can supply the manifest/package/descriptor prerequisites.
8. Add probe toggles for destructive fault tests.
9. Promote future placeholder phases from documented ledger entries to emitted phases only when the required subsystem evidence exists.
10. Prevent any subsystem from bypassing `BootSequence`.
