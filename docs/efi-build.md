# EFI Build

## Current Status

An explicit EFI NativeAOT project shell exists at `src/OS/OS.csproj`. It is
included in `CLROS.slnx`, but its directory-local rules keep the freestanding
no-stdlib build explicit and separate from ordinary managed runtime policy.

As of 2026-07-27, this host has the required Visual Studio C++ toolchain,
Windows SDK, QEMU, and OVMF installed. `scripts/build-efi-shell.ps1` publishes a
NativeAOT x64 PE with EFI application subsystem and stages it as
`artifacts/efi/BOOTX64.EFI`.

The most recent staged artifact enables `M01_LOADED_IMAGE_STACK_PROBE`,
`M01_LIVE_ALLOCATOR_PROBE`, and `M01_OWNED_PAGING_PROBE`. It contains the
M01-021 loaded-image/page-allocation/map/ledger/owned-stack wiring, the M01-023
allocator-arena adapter, and M01-025's stage-three source wiring. M01-024 owns
the accepted stage-two QEMU/OVMF tuple. The M01-025 stage-three artifact has
host/source and PE-verification evidence only; it has not executed under
QEMU/OVMF.

## Implemented Behavior

- The project uses `NoStdLib`, `PublishAot`, `EntryPointSymbol=EfiMain`, and `LinkerSubsystem=EFI_APPLICATION`.
- `EfiEntry` exports `EfiMain`.
- `EfiEntry` contains one opt-in `M01_LOADED_IMAGE_STACK_PROBE` adapter. It is
  entered only after the ordinary owned handoff is available; the all-false
  build retains the previous path and marker order.
- When `M01_LIVE_ALLOCATOR_PROBE` is enabled with the required stage-one probe,
  the owned-stack continuation enters one additional default-off adapter. It
  allocates a two-page loader-data arena, performs a fresh static map recapture,
  builds an exact five-record ledger, and calls the reviewed M01 allocator
  coordinator before emitting the packed stage-two success plan.
- When `M01_OWNED_PAGING_PROBE` is enabled with both required earlier probes,
  the stage-two continuation enters M01-025's default-off stage-three source
  adapter. It wires three page-allocation sites, refreshed-map/ledger checks,
  the reviewed preparation and physical-arena materialization contracts, the
  audited composite call gate, final readback and pre-transfer validation, a
  generated transfer call site, returned-observation audit, and the fixed
  stage-three marker plan. Current evidence proves that this path compiles; it
  does not prove that any of those EFI or generated-code calls executed.
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
into the canonical `src/OS/OS.csproj` no-stdlib compilation. M01-034 expands
that closure to exactly 44 direct compile items: 38 linked `OS.Kernel` sources
and the six local EFI sources `EfiConsoleAdapter.cs`, `EfiEntry.cs`,
`X64InstructionPublication.cs`, `RuntimeImportAttribute.cs`,
`MethodImplAttribute.cs`, and `MinimalRuntime.cs`. There are no source globs,
conditional future slots, alternate EFI projects, generated compile items, or
managed project/assembly references.

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
EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=28 prerequisites=3 compileItems=44 prerequisiteGap=UefiMemoryType
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

### M01 live allocator stage-two EFI wiring (M01-023 build evidence)

The dual-probe artifact is built explicitly with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe
```

The allocator-only combination is invalid. Both the publish preflight and
MSBuild reject it with
`reason=live_allocator_requires_loaded_image_stack`; the ordinary build and
the M01-022 stage-one-only profile remain valid.

After the owned-stack continuation has completed M01-022's final observation,
the M01-023 adapter requests one two-page `EfiLoaderData` arena, performs one
fresh map recapture using the existing static buffers, requires a changed map
key and matching allocation descriptor, extends the canonical ledger from four
to five records, and invokes the M01-018 no-stdlib coordinator exactly once.
The adapter requires an initially zero 312-byte durable state before the
firmware allocation and byte-compares the complete returned and persisted
states before success. Any failure after EFI grants ownership reaches one
matching `FreePages` attempt; no success marker after the stage-two start can
be emitted on a failed result.

The fresh build reported both probe markers, `TEST_PASS efi.artifact.verify`,
and `TEST_PASS efi.shell.publish`. The artifact verifier accepted PE32+ AMD64
machine `0x8664`, EFI application subsystem `10`, an executable/file-backed
entrypoint associated with the same-build `EfiMain`, and absent ordinary,
bound, IAT, and delay-import directories. The accepted build products were:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `BOOTX64.EFI` | 208,384 | `620C37CC6CF517668A84F760CAC19241EB2FFBB6BB8A81B97B2CDA95AC063B44` |
| `BOOTX64.EFI.link.json` | 1,817 | `4D2486A4F3E48FC66480B82B591ACBE0604A41144896FAFFC06FD1AE227A288B` |
| `BOOTX64.EFI.link.map` | 93,728 | `97FD2283DA9C62AFEE4CB02B17B8BF340DC7827299D279866F688C4C86E4DBD9` |
| `BOOTX64.EFI.link.rsp` | 217 | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `OS.obj` | 710,442 | `55BCEEDBA5DCD8A1F9057BB5787FD3CDF153783149D7CA16BE8CC3AA378C9358` |

The emitted `RunAfterStageOne` frame reserves `0xF38` bytes plus eight saved
nonvolatile registers, and `ValidateCompletedResult` reserves `0xED8` bytes
plus its saved registers. Both remain within the already proven 33-page owned
stack. This is binary-specific evidence and must be repeated when source,
ILCompiler, runtime pack, linker inputs, or the artifact changes.

Focused source-contract and mutation tests emit `TEST_START m01.023`,
`CLROS_M01_023_READY`, and `TEST_PASS m01.023`. The accepted complete host
harness exited zero with one `TEST_PASS host.tests`, zero `TEST_FAIL` lines,
and an 819,024-byte stdout log whose SHA-256 is
`4ECD25E46DB2EB41CC36944165EF67EE9EF1B74CEBB0797E59DF4DCC4BEDA60C`.
The build does not prove that stage two executed in a guest, that firmware
granted or reclaimed the arena, that the live allocator cycle completed, or
that the allocated physical page was accessed. It does not establish a
post-EBS allocator, paging, NX/W^X, owned COM1, descriptor loads, PCI, storage,
networking, app execution, hosted .NET, Kestrel, cloud support, or any
completion-gate promotion. M01-024 owns the separate QEMU/OVMF execution and
serial evidence.

### M01 owned-paging stage-three EFI wiring (M01-025 build evidence)

The three-probe artifact is built explicitly with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe
```

The owned-paging combination is valid only with both earlier probes. M01-025
source-wires the stage-three adapter after a completed stage-two state and
preserves the all-false, stage-one-only, and stage-one-plus-stage-two paths.
Each successful `AllocatePages` status immediately becomes the authoritative
ownership flag before CLROS validates the returned address. Consequently,
successfully granted but rejected zero, misaligned, wrapping, overlapping, or
wrong fixed-address allocations remain eligible for cleanup.

All 21 pre-entry failures forward the exact ownership tuple to one cleanup
path. That path attempts `FreePages` for table, probe, and code in reverse
allocation order, uses the corresponding requested page counts, and continues
after any individual free failure. The host source/control-flow oracle covers
16 allocation/map/validation scenarios under all eight possible combinations
of the three free-call outcomes, for 128 deterministic model evaluations.
Those evaluations inspect the source and cleanup model; they do not invoke a
fake EFI service, a real firmware callback, or any generated instruction.

The accepted validation evidence was:

- `dotnet build .\CLROS.slnx --nologo -m:1`: exit 0, zero warnings, zero
  errors, and empty stderr.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`: exit 0,
  zero warnings, zero errors, and empty stderr.
- Final integrated host harness from `2026-07-28T11:25:45.8776369Z` through
  `2026-07-28T11:34:24.2000179Z`: exit 0; exactly one each of
  `TEST_START m01.025`, `CLROS_M01_025_READY`, `TEST_PASS m01.025`, and
  `TEST_PASS host.tests`; no `TEST_FAIL` line and no named M01-019, M01-021,
  M01-030, or M01-031 diagnostic marker (`M01_021_DIAGNOSTIC`,
  `M01_019_BINDING_DIAGNOSTIC`, `M01_019_NOSTDLIB_COMPILE_DIAGNOSTIC`,
  `M01_030_PREPARATION_DIAGNOSTIC`, or `M01_031_ARENA_DIAGNOSTIC`).
- Host stdout SHA-256
  `E9377EECB18F71A8EC329ACAA14F049757A4FD2887B76C85F6F3065B7BE24A1F`;
  empty stderr SHA-256
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Naming validation and QEMU dry run: exit 0 with empty stderr.

The fresh all-switch publish also exited zero with empty stderr. The artifact
verifier accepted PE32+ machine `0x8664`, EFI application subsystem `10`, an
entry RVA of `0x49c50` in `.managed`, a same-build `EfiMain`, and absent
ordinary runtime imports. Its frozen hashes were:

| Build output | SHA-256 |
| --- | --- |
| `BOOTX64.EFI` | `0A674D1628D48C478D616F43548DA18BC9673D4BECBEC0D875C7ECDA05F646CB` |
| `BOOTX64.EFI.link.map` | `B3A9772F07B579986DF4F3B726ADC1AB378FDE4F79E80B9B497DEAA166C11037` |
| `BOOTX64.EFI.link.rsp` | `0108286539031918F3576A3C9759803740D9E2DCB1A26E3E244E882C56E82F1F` |
| `BOOTX64.EFI.link.json` | `405CF854F218423A0283F0DFCF1148FF7E7944F8AAC9F360FC2687A37884AA62` |

The build recorded MSVC linker `14.51.36252.0`, Windows SDK `10.0.28000.0`,
ILCompiler `10.0.8`, QEMU `11.0.50`, and OVMF SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

This is source closure and fresh NativeAOT artifact evidence only. It does not
show firmware allocating, describing, or freeing any page; execute an EFI
callback, architectural-preflight stream, planner thunk, transfer thunk, call
gate, composite stream, or probe; install or restore CR3; change CR4, RFLAGS,
a PTE, permissions, or the TLB; prove page-zero absence or W^X/NX in hardware;
run a stage-three QEMU/OVMF guest; or advance a completion gate.

The first architectural-preflight bytes are dynamically published before their
first transfer; serialization inside a later generated stream cannot serialize
that initial fetch. M01-033 supplied the reviewed deterministic C# AMD64 COFF
object. M01-034 now integrates that exact 336-byte object into the canonical
native link and independently verifies the resulting EFI image, without
invoking the symbol. The final image contains the exact 20-byte serializer in
an executable, readable, nonwritable section and one matching exception-table
entry whose eight-byte unwind record is readable, nonwritable, and
nonexecutable. The verifier also requires the exact map symbol, linker-response
input, no ordinary imports, and the exact managed-assembly hash and managed-PE
shape recorded by the same build. The host integration test independently
decodes that assembly's metadata and requires the exact
`RuntimeImport`/`InternalCall` no-body binding. M01-035 now places and audits
the checked call at every current publication epoch, and the accepted M01-026
tuple supplies the bounded all-switch guest execution prerequisite used by
M02-017.

### M02 safe-stub EFI closure (M02-017)

M02-017 adds `Boot/M02SafeStubPageIntegration.cs` plus the three reviewed
diagnostics contracts required to materialize the fixed page. The explicit EFI
compile closure is now 48 items, and the canonical build/verifier source
snapshot is 61 inputs. `scripts/build-efi-shell.ps1` and
`scripts/test-efi-artifact.ps1` both pin those exact ordered closures; missing,
duplicate, reordered, substituted, wildcard-imported, or stale project-input
variants fail closed. The default switch remains false. Enabling
`M02_SAFE_STUB_PROBE` requires all three M01 probe symbols in the same fresh
publish.

The accepted source-bound build from commit
`cf8ce63c37a9c62a0f8f736adda14b6bd3e19a9a`, tree
`d18f520cee19aa0c89dfb41f326c92afd0284c02`, reported
`EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=61` and
`EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3
compileItems=48`. The post-QEMU same-build verifier accepted PE32+ AMD64, EFI
subsystem 10, no import directories, 843 runtime-function records, the exact
20-byte instruction-publication serializer, and EFI SHA-256
`D30B508AC06F592756C36ABD2B5CFAE2469ED31DF234BFDCD37134A7DBEB4EA5`.
The five-file copied EFI bundle and publish log are preserved under
`artifacts/logs/m02-017-final/cf8ce63c-20260803T211323Z-r1/`.

This build evidence establishes the exact freestanding source and artifact
identity consumed by the separate M02-017 QEMU tuple. It does not by itself
prove guest execution, privileged port I/O, owned COM1, real
`ExitBootServices`, provider behavior, hosted .NET, Kestrel, CloudReady status,
or production readiness.

### Checked COM1 configuration EFI closure (M02-003/M02-004)

The current serialized profile adds
`Diagnostics/InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs`,
`Diagnostics/COM1OwnedPortIOBackend.cs`,
`Boot/M02CheckedPortIoFunctionPointerIntegration.cs`, and
`Boot/M02Com1OwnedPortIoBackendIntegration.cs`. The explicit EFI compile
closure is 52 items and the canonical build/verifier authority snapshot is 65
inputs. `EnableM02Com1OwnedPortIoProbe` defaults false and requires
`EnableM02CheckedPortIoBindingProbe`, which in turn requires the complete
M02-017/M01 probe tuple.

With every required switch enabled, preflight reports
`EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=65` and
`EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3 compileItems=52`.
The same-build verifier accepts PE32+ AMD64, EFI subsystem 10, no ordinary or
delay imports, the exact `EfiMain` association, and the linked instruction
serializer. The current 485,888-byte artifact SHA-256 is
`61CA5D6879DA1AE7DC0A6938AA61AD64DCEFBD61AA937C0DF2D8C3BF0809820E`.

This artifact evidence establishes the no-stdlib closure and PE/link contract.
The separate QEMU tuple proves seven pre-`ExitBootServices` COM1 configuration
writes. Neither result proves `In8` polling, owned serial payload transport,
post-`ExitBootServices` diagnostics, a provider, CloudReady status, hosted .NET,
Kestrel, or production readiness.

### M02-005 owned COM1 payload EFI closure

The default-false `EnableM02OwnedCom1ReadyProbe` profile adds exactly one
explicit compile item,
`Diagnostics/OWNEDCOM1READYExclusivelyOwnedWriter.cs`, immediately after the
owned COM1 backend and before the independent endian catalog. Its accepted
closure was 54 compile items and 67 build/verifier authority inputs. The active
M02-006 development tree is now 55/68 after inserting its separately owned
reporter immediately after the M02-005 writer; this does not alter the accepted
M02-005 artifact evidence below. The M02-005 flag requires
`EnableM02Com1OwnedPortIoProbe=true`; the QEMU wrapper additionally requires
`-BuildEfi`, rejects `-SkipImageBuild`, and rebuilds both EFI and image inputs.

After the accepted M02-004 binding/configuration path succeeds and before
`TEST_PASS m01.stage3`, `EfiEntry` constructs `Com1OwnedPortIoBackend` from the
already checked binding and calls the writer exactly once. It accepts only a
result with seven configuration writes, 16 marker bytes, two line-ending bytes,
18 first-ready polls in the recorded QEMU profile, the owned backend selected,
and firmware fallback absent. Failure emits `M02_OWNED_COM1_READY_FAILED` over
the existing UEFI diagnostic channel and aborts the optional path. Success
leaves the standalone `OWNED_COM1_READY\r\n` bytes exclusively on checked COM1
`Out8`, then emits a separate fixed UEFI-serial evidence line whose transport is
explicit.

Build and verification use the complete cumulative profile through the QEMU
wrapper:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02SafeStubProbe `
  -EnableM02CheckedPortIoBindingProbe `
  -EnableM02Com1OwnedPortIoProbe `
  -EnableM02OwnedCom1ReadyProbe
```

The development accepted publish reported
`EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=67`, `compileItems=54`,
`TEST_PASS efi.artifact.verify`, and `TEST_PASS efi.shell.publish`. Its
492,032-byte EFI artifact SHA-256 is
`8E9F1F70738158858AC319BB4C9085D3DB7005DEF3E6A080585CCB20559F9097`.
The artifact remained PE32+ AMD64, EFI subsystem 10, without ordinary or delay
imports, with the exact `EfiMain` association and audited serializer closure.
Final-head review evidence is still required.

This closure and the separately recorded real-QEMU tuple prove only one bounded
checked COM1 line-status read/payload-write path in the pre-`ExitBootServices`
EFI application. They do not prove owned serial diagnostics after firmware
exit, panic routing, physical hardware/provider behavior, hosted .NET, Kestrel,
CloudReady status, production readiness, or a numbered-gate advancement.

### M02-006 panic-safe owned-COM1 EFI closure

The default-false `EnableM02PanicSafeOwnedCom1Probe` profile adds exactly one
explicit compile item,
`Diagnostics/PanicSafeFormattingSerialReportingOwnedCOM1.cs`, after the
M02-005 writer and before the independent endian catalog. The current closure
is 55 compile items and the canonical build/verifier authority snapshot is 68
inputs. Under `NOSTDLIB_EFI`, the production source exposes only unsafe
pointer/length formatting and its allocation-free readiness byte catalog; the
host `Span` overloads are excluded. The flag requires
`EnableM02OwnedCom1ReadyProbe=true`. The QEMU wrapper additionally requires
`-BuildEfi`, rejects `-SkipImageBuild`, and rebuilds both EFI and image inputs.

After the accepted M02-005 direct marker and evidence line succeed and before
`TEST_PASS m01.stage3`, `EfiEntry` reconstructs the checked
`Com1OwnedPortIoBackend` and calls `EmitReadinessMarker` exactly once. It
accepts only seven configuration writes, 125 marker bytes, 127 serial bytes,
127 first-ready polls in the recorded QEMU profile, a final successful LF write
to `0x3F8`, the owned backend selected, and firmware fallback absent. Failure
emits `M02_PANIC_SAFE_OWNED_COM1_FAILED` through the existing UEFI diagnostic
path and suppresses both M02-006 success lines.

The direct marker is emitted only by the production reporter through checked
COM1 port I/O:

```text
CLROS_M02_006_READY panicFormatter=bounded ownedCom1Transport=true callerScratch=true statusCoded=true firmwareFallback=false
```

Only after that direct path succeeds, `EfiEntry` emits this separate fixed UEFI
serial evidence line:

```text
CLROS_M02_006_EVIDENCE markerBytes=125 serialBytes=127 configurationWrites=7 lineStatusPolls=127 markerTransport=com1PortIo evidenceTransport=uefiSerial guestPortIo=true guestIn8=true panicFormatter=linked panicReporter=ownedCom1 callerScratch=true statusCoded=true firmwareFallback=false postExitBootServices=false
```

Build and verification use the complete cumulative profile:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02SafeStubProbe `
  -EnableM02CheckedPortIoBindingProbe `
  -EnableM02Com1OwnedPortIoProbe `
  -EnableM02OwnedCom1ReadyProbe `
  -EnableM02PanicSafeOwnedCom1Probe
```

The development publish reported
`EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=68`, `compileItems=55`,
`TEST_PASS efi.artifact.verify`, and `TEST_PASS efi.shell.publish`. Its
500,736-byte EFI artifact SHA-256 is
`004006C004248406E7EF504F7CE5CEC0A04E8340EB6BF4168C316780008617C9`.
The artifact remains PE32+ AMD64, EFI subsystem 10, with no ordinary, bound,
IAT, or delay imports. The compile-closure and canonical-project SHA-256 values
are respectively
`5E04CF9C3F932FB0F794394CC69389F9B0EEF85766368C39D7DEE46069D36337`
and
`83719D2344EFED633C9CA133339EBE4091C1E6CCA07E75491996CAE42E539CC0`.
The development hashes above do not substitute for the complete exact-tree
gate; that gate is bound to the pushed review head in Gitea. Independent
exact-head review remains required.

This closure proves that the allocation-free reporter is linked and that its
harmless readiness method can execute over the checked COM1 backend before
firmware exit. It does not trigger a panic or fault and does not prove live
IDT/GDT/TSS/IST handling, post-firmware diagnostics, real `ExitBootServices`,
physical hardware/provider behavior, hosted .NET, Kestrel, CloudReady status,
production readiness, or a numbered-gate advancement.

### M02-007 caller-owned x64 GDT encoding EFI closure

The default-false `EnableM02OwnedX64GdtEncodingProbe` profile adds exactly one
explicit compile item,
`Diagnostics/OwnedX64GDTContainingNullN64BitCode.cs`, after the accepted
M02-006 reporter and before the independent endian catalog. The current closure
is 56 compile items and the canonical build/verifier authority snapshot is 69
inputs. The flag requires `EnableM02PanicSafeOwnedCom1Probe=true`; the QEMU
wrapper additionally requires `-BuildEfi`, rejects `-SkipImageBuild`, rebuilds
both EFI and image inputs, and composes the accepted M01 prerequisites.

After the M02-006 direct marker and distinct evidence line succeed and before
`TEST_PASS m01.stage3`, `EfiEntry` creates one zero-initialized, fixed-size local
stack workspace containing exactly 104 TSS bytes and 40 GDT bytes. It calls
`OwnedX64GdtContainingNullN64BitCode.Encode` exactly once with the actual TSS
buffer address and limit 103. Success requires initialized success statuses,
40 bytes written and required, table limit 39, reported destination mutation,
an exact 40-byte readback with first mismatch `-1`, equal result/observed/
expected fingerprints, selectors 0/8/16/24, and an unchanged zeroed TSS image.
Failure emits `M02_OWNED_X64_GDT_ENCODING_FAILED` through UEFI diagnostics,
suppresses the success marker, and aborts the optional path.

Only after every gate passes, the EFI path emits this fixed evidence line:

```text
CLROS_M02_007_READY encoding=guestValidated callerOwned=true readBack=true gdtBytes=40 tssBytes=104 tableLimit=39 selectors=0,8,16,24 guestEncoding=true guestLgdt=false guestLtr=false allocationFree=true markerTransport=uefiSerial postExitBootServices=false
```

Build, artifact verification, image creation, and QEMU use the complete
cumulative profile:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02SafeStubProbe `
  -EnableM02CheckedPortIoBindingProbe `
  -EnableM02Com1OwnedPortIoProbe `
  -EnableM02OwnedCom1ReadyProbe `
  -EnableM02PanicSafeOwnedCom1Probe `
  -EnableM02OwnedX64GdtEncodingProbe
```

The corrected development publish reported 69 source-authority inputs, 56
compile items, and `TEST_PASS efi.artifact.verify`. Its 506,880-byte PE32+ AMD64
EFI artifact has SHA-256
`88B31B1DEB6B4C1DF3A1FCFDFDCA8BC23CC152039D4D9A767B445672D97861F1`,
subsystem 10, and no ordinary, bound, IAT, or delay imports. The complete
exact-head host/QEMU gate and independent review remain required.

This closure proves only pre-`ExitBootServices` execution of the reviewed
encoder into caller-owned guest memory and byte-for-byte readback observed via
UEFI serial. It does not execute `lgdt` or `ltr`, install active GDT/TSS state,
trigger or handle a live fault, prove post-firmware diagnostics or real
`ExitBootServices`, establish hardware/provider support, run hosted .NET or
Kestrel, advance a numbered completion gate, or establish CloudReady or
production readiness.

### M02-008 guarded IST arena allocation EFI closure

The default-false `EnableM02GuardedIstStackAllocationProbe` profile links
`Diagnostics/GuardedISTStacksDoubleFaultPageFaultNMI.cs` immediately after the
accepted M02-007 encoder and before the independent endian catalog. The current
development closure is exactly 58 compile items and 71 source-authority inputs
after the default-off M02-009 terminal stub source was added after this
historically accepted slice.
The flag requires `EnableM02OwnedX64GdtEncodingProbe=true`; the QEMU wrapper
also requires `-BuildEfi`, rejects `-SkipImageBuild`, and rebuilds both the EFI
artifact and raw image.

After the M02-007 marker succeeds and before `TEST_PASS m01.stage3`, `EfiEntry`
requires live UEFI `AllocatePages` and `FreePages` functions. It allocates a
temporary six-page UEFI LoaderData arena: three 4096-byte guard-intent ranges
interleaved with three 4096-byte stack ranges for double fault IST 1, page fault
IST 2, and NMI IST 3. It initializes and allocates the reviewed status-coded
contract once, verifies every address, length, lease, metadata, and IST field,
writes and reads fixed sentinels at each stack base and stack-top-minus-one, and
never dereferences a guard-intent range. It then releases the lease and calls
`FreePages` for the same base and six pages. A bounded cleanup is attempted when
firmware allocation succeeded but a later gate failed.

Success emits exactly this pre-`ExitBootServices` UEFI-serial line:

```text
CLROS_M02_008_READY stacks=3 guards=3 doubleFaultIst=1 pageFaultIst=2 nmiIst=3 writableMetadata=true executableMetadata=false allocationFree=true guestArenaAllocated=true guestStackWrites=true guestGuardMappings=false guestTss=false guestIdt=false arenaFreed=true markerTransport=uefiSerial postExitBootServices=false
```

Any missing firmware function or allocation, layout, stack-readback, lease,
release, or free failure emits
`M02_GUARDED_IST_STACK_ALLOCATION_FAILED`, suppresses the success line, and
aborts the optional profile. The complete command is:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02SafeStubProbe `
  -EnableM02CheckedPortIoBindingProbe `
  -EnableM02Com1OwnedPortIoProbe `
  -EnableM02OwnedCom1ReadyProbe `
  -EnableM02PanicSafeOwnedCom1Probe `
  -EnableM02OwnedX64GdtEncodingProbe `
  -EnableM02GuardedIstStackAllocationProbe `
  -RequireMarker "CLROS_M02_008_READY"
```

This executes reviewed allocation and lease logic against real temporary guest
memory, but the descriptor flags remain metadata. It does not make the guard-intent pages non-present, enforce NX in active mappings, install a TSS,
IST, GDT, or IDT, switch stacks, trigger or handle a fault, survive real
`ExitBootServices`, establish provider or CloudReady support, run hosted .NET
or Kestrel, advance a numbered completion gate, or establish production
readiness. The source-bound artifact and real-QEMU tuple passed on 2026-08-06:
the 517,632-byte EFI artifact has SHA-256
`58464cd09ece7ce392252588d5c3327ccf64cfe19af4087454292e6646366d05`,
the verifier accepted the exact 57-item compile closure and 70-input source
authority with no imports, and the guest stream accepted 56 ordered/unique
markers while rejecting 40 forbidden markers. The uncompromised host gate also
completed 3,446 starts and 3,447 passes with zero failures and one final
`TEST_PASS host.tests`. Independent exact-head review remains a separate merge
requirement.

### M02-009 owned GDT/TSS terminal EFI integration

The default-false `EnableM02OwnedGdtTssTerminalProbe` profile links the reviewed
`Diagnostics/OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs` source immediately
after the M02-008 guarded-IST dependency and before the independent endian
catalog. The explicit development closure is therefore 58 compile items and 71
source-authority inputs. The flag requires
`EnableM02GuardedIstStackAllocationProbe=true`; the build and artifact-verifier
paths propagate the same property and require the exact
`M02_OWNED_GDT_TSS_TERMINAL_PROBE` symbol.

After firmware console and serial discovery, but before `WriteBootMarkers`, the
opt-in EFI path requires live UEFI `AllocatePages` and `FreePages`. It allocates
exactly one 4096-byte `EfiLoaderCode` page followed by one disjoint 4096-byte
`EfiLoaderData` page. Both addresses must be nonzero, page aligned, canonical
through the last byte, free of arithmetic overflow, and non-overlapping. The
path zeroes both complete pages, places `OwnedGdtTssTerminalContext` at data-page
offset zero, then calls the reviewed `PrepareContext`, `Emit`, and
`ValidateReadBack` operations in that order. It requires the exact 168-byte
context, 2,987-byte stub, `C9C04DEE967D640A` FNV-1a fingerprint, shared context
fingerprint, exact result counts, and zero tails on both pages. A pre-entry
failure frees the data page before the code page and emits
`M02_OWNED_GDT_TSS_PREENTRY_FAILED` through the still-live firmware diagnostic
path.

Only after the successful cumulative M01 continuation has emitted the boot
sequence plan, including `OS_SMOKE_PASS`, does the terminal path revalidate both
pages. It emits `TEST_START m02.009` directly through the existing checked,
owned COM1 binding, calls the required instruction-publication serializer, and
invokes the code-page address as a Windows-x64 unmanaged function pointer with
the context pointer in the first argument register (RCX). The reviewed stub
performs the descriptor-table transition and emits these terminal success
records directly through owned COM1:

```text
CLROS_M02_009_READY gdtr=true tr=true gdtBytes=40 tssBytes=104 gdtrLimit=39 trSelector=24 ownedCom1=true terminalHalt=true postExitBootServices=false
TEST_PASS m02.009
```

The stub intentionally halts and does not return. A readback or stub-side
verification failure emits `M02_OWNED_GDT_TSS_VERIFY_FAILED` and halts. If the
terminal call unexpectedly returns, the caller attempts the direct-COM1
`M02_OWNED_GDT_TSS_UNEXPECTED_RETURN` marker and spins without calling firmware.

The complete acceptance command is:

```powershell
.\scripts\run-qemu-smoke.ps1 `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02SafeStubProbe `
  -EnableM02CheckedPortIoBindingProbe `
  -EnableM02Com1OwnedPortIoProbe `
  -EnableM02OwnedCom1ReadyProbe `
  -EnableM02PanicSafeOwnedCom1Probe `
  -EnableM02OwnedX64GdtEncodingProbe `
  -EnableM02GuardedIstStackAllocationProbe `
  -EnableM02OwnedGdtTssTerminalProbe
```

No fresh real-QEMU acceptance is recorded for this development integration
yet. The allocation types express intended code/data use, but UEFI allocation
types do not enforce page permissions; this implementation does not prove W^X
or NX. All M02-009 evidence remains pre-`ExitBootServices`. The profile does not
install an IDT or fault handler, trigger a fault, prove post-firmware owned
diagnostics, survive real firmware exit, advance a numbered completion gate,
run hosted .NET or Kestrel, establish provider support, or establish CloudReady
or production readiness.

### M04-002 fixed endian-catalog EFI closure

`EnableM04IndependentEndianConformanceProbe` is a default-false, independent
profile. At its acceptance head it added exactly one explicit compile item:
`std/no-runtime/shared/IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs`.
That historical accepted project closure had 53 compile items and the canonical
build/verifier authority had 66 inputs. The current development tree has 58/71
after the later M02-005 writer, M02-006 reporter, M02-007 GDT encoder, and
M02-008 guarded-IST arena contract plus the default-off M02-009 terminal stub
were inserted before the catalog; this
does not alter or reinterpret the M04-002 artifact evidence below. The scripts reject
compatibility counts, missing/reordered sources, stale hashes, wildcard imports,
or a post-restore property mismatch.

Under `NOSTDLIB_EFI`, the catalog exposes caller-owned unsafe pointer APIs
instead of its normal host span APIs. The EFI entry point stack-allocates
exactly 25 read-vector records and seven reverse-vector records after console
and serial discovery, copies and validates the catalog, and requires the exact
counts, `E81E4451E8D999F6` fingerprint, five/seven source-series counts,
`referenceFilesRequired=false`, `hostConformance=false`,
`canonicalSystemNamespace=false`, and Gate 4 `Fail`. Any mismatch emits
`M04_ENDIAN_CONFORMANCE_FAILED` and returns before the normal memory-map marker
path. Success emits exactly:

```text
CLROS_M04_002_READY readVectors=25 reverseVectors=7 byteSeries=5 uint16Series=7 fingerprint=E81E4451E8D999F6 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

Build and verification use:

```powershell
.\scripts\build-efi-shell.ps1 `
  -EfiOutputPath .\artifacts\efi\BOOTX64-M04-002.EFI `
  -LogPath .\artifacts\logs\m04-002-efi-build.log `
  -EnableM04IndependentEndianConformanceProbe
```

The build reports `EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=66`,
`compileItems=53`, and `EFI_PROBE M04_INDEPENDENT_ENDIAN_CONFORMANCE`. The
same-build verifier independently re-evaluates the M04 project property as a
typed Boolean, pins the 53/66 closures, and validates PE32+ AMD64, EFI subsystem
10, absent ordinary/delay imports, the exact `EfiMain` map association, and the
linked serializer. The verified 62,464-byte development artifact has SHA-256
`44F4A7B5E2929884869F324E998ABC5B89559707ACC4BA65412EB818B18D9CA0`;
its link-evidence JSON has SHA-256
`1C50CB90373C7BB4D4B123BB5ADD8B4F7971C5BE66A544F4A07AA76246AD0708`.

The bounded stack workspace causes NativeAOT to require the compiler GS-cookie
symbol in this otherwise independent profile. The EFI source supplies a
profile-local deterministic linker-visible anchor only when the M01 paging
profile is absent. It is not randomized-cookie initialization or production
hardening evidence.

This closure and its separate QEMU tuple prove only fixed-catalog validation in
the pre-`ExitBootServices` EFI application. They do not prove general
`BinaryPrimitives` compatibility, object allocation, GC, exceptions, runtime
completion, real `ExitBootServices`, owned post-firmware diagnostics, hosted
.NET, Kestrel, provider support, CloudReady status, or production readiness.

### Linked instruction-publication serializer (M01-034)

`src/OS/OS.csproj` imports the single allowlisted
`tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets`
file. The import runs only as a host-side build integration:

- the generator remains an ordinary .NET 10 host tool and has no
  `ProjectReference`, package, custom reference, or non-SDK analyzer closure;
- the generated COFF object remains ephemeral, is generated atomically, and is
  registered as both the exact `NativeLibrary` input and linker argument only
  after the NativeAOT runtime-library list is finalized;
- the generator output directory is isolated from the parent build, and
  absolute or relative intermediate paths, including paths containing spaces,
  are normalized without changing the object identity;
- the object is registered early enough for normal `dotnet clean` tracking,
  while the same-build artifact evidence preserves a separate exact copy for
  independent verification;
- `/BREPRO`, `/INCLUDE:CLROS_X64_SerializeInstructionPublication`, `/MAP`, and
  `/MAPINFO:EXPORTS` make wall-clock-independent native output, symbol
  retention, and map evidence explicit, while the verifier rejects any
  `/FORCE` unresolved-symbol escape; and
- the integration rejects attempts to override the symbol, object name,
  expected byte count, expected object hash, or canonical generator inputs.

The direct no-stdlib binding consists of
`Boot/X64InstructionPublication.cs`,
`Runtime/RuntimeImportAttribute.cs`, and
`Runtime/MethodImplAttribute.cs`. Those three files are ordered before
`Runtime/MinimalRuntime.cs`. M02-017 later adds the three reviewed diagnostics
page contracts and `Boot/M02SafeStubPageIntegration.cs`, bringing the exact EFI
compile closure to 48 items. `X64InstructionPublication.Serialize()` binds
`CLROS_X64_SerializeInstructionPublication` from module `*` as an
`InternalCall` and expects success cookie `0x53524C43`; no managed fallback
body is present.

The build acquires one repository-wide, cross-process and cross-session
exclusive file lock at `artifacts/.locks/efi-publish.lock` before writing any
preflight or publish output. The lock deliberately serializes every
configuration and RID that targets the same repository tree; independent
source copies retain fully parallel build isolation. Preflight then pins the
direct and evaluated EFI and generator closures, including both project files,
root and tool-local build props/targets, the three generator sources, the two
expected SDK analyzers, and zero generator project/package/custom references.
Both evaluated-closure queries use a random isolated project-extension root,
disable automatic response files and default project-extension imports, and
reset repository and custom MSBuild hooks. The selected SDK `Sdks` and
extensions roots are explicit, the user-extension root is an absent isolated
path, and all Common.props, Common.targets, CSharp, and cross-targeting
wildcard/user-wildcard import switches are false. The same values are used by
restore, post-restore evaluation, publish, the nested host generator, and the
independent verifier. Consequently, stale or hostile
`src/OS/obj/*.nuget.g.props`, `*.nuget.g.targets`, `project.assets.json`, SDK
override, or user/machine ImportBefore/ImportAfter file cannot influence an
accepted evaluation.

The build takes a 61-input source snapshot that also includes `global.json`
plus the build and verifier scripts. It opens read/share-read handles that deny
writes and replacement before authoritative source validation, holds them for
the entire restore/publish/evidence/verification interval, and confirms the
same aggregate after locking and after publish. Every source path is checked
component by component from the canonical repository root; a reparse-point
ancestor or leaf is rejected before and after locking. The build removes only
the exact plain RID-specific `src/OS/bin` and `src/OS/obj` output directories.
The five final publication leaves may all be absent on a genuinely clean
build. In that case there is no stale output identity set to compare, but each
canonical leaf path is still reparse-checked before create-new publication.
When any stale leaf exists, the build opens it under the output authority,
requires every opened identity to be distinct and single-link, closes the
records, and only then removes the exact five allowlisted leaves.
Before restore, it authenticates five exact cached package
archives by source-pinned SHA-256, copies them into a freshly created local
source beside an allowlisted `NuGet.Config`, and read-locks both the five
original archives and all six local-source files. The configuration clears
ordinary package, fallback-package, and audit sources, so restore has one
explicit local package source and one fresh isolated package root.

That restore creates exactly five canonical NuGet/MSBuild inputs under
`src/OS/obj/<configuration>/net10.0/<rid>/nuget`:
`OS.csproj.nuget.dgspec.json`, `OS.csproj.nuget.g.props`,
`OS.csproj.nuget.g.targets`, `project.assets.json`, and
`project.nuget.cache`. The build rejects any missing, extra, empty, indirect,
or reparse-point entry, securely parses the generated props/targets, and
requires the exact version-4 asset graph. Generated package props and targets
remain restore data only: both restore and publish set
`ImportProjectExtensionProps=false`,
`ImportProjectExtensionTargets=false`, and
`ExcludeRestorePackageImports=true`.

After restore, the build enumerates the exact five isolated package/version
trees plus the selected external NativeAOT 10.0.8 runtime-pack tree. It
validates source-pinned package content identities, rejects any extra package
version or reparse point, hashes 900 stable package files with ordinal relative
paths, validates and locks all six metadata records separately, and holds the
resulting 906 file handles through verification. A narrower nine-file package
build closure pins the five active target files, two task assemblies, and both
task dependency manifests. Publish receives explicit canonical paths for
ILCompiler, ILLink, their tasks, the isolated 10.0.8 host compiler, and the
10.0.8 runtime pack. `OS.csproj` makes the SingleEntry target consume those
same CLROS roots and fails the build if that target rewrites either root.
The nested host generator removes inherited AOT/package globals and explicitly
replaces the restore/project-extension globals it needs with canonical child
values before its package-free build.

A locked post-restore evaluation requires exactly 44 compile items, zero
project references, zero explicit references, and the two SDK analyzers.
Publish then runs with `--no-restore`, `NoIncremental=true`, and the same
explicit paths. The build repeats the source, restore-source, generated
restore-data, package-build, full package-tree, and evaluated-item checks after
publish before accepting an artifact.

Link evidence schema 3 records those distinct closures and their aggregate
SHA-256 values; the isolated project-extension, package-root, and exact
`project.assets.json` paths; the pinned expected five-edge package-import and
two-task-binding contracts; post-restore item counts and hashes; both linked
and preserved object paths; object and binding input hashes; canonical managed
assembly identity; linker response/map hashes; final EFI hash; the validated
.NET host plus selected SDK version and paths; and the declared/validated
linker, Windows SDK, actual isolated ILCompiler, and complete NativeAOT
runtime-pack identities. This is not a binlog or proof that independently
reconstructs every conditional SDK import or causally traces each tool binary
into the output. The installed SDK directory remains a trusted host
prerequisite; its selected SDK/import paths are pinned and wildcard extension
hooks are disabled, but the complete SDK tree is not content-hashed.

`scripts/test-efi-artifact.ps1` treats that evidence as untrusted input. It
independently recomputes every source, restore, package, tool, and artifact
association, revalidates the generated restore semantics and post-restore item
closure, bounds PE parsing, requires PE32+ AMD64 EFI-application shape, rejects
imports and fallback binding, and checks section ranges,
executable/nonwritable serializer bytes, the exception directory,
runtime-function range, unwind RVA, and exact unwind bytes. It also rejects
truncation, overlap, integer overflow, oversized inputs, ambiguous
runtime-function coverage, writable code, executable or writable unwind data,
stale hashes, mismatched map/response/object/source evidence, and a missing,
noncanonical, mutated, or expanded restore/package closure. Runtime-function
records and response tokens have explicit capacity ceilings.

The verifier pins `ProjectPath` and `EntrySourcePath` to the exact
`src/OS/OS.csproj` and `src/OS/Boot/EfiEntry.cs` below its own repository
root. It rejects lexical traversal and alternate or sibling source contracts,
requires exactly 57 mutually distinct single-link source inputs, and still
allows the artifact/map/response/evidence bundle itself to be copied to a
different directory for independent inspection. Its independent post-restore
MSBuild evaluation also runs in a verifier-local kill-on-close Job Object: the
child is created suspended, assigned before resume, and given bounded strict
UTF-8 output plus a fixed timeout. Timeout, output overflow, capture failure,
nonzero exit, or inability to confirm that the complete descendant tree
terminated fails with a distinct stable reason. Node reuse, build-server reuse,
shared compilation, telemetry, and decorative CLI output remain disabled.

The parent creates a startup-gated Windows PowerShell wrapper, assigns that
wrapper to a kill-on-close Windows Job Object, starts bounded asynchronous
stdout/stderr capture, and only then releases the gate that permits direct
invocation of the artifact verifier. This closes the pre-assignment descendant
race: verifier code cannot run or create a child before the job owns its
wrapper. A fixed 60-second verifier timeout has no retry, bounds both output
drains, and treats verified whole-job termination separately from normal
nonzero verifier exit. The host timeout fixture also exercises an explicitly
unkillable test override and requires the build to refuse a false
`terminationConfirmed` result.

That hostile host fixture uses a five-second verifier deadline, which is the
minimum accepted by the production command surface, so startup-gated nested
PowerShell has time to publish both verifier and descendant process identities
before the deliberately sleeping verifier times out. Its separate 45-second
outer harness bound leaves room for the verifier's own termination and
output-drain diagnostics to remain authoritative under host contention. These
are test-only timing bounds: the production verifier remains fixed at 60
seconds with no retry, and neither Job Object ownership nor any drain or
termination check is weakened.

The broader PE/link-evidence negative-fixture wrapper likewise allows 45
seconds for a verifier process to return its precise stable diagnostic. The
same hard 45-second ceiling bounds each maximum-capacity verifier process,
while stable in-process markers separately require both the 4,096-token link
response scan and the 2,048-record runtime-function scan to complete within
one second. The response timer accumulates tokenization, option-policy regexes,
map-path parsing, and every token's object-path normalization/match pass, while
pausing around linked-map authority-handle and identity-hash I/O. This
separates bounded parser work from comprehensive verifier startup,
source-closure, authority-handle, identity-hash, and final unchanged-input
checks. Increasing the generic outer guard therefore prevents host scheduling
from replacing a useful rejection reason with a wrapper timeout without
relaxing either measured parser-performance criterion.

Restore, both project-evaluation passes, the NativeAOT publish, and the final
SDK recheck use the same startup-gated Job Object ownership model with bounded
output and an explicit build-process timeout. MSBuild node reuse and shared
compilation are disabled so a timed-out descendant cannot escape through a
pre-existing build server.

Before final evidence is accepted, output ancestry is validated without
following junctions or reparse points. Existing output leaves are accepted for
replacement only when they are ordinary single-link files at the exact
resolved paths; new outputs use non-overwrite creation after that guarded
cleanup. The EFI, map, response, evidence JSON, preserved and linked serializer
objects, linked map, and managed assembly must have distinct Windows file
identities and expected link counts. The producer and verifier hold read
handles that deny mutation/replacement through the entire verification pass,
then independently rehash the held files immediately before reporting success.
The canonical 57-source closure receives the same single-link and
distinct-identity requirement. Package and tool inputs are reparse-checked and
mutation-locked; they are not claimed to be globally unique file identities.

The final frozen-source artifact counts, scan timings, source and restore
closure hashes, and two-copy wall-clock-separated reproducibility results are
recorded in `done/step886.md`. The lifecycle check separately proves that build
creates the exact object, `dotnet clean` removes it through the normal
file-write ledger, and rebuild regenerates the same 336 bytes and SHA-256
`DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.

The bounded reference influence for this integration is architectural:

- `references/SharpOS-main/bootasm/CoffStub.Generator/CoffStub.Generator.targets`
  (SHA-256
  `C48D6A90385E162E9073B9BCCCF303156151905E598B1F4E84A5E1C6345DCA5F`)
  informed the idea of generating a COFF input before the native link.
- `references/SharpOS-main/apps_native/sdk/MinimalRuntime.cs` (SHA-256
  `4BB87E5027014177ABFB120B1C3D214D71A44AC6707E969F2FA8E7DE0BB43AE7`)
  informed the no-stdlib `RuntimeImport`/`InternalCall` binding shape.

CLROS independently constrains the generator closure, object identity, linker
ownership, source snapshot, clean behavior, PE/map/unwind verification, and
negative cases. No reference artifact or output is accepted as CLROS evidence.
The recorded project-owner grant permits this bounded adaptation.

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
point in the EFI path. M01-022 provides one accepted guest log for the
M01-021 post-allocation static recapture and owned-stack transfer. M01-023 adds
a second recapture after allocating the allocator arena, and M01-024 supplies
the separate accepted stage-two guest tuple. M01-025 source-wires another
recapture after the stage-three allocations, but that stage-three path has
source/build evidence only. None of these records proves that its map key
remains valid after later boot-services calls.

The ACPI physical discovery probe proves only pre-`ExitBootServices` table bytes on the tested QEMU/OVMF host. It does not map ECAM, enumerate PCI, size BARs, bind drivers, discover APIC/HPET for use, or prove post-firmware physical-memory access.

Any next-step EFI build work must keep the freestanding constraints explicit. If a new path introduces NativeAOT helper/linker failures, document the exact failing symbol or helper before adding a runtime stub. Do not stub success for allocation, type, exception, or interface-dispatch behavior that is not actually implemented.

## Next Steps

1. Preserve the accepted M01-035/M01-026/M02-017 serializer, owned-root, and
   offset-zero safe-cookie closures plus the M02-003 checked binding and
   M02-004 seven-write `Out8` result.
2. Preserve M02-005's bounded offset-32 `In8` polling and direct COM1 payload
   evidence while keeping it distinct from M02-004's configuration-only marker.
3. Preserve M02-006's harmless panic-reporter readiness marker and distinct
   UEFI evidence line without treating either as a live panic or post-firmware
   serial path.
4. Preserve M02-007's caller-owned GDT encoding/readback marker without
   treating it as `lgdt`, `ltr`, active TSS installation, or a live fault path.
5. Preserve M02-008's temporary guarded-IST arena allocation, stack readback,
   lease release, and firmware free marker without treating metadata as live
   guard mappings, NX enforcement, TSS/IST installation, or fault delivery.
6. Move UEFI structs behind platform interfaces.
   - Keep the low-level UEFI table pointers isolated to the EFI boundary.
   - Expose only the narrow operations required for console diagnostics, memory-map ownership, firmware shutdown state, and optional filesystem/protocol discovery.
7. Implement post-firmware COM1 before enabling any real firmware shutdown.
8. Copy required firmware data, add the typed `ExitBootServices` call adapter,
   and implement the bounded stale-map-key retry only after its M01/M02
   dependencies pass.
7. Route additional platform interfaces through the UEFI-gone simulation path
   as they are introduced.
8. Replace permissive shell-only runtime placeholders with audited runtime
   helpers as subsystems require them.
