# EFI Build

## Current Status

An explicit EFI NativeAOT project shell exists at `src/OS/OS.csproj`. It is
included in `CLROS.slnx`, but its directory-local rules keep the freestanding
no-stdlib build explicit and separate from ordinary managed runtime policy.

As of 2026-07-27, this host has the required Visual Studio C++ toolchain,
Windows SDK, QEMU, and OVMF installed. `scripts/build-efi-shell.ps1` publishes a
NativeAOT x64 PE with EFI application subsystem and stages it as
`artifacts/efi/BOOTX64.EFI`.

The most recent staged artifact is an explicit
`M01_LOADED_IMAGE_STACK_PROBE` build. It contains the M01-021 loaded-image,
page-allocation, map/ledger, stack-probe, and owned-stack-switch wiring, but it
has build and PE-verification evidence only. The accepted QEMU/OVMF Gate 2
evidence remains the earlier default artifact: it emits firmware-visible
markers from `EfiMain`, captures and owns the UEFI memory map, validates the
handoff, normalizes and materializes the map, and emits the ordered boot marker
plan through the raw-image path.

## Implemented Behavior

- The project uses `NoStdLib`, `PublishAot`, `EntryPointSymbol=EfiMain`, and `LinkerSubsystem=EFI_APPLICATION`.
- `EfiEntry` exports `EfiMain`.
- `EfiEntry` contains one opt-in `M01_LOADED_IMAGE_STACK_PROBE` adapter. It is
  entered only after the ordinary owned handoff is available; the all-false
  build retains the previous path and marker order.
- `EfiMain` uses minimal UEFI system-table, console-output, boot-services, and Serial I/O protocol structs.
- `src/OS/Boot/EfiConsoleAdapter.cs` is a no-stdlib byte-output adapter for the live EFI artifact. It encapsulates UEFI console and Serial I/O fallback writes without using managed interface dispatch.
- `OS.Kernel` has a host-tested `IPlatformConsole` guard contract for future firmware-console marker writes. The live EFI artifact now has a separate no-stdlib adapter boundary, but it is still not a full `IPlatformConsole` implementation.
- `EfiMain` delegates UEFI `GetMemoryMap`, firmware-buffer slack sizing, `AllocatePool`, retry capture, copy-plan validation, and descriptor-byte copy into a fixed static OS-owned buffer to the source-linked no-stdlib-safe `UefiMemoryMapCapture` helper. `EfiEntry` records the returned map key, descriptor size/version, map size, source buffer size, owned buffer size, and descriptor count in a value-type snapshot.
- `EfiMain` emits `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>` after a successful capture.
- `EfiMain` emits `UEFI_MEMORY_MAP_OWNED` after the descriptor bytes are copied into the static owned buffer.
- `EfiMain` populates a source-linked no-stdlib-safe `BootHandoffRecord` with source and owned memory-map metadata after successful copy.
- `EfiMain` validates the live `BootHandoffRecord` through the source-linked no-stdlib-safe `EfiBootHandoffValidator` and emits `BOOT_HANDOFF_READY` before `TEST_START boot.sequence`.
- `EfiMain` normalizes a compact live owned-map summary through the source-linked no-stdlib-safe `EfiMemoryMapNormalizer` and emits `UEFI_MEMORY_MAP_NORMALIZED` before `TEST_START boot.sequence`.
- `EfiMain` materializes compact live owned-map region records through the source-linked no-stdlib-safe `BootMemoryRegionMaterializer` and emits `BOOT_MEMORY_REGIONS_READY` plus `BOOT_MEMORY_REGION_COUNT <count>` before ACPI RSDP handoff and `TEST_START boot.sequence`.
- `OS.Kernel` has a host-tested `UefiMemoryMapCopyPlan` contract that validates the copy from firmware-owned memory-map buffer to OS-owned storage before the EFI shell copies bytes.
- `OS.Kernel` has a host-tested `UefiMemoryMapCapture` helper that owns the no-stdlib EFI memory-map capture/copy mechanics and reports copy-plan statuses for rejected metadata.
- `OS.Kernel` has a host-tested `EfiBootHandoffValidator` contract that mirrors the live EFI numeric validation path without string formatting or managed allocation requirements.
- `OS.Kernel` has a host-tested `EfiMemoryMapNormalizer` contract for live no-stdlib summary normalization without arrays or `Span<T>`.
- `OS.Kernel` has a host-tested `BootMemoryRegionMaterializer` contract for live no-stdlib memory-region materialization without arrays, managed allocation, or exception handling.
- `OS.Kernel` has a host-tested `UefiMemoryDescriptorReader` contract that decodes owned raw descriptor bytes with 40-byte and 48-byte descriptor strides into caller-provided storage.
- `EfiMain` source-links `BootSequencePlan.cs` and emits the ordered `boot.sequence` marker plan without referencing the normal `OS.Kernel` assembly.
- Firmware console output is preferred. UEFI Serial I/O is used only as a fallback to avoid duplicate serial output when OVMF maps console output to the serial sink. Both paths are now called through `EfiConsoleAdapter`.
- `Runtime/MinimalRuntime.cs` contains the compiler-required primitive stubs,
  throw helpers, memory helpers, fail-fast type-cast placeholders, and
  reverse-P/Invoke exports needed to produce the EFI PE. It also contains the
  minimal compiler-recognized `UnmanagedCallersOnlyAttribute` contract and the
  fail-closed `RhpStackProbe` dispatch bridge required by the M01-021 opt-in
  build.
- `scripts/build-efi-shell.ps1` runs the explicit publish command and writes a
  log under `artifacts/logs/`. Before `dotnet publish`, its preflight resolves
  the MSVC x64 `link.exe`, Windows SDK UM x64 `kernel32.lib`, selected
  ILCompiler and NativeAOT runtime pack, QEMU x64 executable, `qemu-img`, and
  OVMF code image. Successful discovery emits exact quoted paths plus linker,
  SDK, package, and executable versions; OVMF evidence includes byte length
  and SHA-256. The stable lines are `EFI_PREFLIGHT_START`,
  `EFI_PREFLIGHT_MSVC_LINKER`, `EFI_PREFLIGHT_WINDOWS_SDK`,
  `EFI_PREFLIGHT_ILCOMPILER`, `EFI_PREFLIGHT_NATIVEAOT_RUNTIME_PACK`,
  `EFI_PREFLIGHT_QEMU`, `EFI_PREFLIGHT_QEMU_IMG`,
  `EFI_PREFLIGHT_OVMF`, and finally `EFI_PREFLIGHT_OK`.
- Preflight inputs are injectable through `-MsvcLinkerSearchRoot`,
  `-WindowsSdkLibSearchRoot`, `-VsWherePath`, `-IlCompilerPath` or
  `-IlCompilerSearchRoot`, `-NativeAotRuntimePackPath` or
  `-NativeAotRuntimePackSearchRoot`, `-QemuPath` or `-QemuSearchRoot`,
  `-QemuImgPath` or `-QemuImgSearchRoot`, and `-OvmfCodePath` or
  `-OvmfSearchRoot`. An explicitly requested missing path does not fall back
  to a host default. `-PreflightOnly` stops after the checks without publishing
  or emitting `TEST_PASS efi.shell.publish`.
- `scripts/build-efi-shell.ps1 -EnableUefiGoneSimulationProbe` defines `UEFI_GONE_SIMULATION_PROBE`, prints `EFI_PROBE UEFI_GONE_SIMULATION`, and publishes a variant that emits `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `scripts/build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe` defines `ACPI_PHYSICAL_DISCOVERY_PROBE`, prints `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`, and publishes a variant that reads firmware-provided ACPI physical addresses before `ExitBootServices`, validates RSDP/root SDT/MCFG bytes into fixed scratch storage, and emits `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.
- On success, the publish script copies `src/OS/bin/Release/net10.0/win-x64/publish/OS.exe` to `artifacts/efi/BOOTX64.EFI`.
- `scripts/test-efi-artifact.ps1` parses the staged PE directly and rejects
  missing, empty, truncated, malformed, non-AMD64, non-PE32+, non-EFI,
  zero-entrypoint, non-executable-entrypoint, virtual-only-entrypoint, import,
  bound-import, IAT, delay-import, and missing-`EfiMain`-contract cases. It
  proves binary entrypoint properties and the checked-in build/source binding;
  a stripped release PE does not independently preserve the `EfiMain` symbol
  name.

### M01 reviewed EFI source closure (host-only)

M01-020 promotes the already host-reviewed M01 leaf and coordinator sources
into the canonical `src/OS/OS.csproj` no-stdlib compilation. The project now
has exactly 41 direct compile items: 38 linked `OS.Kernel` sources and the
three local EFI sources `EfiConsoleAdapter.cs`, `EfiEntry.cs`, and
`MinimalRuntime.cs`. There are no source globs, conditional future slots,
alternate EFI projects, generated compile items, or managed project/assembly
references.

The issue-reviewed set has these 28 explicit entries:

- `TypedPhysicalVirtualDeviceAddressPODWrappers.cs`
- `BootReservedRange.cs`
- `BootReservationTableValidator.cs`
- `EfiLoadedImageRange.cs`
- `EfiLoadedImageRangeCapturePlan.cs`
- `OwnedBootStackLayout.cs`
- `OwnedBootStackSwitchPlan.cs`
- `BootPageAllocatorCoreContracts.cs`
- `BootPageAllocatorStateMaterializer.cs`
- `BootReservationSubtractor.cs`
- `BootPageAllocatorMutation.cs`
- `PageProtection.cs`
- `PageCachePolicy.cs`
- `X64PageTableEntry.cs`
- `X64BootIdentityMappingPlan.cs`
- `X64Cr3LoadStubPlan.cs`
- `X64PagingModePreflightStubPlan.cs`
- `X64ReversibleCr3TransitionStubPlan.cs`
- `X64PageMap.cs`
- `X64TlbInvalidationPlan.cs`
- `X64InvlpgStubPlan.cs`
- `X64ProtectionProbeStubPlan.cs`
- `PageZeroMappingPolicy.cs`
- `M01LoadedImageStackIntegration.cs`
- `M01LiveAllocatorIntegration.cs`
- `M01LivePagingIntegration.cs`
- `M01OwnedCr3CompositeTransition.cs`
- the previously linked `BootSequencePlan.cs`

Three explicit prerequisite sources complete that set:
`BootHandoffRecord.cs`, `BootMemoryRegionMaterializer.cs`, and
`UefiMemoryType.cs`. The original M01-020 catalog omitted
`UefiMemoryType.cs`. A direct exact-list compile failed with 18 unresolved
`UefiMemoryType` diagnostics; adding only that prerequisite made the same
no-stdlib build pass. The project, preflight marker, issue evidence, and
durable backlog record therefore preserve this discovered prerequisite rather
than repeating the incomplete 28-file claim.

`scripts/build-efi-shell.ps1` validates the direct XML and evaluated MSBuild
closure for both workload profiles and the caller-selected `Debug` or `Release`
configuration before tool discovery. Canonicalized XML fingerprints bind the
complete EFI project and root `Directory.Build.props`/`Directory.Build.targets`
shapes, while structural checks produce narrower diagnostics. The preflight
rejects missing, extra, duplicate, conditional, expanded, imported,
target-injected, target-time-mutated, build-customization-injected,
analyzer/package-injected, or outside-repository sources. It also checks exact
`NOSTDLIB_EFI` exclusions; legacy-probe property propagation; active no-runtime
source for managed strings, arrays, records, classes, aliases, attributes, and
anonymous objects; the three-allocation/map/arena contracts; callback
exclusion; and the owned-CR3 restoration/canary contracts. Successful
validation emits:

```text
EFI_PREFLIGHT_M01_SWITCHES status=ok loadedImageStack=<true|false> liveAllocator=<true|false> ownedPaging=<true|false>
EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=28 prerequisites=3 compileItems=41 prerequisiteGap=UefiMemoryType
```

The opt-in build switches form a strict dependency chain:

```text
EnableM01LoadedImageStackProbe
  -> EnableM01LiveAllocatorProbe
    -> EnableM01OwnedPagingProbe
```

All three default to `false`. Stack-only, stack-plus-allocator, and all-three
configurations are valid. Allocator without stack and paging without allocator
fail both preflight and MSBuild validation. The build script passes enabled
values as exact `-p:EnableM01...=true` properties and the project maps them to
`M01_LOADED_IMAGE_STACK_PROBE`, `M01_LIVE_ALLOCATOR_PROBE`, and
`M01_OWNED_PAGING_PROBE`.

Focused host evidence uses:

```text
TEST_START m01.020
CLROS_M01_020_READY
TEST_PASS m01.020
```

This remains M01-020 compile-closure and build-switch evidence. At that issue's
boundary the linked sources were not invoked by `EfiEntry`; M01-021 now invokes
the loaded-image/owned-stack subset only under its explicit probe symbol. All
switches remain disabled in the ordinary build, and no boot order or
completion-gate status changes. M01-020 itself does not prove firmware page
allocation, stack switching, CR3 load, live PTE/TLB mutation, QEMU/OVMF
execution, owned diagnostics, or `ExitBootServices`.

### M01 loaded-image and owned-stack EFI wiring (build evidence)

`scripts/build-efi-shell.ps1 -EnableM01LoadedImageStackProbe` maps the explicit
build property to `M01_LOADED_IMAGE_STACK_PROBE`. In that configuration,
`EfiEntry` uses the incoming EFI image handle to capture and validate the
loader-owned image range, then performs exactly two `AllocateAnyPages` calls:

- one page with memory type `EfiLoaderCode`; and
- 34 pages with memory type `EfiLoaderData`, divided into one reserved guard
  page followed by 33 usable stack pages.

The resulting sizes are 4,096 code bytes, 139,264 data-arena bytes, and
135,168 usable-stack bytes. Status, type, nonzero address, alignment, checked
end, image/allocation non-overlap, map membership, and exact arena accounting
are validated before the adapter can transfer. The loaded image remains
loader-owned and is never freed by the adapter.

After both allocations, the adapter recaptures the firmware map into static
source storage, validates and copies it into the existing static owned-map
destination, requires a changed opaque map key and the exact next CLROS
generation, and then builds a sorted/disjoint four-record ledger for
`LoadedImage`, `BootStackSwitchStub`, `BootStackGuard`, and `BootStack`.
Static metadata already contained by the loaded image is intentionally not
duplicated. A pre-transfer failure clears an installed stack-probe target and
attempts `FreePages` for the data arena and then the code page. Allocation
ownership follows the returned `EFI_STATUS`, so an EFI-success/address-zero
allocation is still reclaimed after the CLROS address policy rejects it.
Loaded-image protocol and map-recapture failures preserve the exact firmware
status independently from the stable adapter status.

The single code page carries two disjoint first-party plans: the existing
14-byte stack-switch plan at byte offset 16 and a 27-byte `RhpStackProbe` page
plan at byte offset 64. Both are copied and read back before use. The latter
starts with an indirect-branch landing prefix, rounds the current stack down to
a page, touches pages downward in 4 KiB increments until it reaches the target
held in `R11`, and returns without modifying `R11`. The runtime export fails
closed until the page plan is installed. The complete code page is first
filled with `0xCC` and read back before either executable slice is written.

The live-facing adapter calls only `PrepareFromStaticScratch`. It validates an
exact 3,264-byte, 8-byte-aligned, initially zero scratch aggregate inside the
loaded image and proves that aggregate disjoint from every caller-owned input
range before its first write. The shared low-stack core places large POD
diagnostics in that static storage and returns an 8-byte status/checkpoint
value. The legacy rich host API is a wrapper over the same core.

This special ABI was independently implemented after reviewing the official
MIT-licensed
[.NET runtime v10.0.8 `MiscStubs.asm`](https://github.com/dotnet/runtime/blob/v10.0.8/src/coreclr/nativeaot/Runtime/amd64/MiscStubs.asm).
The emitted binary
disassembly, not an assumption about the C# source, is binding. Manual
disassembly of the accepted artifact showed a frameless `RhpStackProbe`
bridge: it loads and tests the installed target through `RAX`, follows the
compiler's aligned direct branch, tail-jumps through `RAX`, and loops when no
target is installed. The valid direct path writes neither `RSP`, `R11`, nor
the argument registers before dispatch. This is binary-specific evidence and
must be repeated whenever the source, ILCompiler, runtime pack, linker inputs,
or generated artifact changes.

The stack-switch target calls a directly bound
`[UnmanagedCallersOnly]` static continuation through
`delegate* unmanaged`. The continuation validates the exact static handoff,
converts the observed raw stack pointer through the typed UEFI boundary, and
calls `ObserveContinuationRspFromStaticState`. Apart from the initial
`TEST_START m01.stage1`, all canonical success/checkpoint markers are emitted
retrospectively only after that observation proves `RSP` is contained by the
33-page usable-stack range. Failure uses two allocation-free numeric lines:

```text
TEST_FAIL m01.stage1 status=<number> adapter=<number> checkpoint=<number> free_data=<number> free_code=<number>
M01_STAGE1_FAIL status=<number> adapter=<number> checkpoint=<number> free_data=<number> free_code=<number>
```

The accepted opt-in build on 2026-07-27 produced a 131,584-byte PE32+ AMD64 EFI
application. `scripts/test-efi-artifact.ps1` reported executable/file-backed
`.managed` entrypoint RVA `0x00010BD0`, same-build `EfiMain` association,
absent ordinary/bound/IAT/delay-import directories, and
`TEST_PASS efi.artifact.verify`. Artifact SHA-256 is
`42DC4647AC3EC1B0CAF2A86863834C186E8A0E324DBEE37B879FC401F1E7930F`;
the NativeAOT object, linker-map, and response hashes are
`D3E31151A51ABE783F2638674C8A7A897BDE8A4064A04FA9D9E29446896F8F22`,
`81BABEFFD51033B42014AD5356C11F00A81C503BE1BA50EB668EDD0903AE8323`,
and
`0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F`.

This proves a fresh opt-in NativeAOT build, the PE contract, helper linkage,
the source-wiring contract, and the binary-specific bridge shape. Generated
disassembly places the conservative deepest direct pre-transfer chain at
`0xA268`, or 41,576 bytes, which is 23,960 bytes below the 64 KiB issue budget
and replaces the former 135,632-byte blocker. The emitted bridge remains
frameless, and a post-unpublish call-graph audit found no path that can invoke
`RhpStackProbe` after its target is cleared. The measurement must be repeated
after a relevant compiler, runtime-pack, source, or code-generation change.

No QEMU/OVMF execution or serial evidence was accepted. This implementation does not
prove a live firmware allocation, map recapture, stub or stack-probe execution,
`RSP` transfer/containment, a faulting guard, RW-to-RX transition, NX/W^X
enforcement, `ExitBootServices`, or any completion-gate advancement. M01-022
owns real guest proof and failure classification. This issue's accepted
build/source evidence does not change a completion-gate state.

## Unsupported Behavior

The project is not yet a functional kernel. `ExitBootServices`, owned drivers,
a full firmware abstraction layer, reservation-subtracted allocator-owned
memory state, post-firmware physical mappings, and panic-safe diagnostics are
pending. `UefiExitBootServicesLiveWiringPlan` documents and host-tests the
future live call order, and `PlatformFirmwareExitBootServicesResult` documents
the future firmware-call result mapping, but the live `EfiEntry` path keeps
firmware shutdown disabled until the owned stack/serial/data prerequisites,
typed call adapter, and real bounded stale-key retry are implemented and
proven.

## Tests

Expected commands:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\build-efi-shell.ps1 -PreflightOnly
.\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe
.\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe
.\scripts\build-efi-shell.ps1
.\scripts\test-efi-artifact.ps1
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT
.\scripts\build-efi-shell.ps1 -EnableUefiGoneSimulationProbe -EfiOutputPath artifacts/efi/BOOTX64-SIM.EFI -LogPath artifacts/logs/efi-shell-publish-sim.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-SIM.EFI -Image artifacts/images/os-sim.raw -SerialLog artifacts/logs/qemu-smoke-sim.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset uefi-gone
.\scripts\build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe -EfiOutputPath artifacts/efi/BOOTX64-ACPI.EFI -LogPath artifacts/logs/efi-shell-publish-acpi.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset acpi
```

Current evidence from 2026-05-21:

- `.\scripts\build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`.
- The script emitted `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- `.\scripts\run-qemu-smoke.ps1 ...` emitted `TEST_PASS qemu.smoke`.
- The simulation publish emitted `EFI_PROBE UEFI_GONE_SIMULATION` and `TEST_PASS efi.shell.publish`.
- The simulation QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring the `uefi-gone` marker preset.
- `artifacts/logs/qemu-smoke.log` contains `TEST_START efi.main`, `EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`, `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `TEST_START boot.sequence`, ordered `BOOT_PHASE` markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.
- After the no-stdlib console adapter was added, `scripts/build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`, and real QEMU smoke requiring `OS_SMOKE_PASS,EFI_CONSOLE_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-sim.log` additionally contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.

Current evidence from 2026-05-22:

- `.\scripts\build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`.
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED` emitted `TEST_PASS qemu.smoke`.
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_MEMORY_MAP_OWNED` and `BOOT_HANDOFF_READY`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY`.
- Host tests emitted `TEST_PASS uefi.descriptor.reader decodes 40-byte stride`, `TEST_PASS uefi.descriptor.reader decodes 48-byte stride`, `TEST_PASS uefi.descriptor.reader rejects invalid metadata`, and `TEST_PASS uefi.descriptor.reader reports source and destination capacity`.
- Host tests emitted `TEST_PASS efi.handoff.validator accepts owned memory-map handoff`, `TEST_PASS efi.handoff.validator rejects invalid boot source and capabilities`, `TEST_PASS efi.handoff.validator rejects memory-map ownership issues`, and `TEST_PASS efi.handoff.validator rejects descriptor metadata issues`.
- `.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `UEFI_MEMORY_MAP_NORMALIZED`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED`.
- Host tests emitted `TEST_PASS efi.memorymap.normalizer summarizes owned descriptors`, `TEST_PASS efi.memorymap.normalizer rejects invalid metadata`, and `TEST_PASS efi.memorymap.normalizer rejects descriptor arithmetic issues`.
- Host tests emitted `TEST_PASS uefi.memorymap.capture helper rejects missing firmware pointers` and `TEST_PASS uefi.memorymap.capture helper rejects missing owned destination`.
- Host tests emitted `TEST_PASS platform.firmware exitboot reports success`, `TEST_PASS platform.firmware exitboot reports stale map key`, and `TEST_PASS platform.firmware exitboot rejects unavailable services`.
- Focused host tests emitted `TEST_PASS uefi.memorymap.capture helper copies owned map`, `TEST_PASS uefi.memorymap.capture helper reports allocation overflow`, and `TEST_PASS uefi.memorymap.capture helper reports copy-plan status`.
- Real QEMU smoke requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_READY,UEFI_MEMORY_MAP_DESCRIPTOR_COUNT,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY` emitted `TEST_PASS qemu.smoke`.
- Host tests emitted `TEST_PASS boot.memory regions materializes descriptors`, `TEST_PASS boot.memory regions reports destination capacity`, `TEST_PASS boot.memory regions rejects invalid descriptors`, and `TEST_PASS boot.memory regions rejects invalid metadata`.
- Real QEMU smoke requiring `OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT,BOOT_ACPI_RSDP_HANDOFF_READY,UEFI_ACPI_RSDP_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke.log` contains `BOOT_MEMORY_REGIONS_READY` and `BOOT_MEMORY_REGION_COUNT 102`.
- `artifacts/logs/qemu-smoke.summary.log` records `MISSING_MARKERS none` for `OS_SMOKE_PASS;UEFI_MEMORY_MAP_OWNED;BOOT_HANDOFF_READY;UEFI_MEMORY_MAP_NORMALIZED;BOOT_MEMORY_MAP_ACCEPTED;BOOT_MEMORY_REGIONS_READY;BOOT_MEMORY_REGION_COUNT;BOOT_ACPI_RSDP_HANDOFF_READY;UEFI_ACPI_RSDP_READY`.
- The ACPI physical discovery probe publish emitted `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`, `TEST_PASS efi.shell.publish`, and `EFI_ARTIFACT artifacts/efi/BOOTX64-ACPI.EFI`.
- Real QEMU smoke against `artifacts/efi/BOOTX64-ACPI.EFI` with `-MarkerPreset acpi` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-acpi.log` contains `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY` before `OS_SMOKE_PASS`.
- `artifacts/logs/qemu-smoke-acpi.summary.log` records `MISSING_MARKERS none` for the default boot markers plus the five ACPI discovery markers.

Current evidence from 2026-07-25:

- .NET SDK `10.0.302` selected ILCompiler `10.0.8`; the resolved x64 MSVC
  linker reports file version `14.51.36252.0`.
- Clean, pushed source commit
  `32a8f3b91e17e1fc7e4b296730a5ea6565c38369` emitted
  `TEST_PASS efi.shell.publish`, and
  `scripts/test-efi-artifact.ps1` emitted `TEST_PASS efi.artifact.verify` for a
  PE32+ machine `0x8664`, EFI application subsystem `10`, executable/file-backed
  `.managed` entrypoint at RVA `0x00003730` with same-build `EfiMain`
  association, and absent ordinary, bound, IAT, and delay-import directories.
- The accepted EFI has 54,272 bytes and SHA-256
  `0580A45FC10B93C7C218CAB9BA179549C278B4CA970C82420CE74684EDA6036F`.
  The same-build link-map and response hashes are respectively
  `64375B6CC0C2A900289FE97F7C7013E3356F22003B9B7011AAC04C631A9FDFEE`
  and
  `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F`.
- QEMU `11.0.50` with
  `C:\Program Files\qemu\share\edk2-x86_64-code.fd` booted the source-bound raw
  image to `TEST_PASS qemu.smoke` plus every required marker and no forbidden
  marker. The accepted raw-image SHA-256 is
  `484A7B8D2C9E9C01BFF6AAA63633E3CEC3406A0FBAC487100DFB40B0A463428A`.
- The OVMF file SHA-256 is
  `33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
- The schema-version-1 evidence manifest binds clean source state, command
  bytes, exact tool versions, EFI/link inputs, image, serial log, host
  baseline, markers, and UTC timestamps. Its SHA-256 is
  `D398F73FF5051D138D5F2A77CE4EF51277BD2DE289F58BA5383FC6FC74D02E65`;
  `done/step846.md` is the canonical human-readable ledger.
- This run revalidates only the firmware-visible Gate 2 path. It does not prove
  real `ExitBootServices`, owned COM1, owned descriptor tables, live paging,
  drivers, storage, networking, app execution, hosted .NET, Kestrel, provider
  support, or production readiness.
- The complete host-tool preflight reported MSVC linker file version
  `14.51.36252.0`, Windows SDK `10.0.28000.0`, ILCompiler and NativeAOT runtime
  pack `10.0.8`, QEMU and qemu-img `11.0.50`, and the OVMF byte length and
  SHA-256 before emitting `EFI_PREFLIGHT_OK`. These are discovery facts, not a
  substitute for the publish, PE verification, or QEMU boot evidence above.

The preflight stops before `dotnet publish`, saves all evidence accumulated so
far, and reports a distinct `TEST_FAIL efi.shell.publish` reason:
`platform_linker_missing`, `windows_sdk_um_x64_missing`,
`ilcompiler_missing`, `nativeaot_runtime_pack_missing`, `qemu_not_found`,
`qemu_img_not_found`, or `ovmf_not_found`. It also rejects QEMU executables
whose `--version` output cannot be captured and OVMF files whose hash evidence
cannot be read. The post-publish `platform_linker_missing` detection remains a
backstop for linker failures that surface only during a real publish. These
failures identify host prerequisites; they are not a Hyper-V limitation.

The preflight is host-toolchain evidence only. `EFI_PREFLIGHT_OK` proves that
the reported files and versions were discoverable; it does not prove a
successful publish, a staged `artifacts/efi/BOOTX64.EFI`, QEMU boot,
`ExitBootServices`, or any runtime behavior.
`tests/OS.HostTests/BuildEfiShellPreflightTests.cs` covers every missing-tool
reason, exact injected-path evidence, injected search-root discovery, version
markers, the OVMF hash/length record, and the rule that unrelated environment
values are not printed.

## Known Limitations

The current type-cast helpers are fail-fast placeholders for the shell only; real type safety belongs with later runtime/interface-dispatch work. The EFI shim must not return successful type-check results for unsupported runtime behavior.

The current UEFI protocol structs are intentionally minimal. The default path
covers the fields needed for early console output, Serial I/O fallback,
`LocateProtocol`, `GetMemoryMap`, and `AllocatePool`; the opt-in M01-021
adapter additionally types only the required `HandleProtocol`,
`AllocatePages`, and `FreePages` slots plus the loaded-image fields it reads.
They are not a complete firmware abstraction layer.

`IPlatformConsole` is host-tested as a guardable platform contract only. `EfiEntry` now routes byte writes through a no-stdlib `EfiConsoleAdapter`, but this is still not the full managed `IPlatformConsole` implementation and it does not prove post-ExitBootServices owned serial output.

The memory-map source buffer is firmware-allocated by the source-linked capture helper, but the descriptor bytes are copied into a static OS-owned buffer before `UEFI_MEMORY_MAP_OWNED` is emitted, the owned handoff validates before `BOOT_HANDOFF_READY` is emitted, the compact live summary normalizes before `UEFI_MEMORY_MAP_NORMALIZED` is emitted, and compact boot-region records materialize before `BOOT_MEMORY_REGIONS_READY` is emitted. A later step must still keep a valid map-key retry path for `ExitBootServices` and turn the compact records into allocator-owned usable/reserved page ranges.

The logged default-path memory-map metadata proves the map captured at that
point in the EFI path. The M01-021 opt-in source adds a post-allocation
static recapture, but that path has no accepted guest log. Neither record
proves that its map key remains valid after later boot-services calls.

The ACPI physical discovery probe proves only pre-`ExitBootServices` table bytes on the tested QEMU/OVMF host. It does not map ECAM, enumerate PCI, size BARs, bind drivers, discover APIC/HPET for use, or prove post-firmware physical-memory access.

Any next-step EFI build work must keep the freestanding constraints explicit. If a new path introduces NativeAOT helper/linker failures, document the exact failing symbol or helper before adding a runtime stub. Do not stub success for allocation, type, exception, or interface-dispatch behavior that is not actually implemented.

## Next Steps

1. Move UEFI structs behind platform interfaces.
   - Keep the low-level UEFI table pointers isolated to the EFI boundary.
   - Expose only the narrow operations required for console diagnostics, memory-map ownership, firmware shutdown state, and optional filesystem/protocol discovery.
2. Obtain the M01-022 QEMU/OVMF owned-stack proof using the accepted,
   audited low-firmware-stack M01-021 preparation path.
   Implement post-firmware COM1 before enabling any real firmware shutdown.
3. Copy required firmware data, add the typed `ExitBootServices` call adapter,
   and implement the bounded stale-map-key retry only after its M01/M02
   dependencies pass.
4. Route additional platform interfaces through the UEFI-gone simulation path
   as they are introduced.
5. Replace permissive shell-only runtime placeholders with audited runtime
   helpers as subsystems require them.
