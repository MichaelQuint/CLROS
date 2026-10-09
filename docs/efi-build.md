# EFI Build

## Owned ACPI selection profile

`EnableM05OwnedAcpiSelectionProbe` is default off and requires
`Diagnostic`, loaded-image stack, live allocator, ACPI physical discovery,
first real exit and owned-subsystem survival. Publisher, project and verifier
guards reject missing prerequisites and mapping/revocation/panic combinations.
The source-linked selector and pointer endian partial use no framework assembly.
Full-host qualification, strict native publication, linked-byte/stack review and
real guest captures are accepted in [step1123](../done/step1123.md).
See [QEMU](qemu.md#owned-acpi-selection).

## Bounded rethrow and mapping profiles

The compiled builder, verifier and smoke planner share profile prerequisites in
`EfiProfile`. Rethrow requires the original GC chain and excludes other EH,
constructor, dispatch and unwind profiles. Mapping requires first firmware exit
and owned subsystem survival and excludes the panic/revocation variants.

The rethrow verifier binds both retained generated COFF objects, every bridge
byte and relocation, and the exact compiler clause/unwind/runtime-function rows.
That structural check remains separate from callback stack review. Both new
profiles reproduce their previously reviewed EFI and map bytes in
[step1118](../done/step1118.md), with final host/guest qualification in
[step1120](../done/step1120.md). Current [QEMU commands](qemu.md#rethrow-and-mapping-captures)
run the complete C# publication/verifier/image/guest path. Dangerous probe defaults remain false.

## Ordinary verified-exit policy

The #68/#594 core checkpoint in [step1089](../done/step1089.md) introduces
`ClrosBootPolicy` in `src/OS/OS.csproj`: an unqualified project build selects
`VerifiedExit`; selecting any existing diagnostic probe chooses `Diagnostic`.
An explicit `ClrosBootPolicy=Diagnostic` retains firmware-visible ordinary
compatibility when no probe is selected. Combining `VerifiedExit` with a probe,
or supplying an unknown policy, fails project validation. Diagnostic probe
defaults themselves remain false; the normal policy has its own compile symbol.

The publication and runner integration in [step1090](../done/step1090.md) adds
`-BootPolicy VerifiedExit|Diagnostic` to the build, artifact-verification and smoke
commands. Omitting it selects normal exit unless an explicit diagnostic probe is
requested. The ordinary smoke command automatically builds a fresh EFI and image;
explicitly disabling that build or overriding its marker contract is rejected.
The native evidence records the selected policy and verifies it again against
MSBuild and the requested artifact profile. That checkpoint recorded 122 explicit
compile items, 138 source-authority inputs and all 52 diagnostic properties false.
Current source-authority counts are derived from the compiled/tooling inventory.

Ordinary verified firmware exit is accepted under #68/#594 in [step1094](../done/step1094.md). The frozen source `3c986e83447e2955efb25566ecd611676f054b30` passes the original full host gate (3,903 paired groups plus the suite summary), three fresh ordinary 79-record QEMU/OVMF captures and five explicit diagnostic regressions. Native, raw transcript, source and post-capture GPT/FAT/EFI associations pass.

The five diagnostic regressions are firmware-visible compatibility, first exit, owned survival, adapter revocation and expected owned panic. Their EFI bytes exactly match their retained accepted images. Failed full runs and fixture corrections remain in steps1091–1093; the earlier failed wrapper is preserved in step1090.

This is a bounded terminal boot path. It reuses the original retained allocator and copied ACPI, adopts owned descriptors/page tables and reports through owned COM1. It does not reclaim firmware pages or start a continuing workload. General EH, hosted .NET/Kestrel, live storage/networking, cloud and production readiness remain unproved; numbered gate statuses are unchanged.

## Current toolchain migration

The host producer and verifier run as C# 14/.NET 10 commands:
`dotnet run --project tools/OS.Tooling -- efi build` and
`dotnet run --project tools/OS.Tooling -- efi verify`. Add `--preflight-only`
to the build command for discovery and source admission without publication.
Both GNU-style option names and the established PascalCase names are accepted.
`EfiBuildPolicy` owns process timeout/output limits; `EfiContracts` owns reviewed
source, native object, package, and evidence inventories. Source-authority counts
come from the actual compile/tooling inventory, including newly added tool files.
Closure counts in the milestone sections below describe their retained historical
checkpoints. Use the current inventories and emitted provenance for a new build.

Every compiled CLI entry checks an embedded raw-input manifest before dispatch.
The source patterns live once in `OS.Tooling.csproj` and also drive runtime
inventory discovery. Changed, missing, or newly added tool/dependency sources
fail with `TEST_FAIL tooling.inputs ... rebuild_required`; rebuild the tool
instead of running an older binary against new inputs. Exact source copies may
relocate with the compiled runtime. Documentation and generated build outputs
do not invalidate this check. Native provenance includes the same dependency
inventory in addition to the admitted EFI compile inputs.

Publication holds the canonical output mutex and raw source authorities through
one fresh isolated restore, one NativeAOT publish, and a bounded compiled verifier
process. Input identities and bytes are checked again before success. Generated
restore files, package trees, import/task bindings, managed assembly, native
objects, linker response/map, and copied EFI remain mandatory evidence. Output
and log paths reject aliases, hard links and reparse points. Host fixtures test
these services directly; a passing preflight is not native or guest acceptance.

Compiler completion follows the original bounded build contract: retain the
compiler's exit code, terminate any remaining descendants in its assigned job,
prove the job is empty, and finish the bounded output drain. This prevents
background compiler helpers from consuming the build timeout after native
publication has finished. It does not change the QEMU runner's default process
lifecycle or allow successful builds with unconfirmed child termination.

Issue #590 moves the selected freestanding ILCompiler and NativeAOT runtime pack
to `10.0.12`, with stable SDK `10.0.401`. All three compiler/runtime packages
identify dotnet/dotnet revision `95017c711e6afc1085133d440e42b4bd78155701`.
The active layout profile is `ilc10.0.12-win-x64-le`. The reviewed 67 upstream
ABI sources match the previous `10.0.8` sources byte for byte; compiler binaries
differ, so qualification includes fresh native geometry and guest validation.

The aligned restore has three asset-library identities and four package
directories/archives: ILCompiler, ILLink.Tasks, the win-x64 host compiler and
NativeAOT runtime. Five consumed-tree records retain the separate restored and
externally selected runtime roots. All archive hashes, complete tree identities,
nine build-input hashes, import edges and task bindings remain checked. There
is no duplicate restore-only host compiler after version alignment.

[Step1077](../done/step1077.md) records the separate SDK401/ILC108 default
checkpoint on the destination host. [Step1078](../done/step1078.md) records the
completed compiler qualification: 3,892 host groups plus the suite summary,
ten bounded native/guest profiles and three current-image rejection controls.
The host producer is `46ad1a2`; guest producers remain `770293d`, joined through
the same 137 native authority inputs. This finite matrix covers default,
String, collecting GC, churn, constructors/dispatch, unwind metadata, simple
catch, and first-exit survival/revocation/panic. Historical acceptance sections
below retain their original source, toolchain, counts and hashes. Their package
counts and versioned markers must not be substituted for the current source
contracts in the publisher, verifier and runner. No optional-profile acceptance
outside that matrix or completion gate is renewed by changing package versions.

## Accepted diagnostics and dispatch integration

Shared issue #572 adds `EnableM04InterfaceDispatchProbe`, disabled by default.
It requires `EnableM04ClassConstructorExecutionProbe` and that profile's entire
GC prerequisite chain. It inherits the constructor/churn exclusion. The
compiler-generated calls use the separate C#-authored
`CLROS.X64.InterfaceDispatch.obj`; the existing 336-byte serializer and
1,645-byte reference-barrier objects retain their byte identities. The new
object is 828 bytes, with one callback and one compiler-data REL32 relocation.
The verifier checks object ownership, exact linked code outside those fields,
both relocation targets, RX callback/code and RW initialized cell storage.
Those checks do not by themselves prove the callback graph or dispatch results.

That diagnostics/dispatch wave introduced 114 Compile items, 129 raw inputs and 47
feature properties (44 literal false defaults, with three inherited defaults).
The generator has five C# files and seven declared inputs. Final acceptance at
`d5bef46` derives project/source hashes and fixture vectors from those actual
inputs ([step1063](../done/step1063.md)). For later changes, normalize authored
text before pinning it and preserve raw byte hashes in evidence.

Final native review binds the actual 112-byte cell contribution and four-byte
base sealed table; A/B use ordinary vtable slots and have no sealed tables.
The current dispatch image passes all 158 records and 15 real calls. Its
current-image null-receiver control observes initialized GC context zero and
halts before target entry. The original full host gate passes 3,845 starts,
3,846 passes and zero failures; six fresh captures and three rejection controls
have independent native/raw/lifecycle/disk review in [step1063](../done/step1063.md).
The original failed timed-stop control is preserved; repaired controls record
complete stop state and enforce a bounded unchanged-serial halt observation.
All three passing quiet samples remained on the original loops, so they do not
exercise the repair's off-loop reacquisition branch. No positive capture was
repeated for that driver-only change.

The M03-009 owned diagnostic tail changes the current first-exit/revocation
contracts to 66/78. The following section retains the preceding accepted
revision's 61/73 contracts as history. Step1054 retains development evidence;
step1056 retains the catalog failure/correction and recovery boundary.

## Retained firmware and constructor profiles

Integration #570 introduces two default-off child profiles. Development evidence
remains in [step1052](../done/step1052.md); [step1053](../done/step1053.md) accepts
the original full host gate and five fresh default/churn/first-exit/revocation/
constructor captures at `2b5a4b0`. All final source/native/raw/lifecycle/disk
associations pass, including two fresh corruption controls for the changed
churn image. Historical control results are not relabelled as current runs.

`EnableM03FirmwareAdapterRevocationProbe` requires
`EnableM03FirstRealExitBootServicesProbe` and its three prerequisites:
`EnableM01LoadedImageStackProbe`, `EnableM01LiveAllocatorProbe` and
`EnableAcpiPhysicalDiscoveryProbe`. It preserves the first-exit terminal path
and requires exactly 73 application records, including eight copied capability
denials and `CLROS_M03_008_READY`.

`EnableM04ClassConstructorExecutionProbe` requires
`EnableM04GcStaticRootMaterializationProbe` and all eight GC prerequisites
listed in the seeded-churn section below. It is mutually exclusive with
`EnableM04SeededCollectorChurnProbe`. Its 82-record transcript has nine numbered
constructor rows and `CLROS_M04_024_READY`; it is a distinct grammar from the
82-record churn transcript. The compiler helper type is completely absent
outside this profile, because its presence changes ILC lazy-initialization
policy.

That accepted revision preserved the original Compile ordering and
appended four C# inputs: 112 Compile items, 126 source-authority inputs and 46
explicit feature switches. Every source/project pin must derive from those
actual bytes before a frozen acceptance run. Direct publisher output must be a
file directly inside `artifacts/efi`; the QEMU wrapper passes `-EfiArtifact`
through to that publisher and has the same restriction when `-BuildEfi` is
selected. Give the EFI file a unique name there, with distinct logs and image
paths in each run directory, and keep rejected development receipts.

## Seeded collector churn profile (M04-014)

The default-off `EnableM04SeededCollectorChurnProbe` extends the existing GC
profile. Pass it together with `EnableM04GcStaticRootMaterializationProbe` and
all eight GC prerequisites: `EnableM01LoadedImageStackProbe`,
`EnableM01LiveAllocatorProbe`, `EnableM04IndependentEndianConformanceProbe`,
`EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe`,
`EnableM04ArithmeticConformanceProbe`, `EnableM04LayoutContractProbe`,
`EnableM04ManagedHeapPageRangeProbe` and `EnableM04RhpNewFastProbe`.

Use `dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi` with those ten switches and the QEMU/OVMF
arguments below. The wrapper requires a fresh image and validates exactly 82
application records, including eleven churn rows and `CLROS_M04_014_READY`.
The three fixed seeds complete 192 rounds, 476 direct allocations/reclamations,
198 collections and four read-only stack-canary checks. This is a bounded
diagnostic of the original explicit-root collector before ExitBootServices.

The project, publisher and verifier carry the same explicit switch; the verifier
requires the linked adapter only when selected. The runner loads the shared
`QemuTranscripts.AssertM04SeededCollectorChurnRecords` validator, first validates the original GC
records, then binds churn identities and counters to them. GC-only/default
profiles reject churn output. The first-exit profile is incompatible with GC
and therefore with churn. Combined integration evidence is in
[step1040](../done/step1040.md) and [step1042](../done/step1042.md).
[step1051](../done/step1051.md) records final common and fresh default/churn/first-exit
acceptance at `e64e9dc1bd1f66ca522bd22aafe8bcd3388428ff`, including
source/native/lifecycle/disk association. The profiles have exactly 21, 82 and 61
application records respectively; churn remains a bounded pre-EBS diagnostic.

## First real exit profile (M03-007)

The integrated, default-off first-exit profile has scoped acceptance under #64
and shared lane #560 at `e64e9dc`; see [step1051](../done/step1051.md).
[Step1032](../done/step1032.md) and [step1042](../done/step1042.md) preserve preparation.
The source-fixture selector migration in [step1046](../done/step1046.md)
keeps first-exit publications and security-cookie storage out of historical
default profiles while preserving their exact implementation guards. That
host-fixture evidence remains distinct from the later accepted common and fresh
guest batch.

[step1050](../done/step1050.md) corrects five existing fixture assumptions about
profile relationships, raw project identity and the mutually exclusive
security-cookie owners. Independently reviewed diagnostic evidence accounts
for all 421 original initializers and both original main checks. The failed
attempts and separate complete-module recovery remain recorded. The final
reviewer also binds both retained churn corruption controls through 81 reference
identities. The later original unfiltered common run passed with 3814 starts,
3815 passes including one `TEST_PASS host.tests`, and zero failures; all five
common phases exited zero with clean, unchanged inputs. [step1051](../done/step1051.md)
records the separate final native/guest acceptance. Failed and supplementary
diagnostic receipts in step1050 remain unchanged.

The exact accepted invocation and archived paths are retained in
[step1051](../done/step1051.md) and
`artifacts/m03-m04-final-acceptance/integrated-e64e9dc/final-capture/first-exit/wrapper.command.json`
in the primary checkout. It used Windows PowerShell, `-BuildEfi`, all four
switches below and a 30-second guest timeout.

For a future reproduction, run from the intended clean CLROS checkout. This
command creates a new GUID-named artifact directory and uses fresh output paths;
it does not write into the sealed acceptance capture:

```powershell
$firstExitId = [Guid]::NewGuid().ToString('N')
$firstExitRun = Join-Path (Get-Location).Path ('artifacts\first-exit-repro-' + $firstExitId)
New-Item -ItemType Directory -Path $firstExitRun | Out-Null
dotnet run --project tools/OS.Tooling -- qemu smoke `
  -BuildEfi `
  -Qemu "C:/Program Files/qemu/qemu-system-x86_64.exe" `
  -OvmfCode "C:/Program Files/qemu/share/edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -EfiArtifact (Join-Path (Get-Location).Path ('artifacts\efi\first-exit-' + $firstExitId + '.EFI')) `
  -Image (Join-Path $firstExitRun 'clros.raw') `
  -SerialLog (Join-Path $firstExitRun 'serial.log') `
  -SummaryLog (Join-Path $firstExitRun 'summary.log') `
  -EnableM03FirstRealExitBootServicesProbe `
  -EnableM01LoadedImageStackProbe `
  -EnableM01LiveAllocatorProbe `
  -EnableAcpiPhysicalDiscoveryProbe
```

All three prerequisite switches are required; other diagnostic profiles are
rejected. The publisher forwards the switch to actual compilation and verifies
the linked Enter/Diagnose/Fault callbacks. The runner requires a fresh build and
disk, the exact 61 application records, copied-ACPI numeric consistency, 750 ms
quiet drain and runner-owned teardown. Marker/preset overrides, alternate raw
observers and trailing output are rejected. The guest halts after owned COM1
reporting; runner exit zero does not mean EFI returned to firmware. This profile
does not reclaim firmware pages or start a workload.

## Current Status

An explicit EFI NativeAOT project shell exists at `src/OS/OS.csproj`. It is
included in `CLROS.slnx`, but its directory-local rules keep the freestanding
no-stdlib build explicit and separate from ordinary managed runtime policy.

As of 2026-07-27, this host has the required Visual Studio C++ toolchain,
Windows SDK, QEMU, and OVMF installed. `dotnet run --project tools/OS.Tooling -- efi build` publishes a
NativeAOT x64 PE with EFI application subsystem and stages it as
`artifacts/efi/BOOTX64.EFI`.

The historical M01 staging checkpoint below enabled `M01_LOADED_IMAGE_STACK_PROBE`,
`M01_LIVE_ALLOCATOR_PROBE`, and `M01_OWNED_PAGING_PROBE`. It contains the
M01-021 loaded-image/page-allocation/map/ledger/owned-stack wiring, the M01-023
allocator-arena adapter, and M01-025's stage-three source wiring. M01-024 owns
the accepted stage-two QEMU/OVMF tuple. The M01-025 stage-three artifact had
host/source and PE-verification evidence only at that checkpoint; it had not
executed under QEMU/OVMF. Later accepted profiles are recorded above and in
the owned-subsystem section below.

A separate default-off M02-010 artifact has now been built, independently
verified, and executed under QEMU/OVMF from exact source head
`b88a485c8562a0022ffaae760fb47ca9d5addba2`. It materializes and reads back one
temporary 4,096-byte normalized interrupt-entry page and releases it before
ordinary memory-map capture. It does not execute the generated page or advance
a numbered completion gate.

A later, mutually exclusive default-off M02-011 profile now composes that
frozen normalized-entry implementation with the owned-IDT gate materializer in
one temporary three-page LoaderData arena. A NativeAOT artifact has passed the
same-build verifier and a real QEMU/OVMF run has observed the exact M02-011
population/readback/release transcript. The profile does not publish code,
load IDTR, execute an interrupt entry, or advance a numbered completion gate.

The later default-off `EnableM02IdtrContinuedExecutionProbe` profile now links
the frozen M02-010/M02-011/M02-012 sources. It prepares two LoaderCode pages,
one LoaderData IDT page, and an aligned caller-stack context; publishes and
synchronously invokes the exact reversible transition; verifies raw IDTR/IF
restoration before any firmware callback; audits; frees data then code; and
returns to ordinary memory-map capture. Default and enabled NativeAOT artifacts
have passed same-build verification. Exact QEMU profile dry-run tests pass, and
the accepted 2026-08-26 QEMU/OVMF tuple records the reversible transition and
ordinary-smoke continuation.

The later default-off `EnableM02Ud2VectorStateProbe` profile source-links its
reviewed M01/M02 dependencies, allocates two LoaderCode and five LoaderData
pages, builds fresh owned GDT/TSS/IDT/context/IST state, and enters a frozen
terminal vector-6 transition only after `OS_SMOKE_PASS`. Default/enabled
NativeAOT artifact, same-build verifier, typed dry-run, focused host, and real
QEMU/OVMF evidence pass. This profile deliberately never returns and remains
pre-`ExitBootServices`; it is not the reusable fault path or hosted-.NET app
boundary.

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
- M03-001's [step961](../done/step961.md) adds host regression coverage without changing the EFI capture path. Its actual no-stdlib closure uses the current `MinimalRuntime.cs`, `MethodImplAttribute.cs`, `BootHandoffRecord.cs`, `UefiMemoryMapCapture.cs` and `UefiMemoryMapCopyPlan.cs`; compilation and metadata inspection report zero AssemblyRefs. The complete new and existing host memory-map initializers pass. No new EFI artifact, guest marker or live firmware result is claimed by that step; the later native observation and QEMU evidence are recorded in [step970](../done/step970.md).
- M03-002's [step972](../done/step972.md) adds the host-only `RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap` composition and complete regressions for retaining five full ACPI tables. It consumes caller-supplied snapshots through `Span<T>` and the existing `ReadOnlyMemory<byte>` table-source contract. That managed host composition remains outside the explicit EFI closure. The separate no-stdlib adapter and its default/enabled publications are now integrated in [step973](../done/step973.md). Those host and publication results alone did not demonstrate live copies. [Step977](../done/step977.md) adds the accepted common gate and fresh five-role ACPI/native/raw/lifecycle/disk evidence before firmware exit. PR538 later merged and #59/#539 closed with evidence; no post-exit access is proved.
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
- `dotnet run --project tools/OS.Tooling -- efi build` runs the explicit publish command and writes a
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
- `dotnet run --project tools/OS.Tooling -- efi build -EnableUefiGoneSimulationProbe` defines `UEFI_GONE_SIMULATION_PROBE`, prints `EFI_PROBE UEFI_GONE_SIMULATION`, and publishes a variant that emits `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.
- `dotnet run --project tools/OS.Tooling -- efi build -EnableAcpiPhysicalDiscoveryProbe` defines `ACPI_PHYSICAL_DISCOVERY_PROBE`, prints `EFI_PROBE ACPI_PHYSICAL_DISCOVERY`, and publishes a variant that reads firmware-provided ACPI physical addresses before `ExitBootServices`, validates RSDP/root SDT/MCFG bytes into fixed scratch storage, and emits `ACPI_RSDP_SNAPSHOT_READY`, `ACPI_RSDP_VALIDATED`, `ACPI_ROOT_SDT_SNAPSHOT_READY`, `ACPI_ROOT_SDT_VALIDATED`, and `ACPI_MCFG_DISCOVERY_READY`.
- On success, the publish script copies `src/OS/bin/Release/net10.0/win-x64/publish/OS.exe` to `artifacts/efi/BOOTX64.EFI`.
- `dotnet run --project tools/OS.Tooling -- efi verify` parses the staged PE directly and rejects
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

`dotnet run --project tools/OS.Tooling -- efi build` validates the direct XML and evaluated MSBuild
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

`dotnet run --project tools/OS.Tooling -- efi build -EnableM01LoadedImageStackProbe` maps the explicit
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
application. The historical `scripts/test-efi-artifact.ps1` reported executable/file-backed
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
dotnet run --project tools/OS.Tooling -- efi build -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe
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
dotnet run --project tools/OS.Tooling -- efi build -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe
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
compile closure at that checkpoint contained 48 items, and its build/verifier source
snapshot contained 61 inputs. `dotnet run --project tools/OS.Tooling -- efi build` and
`dotnet run --project tools/OS.Tooling -- efi verify` pin the current reviewed inventories; missing,
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

The M02-003/M02-004 checkpoint added
`Diagnostics/InvokeIn8Out8CheckedUnmanagedFunctionPointers.cs`,
`Diagnostics/COM1OwnedPortIOBackend.cs`,
`Boot/M02CheckedPortIoFunctionPointerIntegration.cs`, and
`Boot/M02Com1OwnedPortIoBackendIntegration.cs`. The explicit EFI compile
closure contained 52 items and the canonical build/verifier authority snapshot contained 65
inputs. `EnableM02Com1OwnedPortIoProbe` defaults false and requires
`EnableM02CheckedPortIoBindingProbe`, which in turn requires the complete
M02-017/M01 probe tuple.

With every required switch enabled, that checkpoint's preflight reported
`EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=65` and
`EFI_PREFLIGHT_M01_SOURCE_CLOSURE status=ok reviewed=31 prerequisites=3 compileItems=52`.
The same-build verifier accepted PE32+ AMD64, EFI subsystem 10, no ordinary or
delay imports, the exact `EfiMain` association, and the linked instruction
serializer. The retained 485,888-byte artifact SHA-256 is
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
M02-006 development checkpoint was 55/68 after inserting its separately owned
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
dotnet run --project tools/OS.Tooling -- qemu smoke `
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
M02-005 writer and before the independent endian catalog. That checkpoint's closure
contained 55 compile items and its build/verifier authority snapshot contained 68
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
dotnet run --project tools/OS.Tooling -- qemu smoke `
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
M02-006 reporter and before the independent endian catalog. That checkpoint's closure
contained 56 compile items and its build/verifier authority snapshot contained 69
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
dotnet run --project tools/OS.Tooling -- qemu smoke `
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
accepted M02-007 encoder and before the independent endian catalog. The retained
development closure contained exactly 62 compile items and 75 source-authority inputs
after the default-off M02-009 terminal stub, M02-010 normalized interrupt-entry
page, M02-011 owned-IDT materializer, M02-012 IDTR transition source, and
M04-003 span/memory catalog sources were added after this historically accepted
slice.
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
dotnet run --project tools/OS.Tooling -- qemu smoke `
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
catalog. That explicit development checkpoint therefore contained 62 compile items
and 75 source-authority inputs after the later default-off M02-010 page,
M02-011 owned-IDT materializer, M02-012 IDTR transition source, and M04-003
catalog were inserted around the endian catalog.
The flag requires
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
dotnet run --project tools/OS.Tooling -- qemu smoke `
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

Step 918 records a fresh current-source QEMU/OVMF acceptance for this profile.
QEMU 11.0.50 with OVMF SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`
accepted all 59 required/ordered/unique markers, rejected all 43 forbidden
markers, and reported `QEMU_PROCESS_RESULT disposition=runner-terminated`.
The accepted EFI SHA-256 is
`A22F5B2AF24682693C09E361C16EE89271CDB9D71FA5D9BBFA32ABA784F8D443`;
the serial SHA-256 is
`3AAF960E6ABCB7F27A4580BF8B8D554FDB66EA165F71816C38C7B81A050B6BAF`.
The allocation types express intended code/data use, but UEFI allocation types
do not enforce page permissions; this implementation does not prove W^X or NX.
All M02-009 evidence remains pre-`ExitBootServices`. The profile does not
install an IDT or fault handler, trigger a fault, prove post-firmware owned
diagnostics, survive real firmware exit, advance a numbered completion gate,
run hosted .NET or Kestrel, establish provider support, or establish CloudReady
or production readiness.

### M02-010 normalized interrupt-entry page EFI integration

The default-false `EnableM02NormalizedInterruptFramePageProbe` profile links
the frozen
`Diagnostics/NormalizeErrorCodeNonErrorCodeInterruptFrame.cs` source after the
M02-009 terminal-stub source and before both M04 conformance sources. When
enabled it defines exactly `M02_NORMALIZED_INTERRUPT_FRAME_PAGE_PROBE`. It does
not silently enable an M01, M02, or M04 dependency, and it is mutually
exclusive with the nonreturning M02-009 terminal profile.

The retained same-build closure contained exactly 60 compile items with compile-key
SHA-256
`805E39822E162FD57BE8421D6D19282267D8B71222602BB359C81ACE9E7BA2E1`
and 73 source-authority inputs with closure SHA-256
`08DB2154F4E6A45B1F020CB9D0562C43A062E13EE6537AA46FA4169D2F314975`.
The raw `OS.csproj` SHA-256 is
`90A9243917910A9516388ABB301D9BEC92F661C6A839DBFA25E24497288C2769`;
its canonical evaluated XML-shape SHA-256 is
`05C6372ADA146E1CF78DFF3FB1D2C63601F788CF650172292F4D8724E15DBF45`.
Post-restore evaluation found zero project references and zero ordinary
references. The linked frozen source has SHA-256
`B66CD19B45E8198897B837DF9EDA6934AFBE087DC141C468BD0DB44E62724DA1`.

After firmware console discovery and optional M04 pre-memory-map checks, but
before `TryCaptureUefiMemoryMap`, the profile emits `TEST_START m02.010`,
requires live `AllocatePages` and `FreePages`, and allocates exactly one page
using `AllocateAnyPages`, `EfiLoaderData`, and page count one. The address must
be nonzero, 4,096-aligned, canonical through its final byte, and free of
arithmetic wrap. The real page address is also the modeled page address; the
modeled-only common target is exactly `pageAddress + 4096`. EFI success
establishes page ownership before address validation, so a successful
allocation returning address zero is released exactly once before failure.

The path calls pointer `Materialize` exactly once and pointer
`ValidateReadBack` exactly once afterward. It requires initialized successful
results, the exact 4,096-byte capacity/write/compare counts, nine hardware-
error-code vectors, 247 synthetic vectors, three normalized semantic slots,
matching modeled addresses, no mismatch, and FNV-1a64
`428D0353A96B7ACE` from both operations. It then calls `FreePages` exactly once
after the readback attempt. All post-allocation failure paths clean up once;
ready/pass requires successful release. The independent modeled page SHA-256
is `71325A5633E1985BB22B4F8E1581399F63FEDEEB8CBFA1E3507A3F4ED73A71AF`.

Success emits exactly:

```text
TEST_START m02.010
CLROS_M02_010_READY vectors=256 hardwareErrorCodeVectors=9 syntheticErrorCodeVectors=247 slotBytes=16 pageBytes=4096 normalizedSlots=3 commonTargetDelta=4096 fnv1a64=428D0353A96B7ACE readBack=true guestMaterialization=true loaderData=true pageReleased=true stubExecution=false idtInstalled=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.010
```

Any missing service/function, allocation or address rejection, materialization
or readback mismatch, result-audit failure, or cleanup failure emits
`M02_NORMALIZED_INTERRUPT_FRAME_PAGE_FAILED` and
`TEST_FAIL m02.010 guest_materialization_readback_or_cleanup`, suppresses the
ready/pass records, and aborts the optional profile.

The accepted real command was:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -BuildEfi `
  -EnableM02NormalizedInterruptFramePageProbe `
  -EfiArtifact .\artifacts\efi\BOOTX64-M02-010-b88a485c-QEMU.EFI `
  -Image .\artifacts\images\m02-010-b88a485c-qemu.raw `
  -SerialLog .\artifacts\logs\issue-487-m02-010-b88a485c-qemu-serial.log `
  -SummaryLog .\artifacts\logs\issue-487-m02-010-b88a485c-qemu-summary.log
```

The schema-three link evidence was generated at
`2026-08-25T22:25:56.1118242-04:00` from implementation head
`b88a485c8562a0022ffaae760fb47ca9d5addba2`, tree
`2c214fdee0ac2e08dd3df7a62523a5c69ca4a5fe`. The production `EfiEntry.cs`
source has SHA-256
`76C15B0296A3E4FD51D9EAEF943B57D846E93050110B636AE57E5DBADE39A48F`.
The artifact verifier accepted a
67,072-byte PE32+ AMD64 EFI application, subsystem 10, entry RVA `0x5330`, the
same-build `EfiMain`/serializer bindings, and no ordinary, bound, IAT, or delay
imports. Artifact identities are:

| Artifact | Bytes | Lines | SHA-256 |
| --- | ---: | ---: | --- |
| EFI | 67,072 | n/a | `9D7EE566D93ED876DDBE1825CC5629330CF46DEEB92F23473F8C6E3A06249B6D` |
| Serializer object | 336 | n/a | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| Link map | 38,683 | 404 | `E8DDA9078650D6590963990F09C3387383EF15165FFD8FB94CE78338CDD4D1CF` |
| Link response | 500 | 15 | `F88A91FBB859939851CCEBB865B680940F9ECE1193DDB8F8A1452BAF31B1A96C` |
| Link-evidence JSON | 398,582 | 5,154 | `90589CAF8365E172824393F761E35195228B2B7860E518D4F82983CE9EFEF65C` |
| Archived build log | 4,622 | 39 | `626C61613A9FFE189B040FE8BE904ED6F15CAD9C1DD8423FAB3027DDCDE9AF86` |

The accepted QEMU/OVMF run observed the exact guest transcript once, then
continued through the ordinary memory-map and boot-smoke markers. It proves
only temporary pre-`ExitBootServices` LoaderData materialization, production
readback, deterministic identity, and release. The page was not allocated as
LoaderCode or published executable; no slot or common target was called or
jumped to; no IDT/IDTR/IST was installed; and no live interrupt, normalized CPU
frame, or fault was produced. This does not advance Gate 3 or any other
numbered completion gate and does not prove hosted .NET, Kestrel, provider,
CloudReady, security-hardening, or production behavior.

### M02-011 owned IDT gate population EFI integration

The default-false `EnableM02OwnedIdtGatePopulationProbe` profile links the
frozen
`Diagnostics/PopulateAllN256IDTVectorsOwnedGatesNever.cs` source immediately
after the M02-010 normalized-entry source and before both M04 conformance
sources. When enabled it defines exactly
`M02_OWNED_IDT_GATE_POPULATION_PROBE`. It does not silently enable another M01,
M02, or M04 profile. It is mutually exclusive with the standalone M02-010
profile and the nonreturning M02-009 terminal profile, while it may be combined
with the returning M04-002/M04-003 profiles.

The accepted M02-011 same-build closure at its evidence head was exactly 61
compile items with compile-key SHA-256
`413DDC044A3469F2F0AC51C1190609EDEAA8FBAC0D90BEB41AB2648559F6549E`
and 74 source-authority inputs with closure SHA-256
`2D24A9B956642DDE73162A34DC0E84A426B92E70D39A21B064003ABDE9171240`.
The raw `OS.csproj` SHA-256 is
`3FFB00E8BA1EBEAD05233CE2EEE860CF1AB6BAF8721B8BD29A2AB958F1C7B994`;
its canonical evaluated XML-shape SHA-256 is
`3F57CBB5DE5561C86A0AD1809534BD5D0E6DB8D8086BF667BA724666413F7181`.
Post-restore evaluation found zero project references and zero ordinary
references. The frozen M02-010 and M02-011 production sources have SHA-256
`B66CD19B45E8198897B837DF9EDA6934AFBE087DC141C468BD0DB44E62724DA1`
and
`00EC277C3F6A0DB49CB54B34810202EE1D508318B29733C3F0392A7BC36CFA38`,
respectively. The integrated `EfiEntry.cs` source has SHA-256
`EBDFCFA71377045E271722EEA786900B5060137347B9322392ED465DC496C9CD`.

After firmware console discovery and optional M04 checks, but before ordinary
memory-map capture, the profile emits `TEST_START m02.011`, requires live
`AllocatePages` and `FreePages`, and requests one three-page `EfiLoaderData`
arena. Page zero receives the 256 normalized M02-010 slots, page one receives
the 256 M02-011 IDT descriptors, and page two owns the modeled common target at
delta 8,192 without being written or executed. A successful firmware allocation
establishes ownership before returned-address validation, so zero, misaligned,
noncanonical, wrapping, and canonical-hole-crossing addresses are still passed
to exactly one three-page `FreePages` call.

This probe finishes allocation, validation, materialization, readback, and
release before the ordinary UEFI memory-map capture and never calls
`ExitBootServices`. Therefore `EFI_BUFFER_TOO_SMALL`, memory-map descriptor
growth, stale map keys, and `ExitBootServices` retry exhaustion are not M02-011
outcomes; those statuses remain owned by the later memory-map and firmware-exit
work.

For a valid arena, the path calls M02-010 `Materialize` and
`ValidateReadBack`, then M02-011 `Materialize` and `ValidateReadBack`. It audits
every status and count, the 4,096-byte capacities, all 256 slots and gates, the
nine/247 error-code classification, the fixed selector and IST policy, zero
copied firmware gates, M02-010 FNV-1a64 `641BA73668C75E0E`, and M02-011
normalized ownership fingerprint `8514F42EBDFC5D55`. Success is withheld until
the arena is released. The independent M02-010 page SHA-256 for target delta
8,192 is `650F6D5EB96B0E625C2FF5FB852C0A67C6CE68D0CB92CD4B137E4DBC28636630`.

The SharpOS audit supplied only a reference concept for this slice:
deterministic descriptor/stub bytes written in C#. CLROS copied no SharpOS
source and did not adopt SharpOS's copied firmware vectors 32-255, its selector
`0x38`, or its `sidt`/`lidt` installation approach. The profile creates all 256
gates from CLROS-owned inputs with selector `0x0008`, copies zero firmware
gates, and leaves IDTR untouched.

Success emits exactly:

```text
TEST_START m02.011
CLROS_M02_011_READY vectors=256 gates=256 gateBytes=16 tableBytes=4096 selector=8 present=256 dpl0=256 interruptGates=256 ownedHandlers=256 firmwareGatesCopied=0 nmiIst=3 doubleFaultIst=1 pageFaultIst=2 normalizedFnv1a64=8514F42EBDFC5D55 readBack=true guestPopulation=true loaderData=true allocationPages=3 allocationReleased=true stubExecution=false idtrLoaded=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.011
```

Any missing service/function, allocation or address rejection, materialization
or readback mismatch, result-audit failure, or cleanup failure emits
`M02_OWNED_IDT_GATE_POPULATION_FAILED` and
`TEST_FAIL m02.011 guest_population_readback_or_cleanup`, suppresses the ready/
pass records, and aborts the optional profile before ordinary memory-map
capture.

The direct NativeAOT build/verifier command is:

```powershell
dotnet run --project tools/OS.Tooling -- efi build `
  -EnableM02OwnedIdtGatePopulationProbe `
  -EfiOutputPath .\artifacts\efi\BOOTX64-M02-011.EFI `
  -LogPath .\artifacts\logs\m02-011-efi-build.log
```

The verifier accepted a 72,192-byte PE32+ AMD64 EFI application, subsystem 10,
entry RVA `0x5F30`, 145 exception/runtime records, the same-build
`EfiMain`/serializer bindings, and no ordinary, bound, IAT, or delay imports.
The real-QEMU build produced the following same-build artifact tuple:

| Artifact | Bytes | Lines | SHA-256 |
| --- | ---: | ---: | --- |
| EFI | 72,192 | n/a | `433B087FB4D0ADBDE01DFF47F7DCB7DE7E061974ED7329138A5AD0B8250255ED` |
| Serializer object | 336 | n/a | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| Link map | 42,436 | 433 | `28A7CD7615E654E8C68DAE24B6865B1CB211B8DE3E5275BD26E35A09B3F719FD` |
| Link response | 500 | 15 | `156EB9321ED154D124A4E4CA8403DA23EE94F01836D430DC6AA3458BACD22103` |
| Link-evidence JSON | 398,982 | 5,159 | `A99E85F3720B3E76980091BAB8BC6AA23EB8A7EBBF22750D2C9502DE7DFE1CF7` |

The real QEMU/OVMF profile required 17 records, ordered five, required the three
M02-011 transcript records to be unique, and forbade three failure records. The
streaming parser reported `status=Passed`, `terminal=Success`, `bytes=8430`,
`lines=29`, `orderedSeen=5`, `outOfOrder=0`, `uniqueSeen=3`, and
`duplicateUnique=0`; the ordinary path subsequently emitted `OS_SMOKE_PASS`.
This proves only temporary, pre-`ExitBootServices` population, production
readback, exact cleanup, and the observed marker stream. It does not prove
executable publication, W^X/NX, common-entry code, `lidt`, an active IDT or
GDT/TSS/IST path, slot execution, interrupt delivery, fault capture,
post-firmware diagnostics, hosted .NET, Kestrel, cloud-provider behavior,
CloudReady status, or production readiness. Gate 2 remains the only passing
numbered gate; Gate 3 and Gate 4 remain `Fail`.

### M02-012 reversible IDTR continued-execution EFI integration

The default-false `EnableM02IdtrContinuedExecutionProbe` profile defines exact
symbol `M02_IDTR_CONTINUED_EXECUTION_PROBE` and source-links the reviewed
`Diagnostics/IDTRContinuedExecution.cs` leaf immediately after its frozen
M02-010 and M02-011 dependencies. At the accepted M02-012 evidence head, the
explicit closure was 62 compile items with compile-key SHA-256
`9B243B135C05442631A0323654FDBE07C827F043703330D8BAA9E83A78A21757`
and 75 source-authority inputs with closure SHA-256
`F1543BCDE0DF38127068CF692188A8510DF9E41A61E49FA5CC5A90C785C37A26`.
At that evidence head, the raw project SHA-256 was
`19831A8A437D6003B51D1E89B6B26325C17417EB38B869CE01C18C04C5952BAC`;
and its canonical evaluated XML-shape SHA-256 was
`DE242129BD59B0358211B606F666C0D99F65AB2E23096DA47EC9B182E7D2748E`.
The integrated `EfiEntry.cs` SHA-256 is
`FA3F7896078F024B6F5D5300E806B24AE6B739184C39A7292E24382F46E082B8`,
and the frozen M02-012 production leaf SHA-256 is
`7CBF5DD9FB17D8D9608237FC78D06FE00B40D7AFE8669E1196C72B9F1B6848EB`.

This standalone profile is mutually exclusive with the nonreturning M02-009
profile and the standalone M02-010/M02-011 profiles. It may compose with the
returning M04 profiles, which remain ordered before M02-012. Build, verifier,
and QEMU tooling propagate the typed switch independently, require `-BuildEfi`,
reject `-SkipImageBuild`, reject a typed ready marker without the profile, and
preserve the all-false artifact path.

After console/serial resolution and returning M04 checks, but before ordinary
memory-map capture, the adapter emits `TEST_START m02.012` and requires live
`AllocatePages`/`FreePages`. Its successful path makes exactly two allocation
calls: two contiguous `EfiLoaderCode` pages and one disjoint `EfiLoaderData`
page. Code page zero contains the complete M02-010 normalized slot page. Code
page one contains the six bytes `48 83 C4 18 48 CF` at offset zero and the exact
52-byte M02-012 transition at offset `0x40`. The data page contains the complete
M02-011 IDT. The 80-byte transition context remains in 16-byte-aligned
caller-owned stack storage for the synchronous call.

Returned ranges must be nonzero, aligned, complete half-open, nonwrapping,
canonical, and disjoint before the first dereference. The adapter materializes
and production-readbacks M02-010 and M02-011, prepares/emits/readbacks M02-012,
requires transition FNV-1a64 `AFF59D6480DE7F3C` and zero relocations, then calls
`X64InstructionPublication.Serialize()` before invoking only
`delegate* unmanaged<IDTRContinuedExecutionContext*, ulong>`. No C# or firmware
callback occurs while the requested IDTR may be active.

Immediately on return, before any callback, the raw safety gate requires the
loaded descriptor to equal `{ limit=4095, base=ownedIdt }`, previous/restored
descriptor bytes to match exactly, the previous descriptor to differ from the
owned IDT, and the entry/return IF bits to match. An all-zero/no-transition,
loaded mismatch, restore mismatch, previous-equals-owned, or IF mismatch takes
a callback-free spin. Once raw restoration is independently safe, the
production auditor requires the exact cookie, transition, loaded/restored
descriptors, IF equality, and all invariant context bytes. Safe audit failure
releases data then code and emits failure. Success also releases data then code
and withholds ready/pass unless both frees succeed. Pre-invocation cleanup uses
overflow-safe overlap checks and suppresses the code free for any equal or
partial hostile overlap, preventing a double free.

Success emits exactly:

```text
TEST_START m02.012
CLROS_M02_012_READY tableBytes=4096 idtrBytes=10 idtrLimit=4095 policyFnv=8514F42EBDFC5D55 firmwareGatesCopied=0 firmwareIdtrCaptured=true ownedIdtrLoaded=true ownedIdtrReadBack=true firmwareIdtrRestored=true restoreReadBack=true ownedIdtrActive=false continuedExecution=true cliWindow=true ifRestored=true instructionPublished=true allocationsReleased=true wxEnforced=false interruptTriggered=false faultTriggered=false markerTransport=uefiSerial postExitBootServices=false
TEST_PASS m02.012
```

Returning failures emit exact records
`M02_IDTR_CONTINUED_EXECUTION_FAILED` and
`TEST_FAIL m02.012 idtr_load_readback_restore_return_or_cleanup`. The raw
unverified-restore path intentionally emits neither because firmware callbacks
are unsafe there.

Default and enabled NativeAOT builds plus their same-build verifiers passed:

```powershell
dotnet run --project tools/OS.Tooling -- efi build `
  -EfiOutputPath .\artifacts\efi\BOOTX64-M02-012-DEFAULT.EFI `
  -LogPath .\artifacts\logs\m02-012-default-efi-build.log
dotnet run --project tools/OS.Tooling -- efi build `
  -EnableM02IdtrContinuedExecutionProbe `
  -EfiOutputPath .\artifacts\efi\BOOTX64-M02-012.EFI `
  -LogPath .\artifacts\logs\m02-012-efi-build.log
dotnet run --project tools/OS.Tooling -- efi verify `
  -EfiArtifact .\artifacts\efi\BOOTX64-M02-012.EFI `
  -EnableM02IdtrContinuedExecutionProbe
```

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| Default EFI | 55,808 | `A70718FFA723808966005005A366340A7A7B7F02FF43B6952B7D1B51ACA94B5A` |
| Enabled EFI | 87,040 | `D7428017EB461F8678C5C6793DA9C8F6A9E6CFD2456DDACB23D1161D1759A69C` |
| Enabled link map | 53,077 | `EFCC25EF19688C2F497763D902F6279FF1B665094A044A02B9E6FD3AB2E6ED00` |
| Enabled link response | 500 | `759617E9619E340C4016B730A32D98D5F4CABED9F61BBE0938B1169F2170BDBC` |
| Enabled link evidence | 399,378 | `AAABD5CACF4123F10B273CB80899371C429EDC7216A81FEBAEDA942DF40513DD` |

The focused integration test proves default exclusion, enabled linkage,
same-build provenance, hostile fake-firmware cleanup and fail-closed paths,
profile collision/argument handling, returning-M04 order, and exact transcript
acceptance/rejection without executing privileged bytes on the host. The dry
run requires and orders all 17 acceptance markers, requires the three M02-012
records plus all three ordinary terminal records to be unique, and retains five
forbidden markers: `OS_SMOKE_FAIL`, `TEST_FAIL`, the
M02-012 failure sentinel, the exact host-only M02-012 marker, and the exact
standalone M02-011 ready marker.

The accepted 2026-08-26 real QEMU 11.0.50/OVMF run used the enabled artifact
above. Its 67,108,864-byte raw image has SHA-256
`1757D1D647103212CAC8CAD7082D64BBC30C6632DE559EA39683D90521A236FC`;
its 8,462-byte serial log has SHA-256
`4E97D1D61EAF5FCB5371F05889734D8A8103C1C3CD0126B5E0A6A5519A8947C6`;
and its 5,844-byte passing summary has SHA-256
`6302E3FC51D4869F017E54836BE41F8C99A2F2CAAE3F000E61854D7947C69669`.
The marker state machine observed all 17 ordered records, all six unique
records exactly once, no forbidden record, and ordinary continuation through
`OS_SMOKE_PASS`. This bounded pre-`ExitBootServices` proof establishes only
temporary load/readback/restoration, IF restoration, return-cookie continuity,
cleanup, and ordinary-smoke continuation on that recorded virtual platform. It
does not prove interrupt/fault delivery, executable common dispatch, active or
persistent GDT/TSS/IST/IDTR state, W^X/NX (`wxEnforced=false`), NMI/SMI/
machine-check safety, real `ExitBootServices`, hosted .NET/Kestrel, CloudReady
status, or production readiness. Gate 2 remains the only passing numbered
gate; Gate 3 and Gate 4 remain `Fail`.

### M02-013 terminal UD2 vector-state EFI integration

The default-false `EnableM02Ud2VectorStateProbe` profile defines exact symbol
`M02_UD2_VECTOR_STATE_PROBE`. The typed switch rejects any explicitly false
prerequisite, otherwise implies the reviewed M01 owned-stack/allocator/paging
and M02 safe-stub/checked-port/owned-COM1 chain. It is mutually exclusive with
the returning M02-007/M02-008 descriptor profiles, M02-009 through M02-012
standalone profiles, and M04-003 span/memory conformance. It requires
`-BuildEfi`, rejects `-SkipImageBuild`, and rejects a typed M02-013 marker
without the profile. The all-false build remains unchanged.

The explicit enabled closure contains 67 compile items with compile-list
SHA-256
`F118A012101E87ABF5D1AC1299BE82705B47942EAE06ABB669418977412D124C`
and 80 source-authority inputs with closure SHA-256
`559999AC08D30B1FF5629A8240D9E822774ADC215D5E43B80910297B0C1E5DE2`.
The raw `src/OS/OS.csproj` SHA-256 is
`FF1EF01D1017123ECE5DC5EF81965C488382240937F088F1F35B28E57967FEEA`,
and its canonical evaluated XML-shape SHA-256 is
`B317CC30C4DD27D61B6349686BE68C3FD6F6702626DD5637D0C6A43A9C2BEA2D`.
The integrated `EfiEntry.cs` SHA-256 is
`5BC413EE69931A454EF5F4B084106D8E3AB7DF68C18351F785D2535B992FD7EB`.
The current production transition source has SHA-256
`F73C8F78FD1D5A1E0FD54C3FBE80716B40C61F1C05BC9F51CED8A505D61C407E`.
Its exact 4,096-byte stream has FNV-1a64 `E5070E16F23533A3` and SHA-256
`DB402C072CBD5E9C82F36D1802AAB526C347077BDD147031D83E83B563E195B7`.

After the ordinary boot path has emitted `OS_BOOT_OK`,
`TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`, the terminal adapter emits
`TEST_START m02.013` and requires live `AllocatePages`. Preparation makes
exactly two allocations:

- two contiguous `EfiLoaderCode` pages for the normalized vector-slot page and
  common-handler/transition page; and
- five contiguous, disjoint `EfiLoaderData` pages for the owned IDT,
  transition context plus GDT/TSS, and three zero-initialized IST pages.

Every returned half-open range must be nonzero, page-aligned, nonwrapping,
canonical, complete, and disjoint before dereference. Preparation zeroes both
ranges, materializes and production-readbacks the M02-010 page, M02-011 IDT,
frozen terminal stream, context, owned GDT/TSS, and untouched IST pages, then
publishes the exact instruction range. Any pre-entry failure releases data
before code; if hostile ranges are not provably disjoint, code release is
suppressed to prevent a double free. No cleanup callback is attempted after
descriptor activation.

The transition entry is at code offset `0x1800`, and its deliberate `ud2` is at
offset `0x19CE`. It loads and verifies the fresh GDT/TSS/IDT state, seeds live
pre-fault `RFLAGS=0x46`, records the expected state, loads 15 distinct GPR
cookies, and executes the two-byte invalid opcode. Vector slot 6 normalizes the
no-error-code frame into the common handler. The handler captures vector,
error-code metadata, RIP/RSP/RBP/RFLAGS, CR2/CR3, and all GPRs; compares all 23
fields; and emits either the exact success or failure transcript through an
allocation-free owned-COM1 writer before a terminal `cli`/`hlt` loop. The exact
saved fault-frame RFLAGS expectation is `0x10046`, not the live pre-fault
`0x46`: Intel fault-class exception delivery sets RF, bit 16, in the saved
image. No managed or firmware callback can occur after entry.

The exact success tail is:

```text
OS_BOOT_OK
TEST_PASS boot.sequence
OS_SMOKE_PASS
TEST_START m02.013
CLROS_M02_013_READY vector=6 errorCodePresent=false errorCode=0 ud2RipMatched=true registerFields=23 registerStateCaptured=true registerStateMatched=true ownedGdtLoaded=true ownedTssLoaded=true ownedIdtrLoaded=true ownedCom1=true faultTriggered=true terminalHalt=true markerTransport=ownedCom1 postExitBootServices=false
TEST_PASS m02.013
```

The generated failure path emits `M02_UD2_VECTOR_STATE_VERIFY_FAILED` followed
by `TEST_FAIL m02.013 vector_or_register_mismatch`, then halts. The runner
requires, orders, and uniquifies all six success-tail records; forbids eight
failure, host-only, or inherited standalone-profile markers; and separately
rejects any marker-like bytes after `TEST_PASS m02.013`.

The accepted build/QEMU command was:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 45 `
  -BuildEfi `
  -EnableM02Ud2VectorStateProbe `
  -EfiArtifact .\artifacts\efi\OS-M02-013-final-head.efi `
  -Image .\artifacts\images\m02-013-final-head.raw `
  -SerialLog .\artifacts\logs\m02-013-final-head-serial.log `
  -SummaryLog .\artifacts\logs\m02-013-final-head-summary.log `
  -RequireMarker "CLROS_M02_013_READY vector=6 errorCodePresent=false errorCode=0 ud2RipMatched=true registerFields=23 registerStateCaptured=true registerStateMatched=true ownedGdtLoaded=true ownedTssLoaded=true ownedIdtrLoaded=true ownedCom1=true faultTriggered=true terminalHalt=true markerTransport=ownedCom1 postExitBootServices=false"
```

The 2026-08-26 accepted tuple used QEMU 11.0.50 and the 3,653,632-byte OVMF
code image with SHA-256
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.
The artifact set is:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| Enabled EFI | 543,744 | `E532250338F50AE5EE191C6A2661612711A1DDA541662C72F6A7ADAF01C264DF` |
| Raw image | 67,108,864 | `E6D063ADEB790A2DE0B104A39827D2EDD75D16DE2CEAAF079A1353CDA9920C4D` |
| Serial log | 3,837 | `39DF159E4990CEE91574B6240ED77375B10DAC8B43338BA232FC965DF11D2E3E` |
| Passing summary | 17,000 | `75ED1E58513754E86579C6FD0B52F8BEC794EED9A7C205068BC9A5ADF9B7D99F` |
| Link map | 251,264 | `85A0F8D3BB996B1269F22885C958A3983DA510EA5B2C610318FC4D6546BDAFA7` |
| Link response | 500 | `70B1580FEC6721F70F1F9AAA71332055F1FD4BA6586B914156F8009F51A66B4D` |
| Link evidence | 400,930 | `AE8C700A34B796EE893FB91C1B82F83141C0FBC93ABF5AF288DFB4A10BF10AFB` |
| Publication object | 336 | `DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B` |
| QEMU runner stdout | 30,235 | `3B06EB7C8967374491D2F4FD099028CAD30AC55B5D426171BC43385CD61B4162` |
| QEMU runner stderr | 0 | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |

The same-build verifier identified PE32+ AMD64, EFI subsystem 10, entry RVA
`0x55980`, no imports, and 991 exception records. The marker state machine
reported `status=Passed`, `bytes=3837`, `lines=79`, `orderedSeen=59`,
`outOfOrder=0`, `uniqueSeen=59`, and `duplicateUnique=0`; QEMU was runner-
terminated only after the complete terminal transcript. The additional audit
reported `postSmokeSequence=true` and `postPassMarkerNoise=false`.

The complete same-head host acceptance exited zero with 3,507 `TEST_START`,
3,508 `TEST_PASS`, zero `TEST_FAIL`, and the terminal aggregate
`TEST_PASS host.tests`. Its 454,016-byte stdout has SHA-256
`DC9F1B87D2D4E528B21E4CD20547009AC2090AEB9BF9F5C9B87F2E171BE3D423`;
its zero-byte stderr has SHA-256
`E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

An initial real-QEMU run reached vector 6 with all register cookies and fresh
descriptor addresses correct but emitted the exact failure transcript. A
bounded QEMU instruction/exception trace isolated the comparison to saved
RFLAGS: live `0x46` became architectural fault-frame `0x10046`. The corrected
stream now asserts that exact RF-bearing value rather than masking high bits.

This is bounded processor evidence for one terminal, pre-`ExitBootServices`
profile. `EfiLoaderCode` allocation does not prove live W^X/NX enforcement;
the profile neither restores descriptor state nor returns, frees resources,
survives a second fault, reports after firmware exit, or supplies a reusable
kernel interrupt path. It proves no hosted .NET/Kestrel, cloud-provider,
CloudReady, security-hardening, or production behavior. Gate 2 remains the only
passing numbered gate; Gate 3 and Gate 4 remain `Fail`.

### M04-002 fixed endian-catalog EFI closure

`EnableM04IndependentEndianConformanceProbe` is a default-false, independent
profile. At its acceptance head it added exactly one explicit compile item:
`std/no-runtime/shared/IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs`.
Its current source location is
`std/no-runtime/shared/Binary/IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMOSA.cs`.
That historical accepted project closure had 53 compile items and the canonical
build/verifier authority had 66 inputs. The then-current development tree had 62/75
after the later M02-005 writer, M02-006 reporter, M02-007 GDT encoder, and
M02-008 guarded-IST arena contract plus the default-off M02-009 terminal stub,
M02-010 normalized interrupt-entry page, M02-011 owned-IDT materializer,
M02-012 IDTR transition source, and M04-003 span/memory catalog were added; this
does not alter or reinterpret the M04-002 artifact evidence below. The commands reject
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
dotnet run --project tools/OS.Tooling -- efi build `
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

### M04-003 span/memory conformance EFI integration

`EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe` is a default-false,
returning opt-in profile. It requires
`EnableM04IndependentEndianConformanceProbe=true` and is rejected when the
terminal `EnableM02OwnedGdtTssTerminalProbe` profile is enabled. The explicit
project links exactly one frozen source immediately after the M04-002 endian
catalog and before `M01LoadedImageStackIntegration.cs`:
`std/no-runtime/shared/Memory/SpanOverlapMemoryCastUnsafeWidthConformanceVectors.cs`.
At the accepted M04-003 source head the build and artifact-verifier closure was
59 compile items and 72 source-authority inputs. The then-current development closure
was 62/75 after the later M02-010, M02-011, and M02-012 sources; the later historical
68/81 closure is described in the M04-004 section below. Both commands
carry the exact property and
`M04_SPAN_OVERLAP_MEMORY_CAST_UNSAFE_WIDTH_CONFORMANCE_PROBE` symbol without
silently enabling the M04-002 dependency.

When enabled, the EFI entry path runs after accepted M04-002 validation and
before memory-map capture. It uses caller-owned stack storage for exactly 16
catalog vectors, 16 scratch bytes, and 16 execution records. It calls the
no-stdlib pointer `CopyBuiltInCatalog` operation and exactly one
`ExecuteCatalog` operation with fingerprint `F03FF59D36F39ECD`. The guest path
audits every vector and every output record: keys, operations, status,
result-element and output-byte counts, exact outputs, rejected-slice
preservation, overlap-direction scratch results, and raw unsigned observations
`FF`, `FF80`, `89ABCDEF`, and `1122334455667788`. The complete key-16 raw value
is checked before its four-byte output truncation.

Any mismatch emits only `M04_SPAN_MEMORY_CONFORMANCE_FAILED`, suppresses the
success record, and returns before `UEFI_MEMORY_MAP_READY`. The accepted guest
run emitted exactly:

```text
CLROS_M04_003_READY vectors=16 spanVectors=4 overlapVectors=5 memoryCastVectors=3 unsafeWidthVectors=4 fingerprint=F03FF59D36F39ECD pointerWidth=8 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

The accepted build and artifact-verifier evidence was generated from source
head `dedb55c3ddb155336ef4c7f387cf14b533c37f09`, tree
`3ce99d70fe4607e5ed5858e86863ed49d0e47381`. The schema-three same-build link
evidence records `Release`, `win-x64`, exactly 59 compile items, compile-key
SHA-256 `9E424BEFDB74433B2B179AEFC15996E693D835099C196B2E8DC3716C5D7C4F6D`,
exactly 72 source-authority inputs, and source-closure SHA-256
`6CD53117395FFE7668E26A8FD9AB30F6F9154AA8A7911A5B99B92E89F493599A`.
Both M04 properties evaluate to `true`; the terminal M02-009 property remains
`false`.

The artifact verifier accepted a PE32+ x64 EFI application with subsystem 10,
the same-build `EfiMain` and serializer bindings, and no ordinary, bound, IAT,
or delay imports. The resulting 82,944-byte EFI artifact has SHA-256
`0F8A7E3FB5D626E7017D2839DA0E3C966D86A53E769B9D39A3D0275F974A550B`.
The link map and response hashes are
`7656D08CBCC932CEFC35303B19DEC981D437DD6FCC70FECA92084F8C2DAED2B3`
and `C2A083629F7DC030ECBCA2347A6B4E8AD8C505CE107B233F078814B793C50E24`;
the 398,199-byte link-evidence JSON has SHA-256
`7136A51B7199AC0B0EBB76E0F6908FB09869EBD6E1584EDDA1E8C521A770FA2B`.
The archived build log has SHA-256
`31D76FACE1A612B7CEC24519C55BC9A89937023995CD5A9B0A9ADAAA4DDFAC70`.
`docs/qemu.md` and `done/step919.md` preserve the command and complete guest
tuple.

This evidence proves only one fixed-catalog call in the pre-`ExitBootServices`
EFI application. Gate 4 remains `Fail`. It does not prove general
`System.Span<T>` or `MemoryMarshal`, unsafe memory safety, allocation, GC,
exceptions, metadata or dispatch, real `ExitBootServices`, hosted .NET 10,
Minimal API/Kestrel, cloud readiness, or production readiness.

### M04-004 arithmetic conformance EFI integration

`EnableM04ArithmeticConformanceProbe` is a default-false, returning profile
implemented by supplemental #507. It requires both M04-003 and M04-002 switches
explicitly and rejects the terminal M02-009/M02-013 profiles. The project links
the unchanged arithmetic catalog immediately after M04-003: the explicit
closure at step929 was 68 compile items and 81 source-authority inputs. All build,
post-restore evaluation, verifier, and QEMU layers carry the same property.

The enabled adapter calls `RunCatalog()` once after M04-003 and before later
M02 probes and memory-map capture. Passed status, completed count 57, failed
index -1, and literal fingerprint `45FDFA24DD72EA28` must all match. Failure
emits `M04_ARITHMETIC_CONFORMANCE_FAILED` and returns before downstream boot
markers. The successful adapter emits:

```text
CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

The only compiler-shim addition is `MethodImplOptions.NoInlining = 0x0008`,
following the concrete CS0117 publish failure. Metadata tests verify that bit
on the actual evaluator and zero framework assembly references. No new
runtime service or library is introduced. Host adapter tests inject malformed
results and require the exact failure line with no success output.

Step928 records the implementation-worktree publishes and review. Step929
adds the complete host gate and fresh default/enabled QEMU/OVMF acceptance at
source `07da84eb0305b680d43f72f4fd690d5ed6fe3e18`. Both rebuilt FAT images
contain the independently verified EFI bytes, and both schema-three manifests
match the 68/81 source closure. The enabled guest emits the exact 57-vector
record once after M04-003 and before memory-map capture; default emits no M04
records. See `docs/qemu.md` for the strict transcript and firmware-return
boundary. Gate 4 remains **Fail**; this is one fixed-catalog guest execution.

### M04-005 layout value checks in EFI

Supplemental #512 adds the default-false `EnableM04LayoutContractProbe` /
`M04_LAYOUT_CONTRACT_PROBE` profile. Select all four M04 flags explicitly;
missing or false predecessors and terminal owned-GDT/TSS or UD2 combinations
are rejected. The explicit closure at step933 was 69 Compile items and 82 raw
source-authority inputs. Default and older-profile marker lists are preserved.

Immediately after arithmetic conformance and before memory-map capture, the
returning adapter calls the frozen NoInlining scalar evaluator 19 times:
13 independent positive fixtures and six rejection cases. Every result field
must match, including zero payload on rejection. A mismatch emits
`M04_LAYOUT_CONTRACT_FAILED` and suppresses later map/boot/smoke success.

```text
CLROS_M04_005_READY profile=ilc10.0.8-win-x64-le checks=19 shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

The compiler/runtime authority remains 10.0.8 at dotnet/dotnet commit
`94ea82652cdd4e0f8046b5bd5becbd11461482ca`. The source scanner admits only
the exact ordinal layout-leaf path and reviewed whole-source identity, with
CRLF-to-LF normalization for that text check. All other source scanning and
raw source/artifact hash authority remain unchanged. No runtime shim changed.

Step932 records reviewed integration, controlled adapter/transcript checks and
the earlier manifest-gate failure. Step933 records the accepted complete host
gate and fresh default/layout NativeAOT publish/QEMU matrix at source
`5f6108ca363f03c7ebad8dafca7ae78e1d3a0db2`. Both source-bound EFI payloads
were independently recovered from their images. The enabled guest emitted the
exact 19-check record once in order; default emitted no M04 records. See
`docs/qemu.md` for hashes and transcript boundaries, including the separately
tracked #514 FAT geometry defect. These scalar checks do not allocate objects
or prove compiler-emitted physical layouts.

### M04-006 retained heap page in EFI

Supplemental #516 adds the disabled-default
`EnableM04ManagedHeapPageRangeProbe` / `M04_MANAGED_HEAP_PAGE_RANGE_PROBE`
profile. It requires both loaded-image-stack/live-allocator flags and all four
earlier M04 flags explicitly. Owned paging, all twelve M02 profiles and the
three risky optional firmware/ACPI profiles are rejected. The smoke wrapper
requires `BuildEfi` and rejects `SkipImageBuild` and `M01RawObservationLog`.

That checkpoint's explicit closure contained 70 Compile items and 83 raw source-authority
inputs. Its schema-three post-restore evidence authenticated all 23 then-known probe
properties: three M01, twelve M02, five M04 and three existing risky optional
properties. The project declares 20 false defaults. The default/layout/heap
acceptance matrix evaluates respectively zero/four/seven true properties;
the three risky properties are explicitly false in that matrix. Other
permitted profiles retain their existing optional-property behavior.

The original M01 allocator/node/ledger and two-page LoaderData parent provide
the backing; the historical freed page address is not reused as a lease.
`ReserveAndInitializeOnce` executes after all M01 stage-two completion
consumers, and `ValidateRetained` executes on freshly acquired original slots
immediately before the boot plan. Both require actual full-page zero readback,
matching original identities and consistent current accounting. Failure keeps
ownership and follows the terminal spin; the enabled path never returns to
firmware. See `docs/nativeaot-nostdlib-limits.md` for the retained-page boundary
and `docs/qemu.md` for the exact transcript and lifecycle requirements.

The source firewall admits only the ordinal exact
`..\OS.Kernel\Runtime\ManagedHeapPageRangeLiveAllocator.cs` path with whole
LF-normalized SHA-256
`43cdf5af449f7963f07b200ec494da3d8538f1ceb2f01fb3dbe9c29edebcb070`.
All other sources retain the generic scanner. This text-check normalization
does not alter raw artifact/source-authority hashes. No runtime shim changed.
Step937 records independent source review and focused adapter, transcript,
artifact-authority, legacy-fixture and preflight evidence. Step939 records the
accepted complete host gate, fresh default/layout/heap NativeAOT/QEMU matrix
and separate corrected-image accounting at source `4b18b4f`.

The first complete host run at `b5784a2` failed in M02-012's actual-artifact
provenance check: default and enabled builds succeeded, but a stale assertion
required 82 authority inputs. Independent comparison verified all 83 ordered
paths and raw hashes. The matching M02-011 assertion had the same stale count.
Step938 corrects both counts without relaxing the other provenance predicates;
the failed gate remains preserved and cannot authorize guest acceptance.
The subsequent complete gate passed with 3,541 starts, 3,542 passes and zero
failures. Each fresh guest lane binds the same 70 Compile items, 83 raw inputs
and 23 evaluated properties, with zero assembly references in its actual
no-stdlib managed assembly. Two failed ignored-wrapper attempts are preserved:
PowerShell JSON-array nesting stopped the first before any lane, and a
same-AppDomain reflection-only assembly collision stopped the second after
default/layout guest execution. Reviewed helper corrections preceded the
successful three-lane attempt; no product source changed for either correction.

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

The build snapshots the exact source inventory returned by
`EfiEvidence.SourceClosurePaths`, including `global.json`, explicit compile
inputs, generator inputs and the compiled C# tooling sources. It opens read/share-read handles that deny
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
Before restore, it authenticates every archive in `EfiContracts.RestoreArchives`
by reviewed SHA-256, copies them into a freshly created local source beside an
allowlisted `NuGet.Config`, and read-locks the original archives and local files.
The configuration clears
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

After restore, the build enumerates the exact trees in
`EfiContracts.ConsumedPackageTrees`, including the separately selected NativeAOT
runtime root. It validates reviewed package content identities, rejects any extra
package version or reparse point, hashes every stable file with ordinal relative
paths, and separately validates and locks package metadata. Those file handles
remain held through verification. `EfiContracts.PackageBuildClosure` binds the
active targets, task assemblies and task dependency manifests. The shared
`NativePackageVersion` supplies expected package versions and paths; changing it
does not approve different archive or tree hashes. Publish receives explicit
canonical ILCompiler, ILLink, task, host compiler and runtime-pack paths.
`OS.csproj` makes the SingleEntry target consume those
same CLROS roots and fails the build if that target rewrites either root.
The nested host generator removes inherited AOT/package globals and explicitly
replaces the restore/project-extension globals it needs with canonical child
values before its package-free build.

A locked post-restore evaluation requires the exact `EfiContracts.CompileItems` inventory, zero
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

`dotnet run --project tools/OS.Tooling -- efi verify` treats that evidence as untrusted input. It
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
requires the complete source inventory to have mutually distinct single-link identities, and still
allows the artifact/map/response/evidence bundle itself to be copied to a
different directory for independent inspection. Its independent post-restore
MSBuild evaluation also runs in a verifier-local kill-on-close Job Object: the
child is created suspended, assigned before resume, and given bounded strict
UTF-8 output plus a fixed timeout. Timeout, output overflow, capture failure,
nonzero exit, or inability to confirm that the complete descendant tree
terminated fails with a distinct stable reason. Node reuse, build-server reuse,
shared compilation, telemetry, and decorative CLI output remain disabled.

The parent starts the compiled verifier suspended, assigns it to a kill-on-close
Windows Job Object, starts bounded asynchronous stdout/stderr capture, and only
then resumes it. Verifier code cannot run or create a child before the job owns
the process. `EfiBuildPolicy` owns the default 60-second verifier timeout, allowed
bounds and output capacity. There is no retry. Both output drains are bounded;
verified whole-job termination is checked separately from a normal nonzero exit.
Controlled process-result tests require the build to reject a false
`terminationConfirmed` value for every relevant result status.

The hostile process fixtures use the five-second minimum verifier deadline.
Compiled child controls publish the parent and descendant process identities,
hold a sentinel file, and deliberately wait. The fixture requires confirmed
termination of both processes and release of the sentinel, including when the
parent exits while its descendant remains. These are controlled host checks;
they preserve Job Object ownership and the output-drain and termination checks.

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
The complete source closure receives the same single-link and
distinct-identity requirement. Package and tool inputs are reparse-checked and
mutation-locked; they are not claimed to be globally unique file identities.

The C# verifier walks every path component using `Path.Combine` and
`File.GetAttributes`, rejects reparse points, and retains native file identity
handles through verification. It does not cache ancestry or skip hashing.
The historical Windows PowerShell 5.1 comparison in
[step1068](../done/step1068.md) measured about 88.6% less helper wall time over
1,030 paths in the old implementation. That measurement is retained as historical
component-walk evidence; it is not a measurement of the current C# verifier.

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

### M02-014 terminal page-fault EFI integration

`EnableM02PageFaultCr2ErrorCodeProbe` defaults to false at project, build,
artifact-verifier and QEMU boundaries. It implies loaded-image/stack, live
allocator, owned paging, safe stub, checked port binding, owned COM1 backend,
owned COM1 readiness and panic-safe COM1 when omitted; caller-explicit false
values reject. It rejects the GDT encoding, guarded IST allocation, terminal
GDT/TSS, normalized frame, IDT population, reversible IDTR and UD2 profiles,
plus M04 span, arithmetic, layout and retained-heap profiles. Independent endian
remains optional and does not imply allocation/runtime completion.

The closure adds only the narrow false null-pointer authority and the existing
null-read intent, CR2/error, page-image and terminal-transition leaves: 75
explicit Compile items, 88 raw authority inputs and 24 evaluated switches.
The #510 scanner admission is exact path plus full normalized source hash;
the rest of the scanner and raw-byte authority checks remain strict. The
MinimalRuntime shim and the frozen leaf implementations are unchanged.

The adapter uses three LoaderCode pages, ten LoaderData pages and 43 table
pages. Its six mappings plus table arena contain 52 present leaves, four guard
holes and absent null page. Readback and an actual serializer call precede the
closed stream entry. Step941 records source and focused compiler evidence.
The separate accepted step944 guest matrix builds fresh default/enabled EFI
payloads of 55,808/558,592 bytes and inspects each actual `OS.dll` in a fresh
metadata-only process: assembly `OS`, zero AssemblyRefs. All 24 properties and
the 75/88 source closure remain bound to tested source `7c71e72`.

See [step944](../done/step944.md) for artifact identities and the actual fault
capture. The observed firmware/saved CS is `0x38`; current-handler CS and LAR
rights are source/path predicates, not separately printed descriptor dumps.

## Unsupported Behavior

The project is not yet a functional kernel. The default path retains firmware
services. The accepted default-off M03-007 profile proves only its bounded real
exit, retained mappings and terminal owned diagnostics ([step1051](../done/step1051.md)).
The accepted revocation profile covers exactly eight retained capabilities
([step1053](../done/step1053.md)); owned post-exit diagnostics have their separate
bounded acceptance ([step1063](../done/step1063.md)). Firmware-page reclamation,
general post-firmware physical/MMIO interfaces, continuing drivers/workloads
and broader panic recovery remain incomplete. The host-tested
`UefiExitBootServicesLiveWiringPlan` and `PlatformFirmwareExitBootServicesResult`
contracts alone do not establish the accepted live transition.

## Tests

### Prepare acceptance from current inputs

Finish production changes and text normalization before freezing the acceptance
commit. Review the entire changed-file list against existing assertions, including
backlog ownership notes and dependency contracts, canonical docs, source/Compile
closures, feature defaults, generator inputs, native object sizes and transcript
grammar. A hash/count-only search does not cover semantic catalog requirements.
Keep raw source hashes separate from documented canonical-text hashes.

Run the affected registered checks before the full harness. Use
`dotnet run --project tests/OS.HostTests -- --list` to inspect discovery and
`--filter <fixture-or-case>` to select existing checks. Native fixtures require
`--lane native` or `--lane full`; `--case <marker substring>` narrows grouped
fixtures. Record the selection and exclusions. Use these compiled fixture paths
directly; do not copy test sources or generate subset projects. Focused success
never replaces the unfiltered `--lane full` gate.

Build and run the complete harness against frozen inputs. Keep edits,
competing builds and native publications out of its checkout until its terminal
result and unchanged source/binary inventories are reviewed. After a failure,
preserve the failed attempt, identify the exact predicate, check related omitted
assertions and demonstrate the correction before starting another full run.

Use fresh output paths for each final native publication and guest. Retain native
objects/maps, managed inputs, the complete property vector, image payload, raw
serial stream and process lifecycle. Bind debugger controls to that exact image
and its actual symbols. Prior-image runs remain historical evidence. Review
capture helpers, tool-format assumptions and cleanup receipts before execution.

Expected commands:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project tools/OS.Tooling -- efi build -PreflightOnly
dotnet run --project tools/OS.Tooling -- efi build -EnableM01LoadedImageStackProbe
dotnet run --project tools/OS.Tooling -- efi build -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM01OwnedPagingProbe
dotnet run --project tools/OS.Tooling -- efi build -EnableM02NormalizedInterruptFramePageProbe -EfiOutputPath .\artifacts\efi\BOOTX64-M02-010.EFI -LogPath .\artifacts\logs\m02-010-efi-build.log
dotnet run --project tools/OS.Tooling -- efi build -EnableM02OwnedIdtGatePopulationProbe -EfiOutputPath .\artifacts\efi\BOOTX64-M02-011.EFI -LogPath .\artifacts\logs\m02-011-efi-build.log
dotnet run --project tools/OS.Tooling -- efi build -EnableM02IdtrContinuedExecutionProbe -EfiOutputPath .\artifacts\efi\BOOTX64-M02-012.EFI -LogPath .\artifacts\logs\m02-012-efi-build.log
dotnet run --project tools/OS.Tooling -- efi verify -EfiArtifact .\artifacts\efi\BOOTX64-M02-012.EFI -EnableM02IdtrContinuedExecutionProbe
dotnet run --project tools/OS.Tooling -- efi build -EnableM02Ud2VectorStateProbe -EfiOutputPath .\artifacts\efi\BOOTX64-M02-013.EFI -LogPath .\artifacts\logs\m02-013-efi-build.log
dotnet run --project tools/OS.Tooling -- efi verify -EfiArtifact .\artifacts\efi\BOOTX64-M02-013.EFI -EnableM02Ud2VectorStateProbe
dotnet run --project tools/OS.Tooling -- efi build
dotnet run --project tools/OS.Tooling -- efi verify
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -BuildEfi -EnableM02NormalizedInterruptFramePageProbe
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -BuildEfi -EnableM02OwnedIdtGatePopulationProbe
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -BuildEfi -EnableM02IdtrContinuedExecutionProbe
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -BuildEfi -EnableM02Ud2VectorStateProbe
dotnet run --project tools/OS.Tooling -- qemu smoke -BootPolicy Diagnostic -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -RequireMarker OS_SMOKE_PASS,UEFI_MEMORY_MAP_OWNED,BOOT_HANDOFF_READY,UEFI_MEMORY_MAP_NORMALIZED,BOOT_MEMORY_MAP_ACCEPTED,BOOT_MEMORY_REGIONS_READY,BOOT_MEMORY_REGION_COUNT
dotnet run --project tools/OS.Tooling -- efi build -EnableUefiGoneSimulationProbe -EfiOutputPath artifacts/efi/BOOTX64-SIM.EFI -LogPath artifacts/logs/efi-shell-publish-sim.log
dotnet run --project tools/OS.Tooling -- qemu smoke -BootPolicy Diagnostic -EfiArtifact artifacts/efi/BOOTX64-SIM.EFI -Image artifacts/images/os-sim.raw -SerialLog artifacts/logs/qemu-smoke-sim.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset uefi-gone
dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi -EnableAcpiPhysicalDiscoveryProbe -MarkerPreset acpi -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
```

Historical evidence from 2026-05-21:

- `.\scripts\build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`.
- The script emitted `EFI_ARTIFACT artifacts/efi/BOOTX64.EFI`.
- `.\scripts\run-qemu-smoke.ps1 ...` emitted `TEST_PASS qemu.smoke`.
- The simulation publish emitted `EFI_PROBE UEFI_GONE_SIMULATION` and `TEST_PASS efi.shell.publish`.
- The simulation QEMU smoke emitted `TEST_PASS qemu.smoke` while requiring the `uefi-gone` marker preset.
- `artifacts/logs/qemu-smoke.log` contains `TEST_START efi.main`, `EFI_CONSOLE_READY`, `UEFI_MEMORY_MAP_READY`, `UEFI_MEMORY_MAP_DESCRIPTOR_COUNT <count>`, `TEST_START boot.sequence`, ordered `BOOT_PHASE` markers, `OS_BOOT_OK`, `TEST_PASS boot.sequence`, and `OS_SMOKE_PASS`.
- After the no-stdlib console adapter was added, `scripts/build-efi-shell.ps1` emitted `TEST_PASS efi.shell.publish`, and real QEMU smoke requiring `OS_SMOKE_PASS,EFI_CONSOLE_READY` emitted `TEST_PASS qemu.smoke`.
- `artifacts/logs/qemu-smoke-sim.log` additionally contains `UEFI_GONE_SIM_START`, `UEFI_GONE_SIM_PLATFORM_DENIED`, `POST_FIRMWARE_DIAGNOSTICS_READY`, and `UEFI_GONE_SIM_PASS`.

Historical evidence from 2026-05-22:

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

Historical evidence from 2026-07-25:

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

The memory-map source buffer is firmware-allocated by the source-linked capture helper, but the descriptor bytes are copied into a static OS-owned buffer before `UEFI_MEMORY_MAP_OWNED` is emitted, the owned handoff validates before `BOOT_HANDOFF_READY` is emitted, the compact live summary normalizes before `UEFI_MEMORY_MAP_NORMALIZED` is emitted, and compact boot-region records materialize before `BOOT_MEMORY_REGIONS_READY` is emitted. The separate accepted first-exit profile supplies the bounded fresh-map/key-retry transition and retained allocator ownership; this default-path capture alone does not establish those operations or general firmware-page reclamation.

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
6. Preserve M02-010's one-page LoaderData materialization, exact production
   readback, release, and distinct guest marker without treating the modeled
   common target as executed code or claiming an IDT, live frame, or fault.
7. Preserve M02-011's temporary three-page LoaderData population/readback,
   exactly-once release, and distinct guest marker without treating owned IDT
   bytes as an active IDT, executable entry path, interrupt, or fault.
8. Move UEFI structs behind platform interfaces.
   - Keep the low-level UEFI table pointers isolated to the EFI boundary.
   - Expose only the narrow operations required for console diagnostics, memory-map ownership, firmware shutdown state, and optional filesystem/protocol discovery.
9. Preserve the accepted real-exit and owned-COM1 profiles while extending
   continuing post-firmware services.
10. Preserve copied firmware data, the typed `ExitBootServices` adapter and
   bounded stale-key retry; admit each additional retained owner before use.
11. Route additional platform interfaces through the UEFI-gone simulation path
   as they are introduced.
12. Replace permissive shell-only runtime placeholders with audited runtime
   helpers as subsystems require them.

### M04-007 fixed-allocation source profile

Step946 adds default-false `EnableM04RhpNewFastProbe`,
`EnableM04RhpNewFastBeforeBindFailureProbe` and
`EnableM04RhpNewFastExhaustionFailureProbe`, with corresponding
`M04_RHP_NEW_FAST_PROBE`, `M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE` and
`M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE` symbols. Main requires both M01
loaded-image/live-allocator flags and all five prior M04 flags explicitly.
Negative selectors require main and exclude each other. Owned paging, every
M02 probe and the simulation/fresh-ExitBootServices/physical-ACPI flags conflict.

The step949 closure recorded 77 ordered Compile items, 90 raw source-authority inputs,
27 evaluated flags and 24 XML false defaults. The two appended sources are the
unchanged `GuestTestRhpNewFast.cs` leaf and new `RhpNewFastAllocation.cs` adapter;
all75 previous Compile indices remain stable. Scanner admission is limited to
the exact leaf path and LF-normalized whole-source SHA256
`22DF5D019EC4F09C5CB870B807ED91B9EA62C35E30CF59C031C3A15290CB9F45`.
Raw publish hashes and the generic source scanner retain their prior authority.

The minimal shim adds only the reviewed compiler declarations/token conversion
and sole real `RhpNewFast` export, which fails closed by default. Four actual
native artifact profiles pass; their linked caller/getter/constructor/reader
associations are preserved in step946. Step949 adds accepted complete host and
five fresh QEMU profiles on source `77860a689b8460114cf179e44dd442533a1ad638`.
Each archived managed DLL passes a fresh metadata-only inspection; same-image
native routes, relocated prefixes, raw serial, lifecycle and disk accounting
are independently accepted. See `docs/nativeaot-nostdlib-limits.md` for the finite
domain and `docs/qemu.md` for the positive command and negative-proof boundary.

Step947 scopes CS0649 suppression to the profile-written constructor counter
and nine observation slots accessed through a POD pointer stride. Ordered
fields, layout and executable logic are unchanged; unrelated warnings remain
enabled. The initial integrated build's ten warnings and interrupted host run
are preserved in that step. Step948 repairs the catalog's exact ownership note.
Step949 records the subsequent clean solution build with zero warnings/errors,
complete 3,573-start/3,574-pass host run and accepted five-profile guest evidence.
That milestone retained the 77-item/90-input closure and 27-property vector.
The later M02-015 integration below extends this shared source boundary.


### M02-015 terminal double-fault source profile

The separately reviewed #521 adapter in [step950](../done/step950.md) adds
`EnableM02DoubleFaultIstProbe` and `M02_DOUBLE_FAULT_IST_PROBE`. Its authority
defaults false. Enabling it implies the existing loaded-image, live-allocator,
owned-paging, safe-stub, checked-port-I/O, owned-COM1, COM1-ready and panic-safe
COM1 prerequisites. Explicit false prerequisites and other terminal, M04,
simulation, fresh-ExitBootServices and physical-ACPI probes are rejected.

The two exact Compile items are the narrow false authority followed by the
unchanged double-fault leaf, after `SeparateDisabledDefaultPageFaultTransition`
and before `InvalidOpcodeVectorAuthority`. All 77 previous items retain their
relative order. That checkpoint's explicit closure contained 79 Compile items, 92 raw
authority inputs, 28 evaluated profile flags and 25 XML false defaults. Default
has zero true flags; PF and DF each have their own nine-true vector. Ordinary
source admission accepts the leaf without a new scanner exception. The retained
allocation shim, helpers and ledger are unchanged.

The adapter validates C3/D10/T43 ownership and all caller scratch before writing,
checks observed paging mode/PAT and current code-segment rights, and prepares
the exact armed IDT/context/stream. A fresh six-range audit of the original
table backing precedes final readback, publication and entry. No C# ready marker
substitutes for entry. Only the PF gate Present bit is cleared, leaving vector 8
on IST1; the trigger is the frozen null read. Saved DF program state and CR2
remain diagnostic-only. Default/enabled NativeAOT artifacts are implementation
evidence. [step951](../done/step951.md) separately accepts the settled-source
complete host suite and fresh default/PF/DF images, each with the recorded
79-item/92-input source closure, actual 28-flag profile and zero-AssemblyRef
OS.dll. Same-image native/current-state and full capture/lifecycle review supply
the bounded delivery evidence; successful builds alone do not.

### M04-008 shared SZ-array source profile

Step952 adds false-default `EnableM04SzArrayAllocationProbe`,
`EnableM04SzArrayAllocationBeforeBindProbe` and
`EnableM04SzArrayAllocationExhaustionProbe`, with corresponding
`M04_SZ_ARRAY_ALLOCATION_PROBE`, `M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE`
and `M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE` symbols. Main requires both
M01 loaded-image/live-allocator flags and the six prior M04 flags explicitly,
including `EnableM04RhpNewFastProbe`. Each negative selector requires main;
the selectors exclude one another. Owned paging, all M02 probes, either fixed
allocation negative selector, UEFI-gone simulation, fresh ExitBootServices and
physical ACPI discovery conflict. Existing prerequisite/exclusion rules remain.

The single appended array wrapper gives 80 ordered Compile items and 93 raw
source inputs, with 28 XML false-default probe properties and three separately
checked risky inputs forming the 31-flag evaluated vector. The thin wrapper
passes the unchanged generic scanner; no wrapper exception is introduced.
The fixed allocator's existing exact-source pin is refreshed for the reviewed
shared private allocation path. Raw-source hashing remains authoritative.

The complete array integration initializer published default, positive,
before-bind and exhaustion NativeAOT artifacts with true-flag counts 0/9/10/10.
It checked actual zero-reference OS metadata, helper attributes/signatures,
genuine `newarr`/length/index IL and linked array/element routes. This remains
focused artifact evidence. [Step954](../done/step954.md) separately accepts the
clean same-head solution build, complete 3,611-start/3,612-pass host gate and
eight fresh QEMU profiles. Actual metadata/native routes, relocated prefixes,
complete transcripts, process lifecycle and pristine/booted disks bind the
80-Compile/93-input/31-flag artifacts to that source. Both allocation-negative
profiles use their separately reviewed expected-failure observer. No successful
publish is itself execution of a guest allocation; general runtime support
remains incomplete.

### M02-016 unexpected-vector profile

[Step956](../done/step956.md) records the default-off
`EnableM02UnexpectedVectorPanicProbe` /
`M02_UNEXPECTED_VECTOR_PANIC_PROBE` profile. Its separate partial authority is
false by default. Enable the existing loaded-image stack, live allocator, owned
paging, safe stub, checked port-I/O binding, COM1 port-I/O, owned COM1 readiness
and panic-safe COM1 profiles together with it. All23 other flags must be false.
Explicit false prerequisites and reciprocal profile conflicts fail closed in
the project, build, artifact verifier and QEMU wrapper.

The current checkpoint has82 ordered Compile items,95 raw source inputs and
32 evaluated flags (29 XML false defaults plus three separately checked risky
inputs). The new authority and D7 leaf pass ordinary source admission; no new
scanner exception is introduced. The existing allocator pin accounts for the
accepted shared String storage host change. Default/enabled artifact builds
passed with0/9 true flags and zero OS AssemblyRefs; managed IL checks bind the
original callback and publication order. Those builds are not guest evidence.

Native compiled adapter entry already uses SSE while on the inherited firmware
path, before its later Query/Mode check. Supporting that compiled entry ABI is
a prerequisite; the later check cannot establish safety for instructions
already executed or reject an adversarial unsupported-SSE entry before all SSE
use. Fresh mode admission still governs the subsequent owned transition and
callback. Full current-image call/access/stack, ABI, original port-target and
SIMD analysis was separately accepted for the actual step959 images. Its
113-method access bound is5,208 bytes within the8,176-byte callback budget.
Step959 binds the complete host pass and nine fresh guest profiles to this
same82-item/95-input/32-flag source, including default adapter absence and the
strict vector-32 expected-panic result. Those facts do not broaden the entry
ABI prerequisite or prove general post-firmware operation.

### M04-009 shared String source profile

[Step963](../done/step963.md) adds four false-default switches:
`EnableM04StringAllocationProbe`, `EnableM04StringAllocationBeforeBindProbe`,
`EnableM04StringAllocationExhaustionProbe` and
`EnableM04StringAllocationInvalidLengthProbe`. Their corresponding symbols are
`M04_STRING_ALLOCATION_PROBE`, `M04_STRING_ALLOCATION_BEFORE_BIND_PROBE`,
`M04_STRING_ALLOCATION_EXHAUSTION_PROBE` and
`M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE`. The positive profile enables both
M01 loaded-image/live-allocator switches and all eight M04 switches shown below.
Each negative requires main and excludes its siblings, the fixed/array
negative modes, M02/owned-paging modes and the three risky firmware/ACPI modes.

That source checkpoint recorded 84 ordered Compile items, 97 raw publication inputs and 36
evaluated properties (33 XML false defaults plus three risky inputs). The
String wrapper and authority enter the explicit closure. The generic scanner
is unchanged; exact source pins and finite selector rules are refreshed.
`EfiEntry.cs` and the original publication call counts remain unchanged.

Five focused NativeAOT publications pass for default, positive, before-bind,
exhaustion and invalid-length, with0/10/11/11/11 true properties. All have zero
OS AssemblyRefs. Metadata and linked-code checks bind `RhNewString`, constructor
remapping, String/Object prefixes and relocations, UTF-16 field offsets and the
full-width invalid-length call. These were the compiler/artifact results at
step963. [Step964](../done/step964.md) adds the accepted clean `be2762f` solution
build, complete 3,653/3,654/0 host suite and seven fresh QEMU/OVMF captures.
Default/fixed/mixed-array/String/before-bind/exhaustion/invalid-length artifacts
have zero OS AssemblyRefs and exact 36-property vectors with 0/8/9/10/11/11/11
true flags. All 84 Compile and 97 raw input identities are bound, with smoke
source provenance recorded separately. Fresh independent native, raw,
lifecycle and disk reviews accept the bounded String behavior and shared-core
regressions; general runtime gates remain incomplete.

The positive smoke command is:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 `
  -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe `
  -EnableM04IndependentEndianConformanceProbe `
  -EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe `
  -EnableM04ArithmeticConformanceProbe -EnableM04LayoutContractProbe `
  -EnableM04ManagedHeapPageRangeProbe -EnableM04RhpNewFastProbe `
  -EnableM04SzArrayAllocationProbe -EnableM04StringAllocationProbe
```

This command is not a step963 guest result; step964 records its later capture
and independent acceptance. Negative variants add exactly one
String negative switch to the same build prerequisites. Their intentional
`TEST_FAIL m04.009` endings remain ordinary runner failures; independently
reviewed capture and full negative-transcript/lifecycle checks are required
before accepting an expected rejection. See `docs/qemu.md` for that boundary.

### M03-001 original memory-map capture observation

[Step968](../done/step968.md) adds the false-default
`EnableM03MemoryMapCaptureProbe` / `M03_MEMORY_MAP_CAPTURE_PROBE` profile. It
requires all other 36 probe properties false; reciprocal project and script
checks preserve the existing profiles. That checkpoint's explicit closure contained 85 ordered
Compile items, 98 raw publication inputs and 37 requested/evaluated probe
properties, including 34 XML false defaults. The appended authority is
`MemoryMapCaptureProbeAuthority.cs`; the generic source firewall and QEMU
runner remain unchanged.

The profile observes the original `UefiMemoryMapCapture.Capture` and
`UefiMemoryMapCopyPlan` path through bounded forwarding callbacks. Its observer
POD is 488 bytes. The guarded destination spans 131,104 bytes, with the full
131,072-byte payload beginning at offset 16. It preserves separate query and
recapture size slots, original firmware callbacks, full-width statuses and
the source/owned-map/handoff association. Successful readback checks every
captured byte, all 32 guard bytes and the entire unused payload tail before
output. A local admission or firmware failure cannot continue to boot success.

At source checkpoint v8, the complete observer module passes six starts/seven passes/zero
failures, including actual default and enabled NativeAOT publications with
zero OS AssemblyRefs and exact 0/1 true-property vectors. The earlier recorded run of four complete M01 selector/publication modules
plus the two unchanged memory-map modules passed 80 starts/81 passes/zero
failures on its retained inputs; final normal HostTests compilation passes.
Final v9 changes only the new authority file from CRLF to LF and refreshes
three derived raw-source pins; the v8 artifact evidence remains tied to its
original bytes. Independent static inspection now accepts the saved v8 callback
arguments/returns, stack alignment, original Capture/CopyPlan and complete
copy/guard/tail readback. This does not execute firmware.

[Step969](../done/step969.md) records the first common-run failure: the legacy
artifact-verifier fixture omitted the new disabled M03 property, so its
synthetic 92-property record failed the verifier's exact 93-property check.
The repair adds only that false entry; verifier guards and all original
scenarios remain unchanged. The failed run was stopped after its completed
failing child inputs were archived; it remains failed/incomplete evidence.

[Step970](../done/step970.md) records the successful clean common gate on
`854a3359623f6a619b0ea769ce97cfed6c3a96b1`: 3,670 starts/3,671 passes/zero
failures, one terminal host marker, 16 selected READY lines, all 905 committed
inputs and five launch/end binaries unchanged. Fresh default and enabled
QEMU/OVMF captures then pass the source/profile, same-image native,
transcript/lifecycle and bounded disk joins. Both retain the actual
85-Compile/98-input/37-property closure, zero OS AssemblyRefs and 0/1 true
profile vectors. The enabled 31-record owned stream reports the original
query/allocation/recapture once each, 4,896 bytes copied from 102 descriptors,
zero copy mismatches, 32 intact guard bytes and 126,176 untouched tail bytes.
The matching snapshot/handoff key describes this capture only. See
[QEMU evidence](qemu.md#m03-001-original-capture-observation-evidence) for the
actual capture and lifecycle boundary.

The enabled ordinary smoke command is:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" `
  -TimeoutSeconds 30 -EnableM03MemoryMapCaptureProbe
```

Its reviewed transcript requires the `m03.001.capture` START/PASS pair,
actual query/allocation/recapture/copy/guard-tail/handoff records and exactly
one `CLROS_M03_001_READY`, followed by the ordinary boot path. A dry run or
publication does not prove that transcript occurred. Firmware remains
available; no additional allocation retry, pool-release policy, EBS call,
map-key freshness or general ownership claim is added.

### M03-002 and M04-010 source and publication checkpoint

[Step973](../done/step973.md) integrates the separately reviewed #539 ACPI and
#537 reference-barrier adapters. That combined explicit closure contained 88 ordered
Compile items, 102 raw publication inputs and 38 requested probe properties.
Existing profile/source equality guards remain; three older expected-source
lists gain the newly admitted generator, and all derived pins are refreshed.

The existing default-off `EnableAcpiPhysicalDiscoveryProbe` now validates and
copies the complete RSDP, selected root, MCFG, MADT and HPET ranges into a fixed
guarded aggregate. It records five retained descriptors, checks every copied
byte and unused tail, and keeps original embedded firmware addresses. The smoke
profile requires the exact 35 ordered/unique records and bounds physical and
owned ranges, accounting, summary bytes and runner lifecycle. It requires
`-BuildEfi -EnableAcpiPhysicalDiscoveryProbe -MarkerPreset acpi` for a real run;
its controlled dry-run does not build or execute firmware code.

The new default-off `EnableM04CheckedReferenceBarrierProbe` selects a bounded
noncollecting reference route on the original retained heap. A C# host generator
emits three returning helper thunks with explicit callback relocations; the
managed bulk route shares the same POD admission core. The source binds six
allocation roles and a finite 23-event sequence. Default and enabled publication
checks pass with zero managed AssemblyRefs, the original 336-byte serializer
and the enabled 1,645-byte reference object. Compiler field layouts and linked
helper/callback ABI, stack and SIMD behavior
were subsequently joined to the fresh images in [step977](../done/step977.md).
That historical static proof alone did not satisfy the live stack-canary
requirement; [step981](../done/step981.md) records the later observed sentinel.

The full reference integration initializer passes 5 starts/6 passes/zero
failures; the full ACPI initializer passes 4 starts/5 passes/zero failures.
Each includes actual default/enabled NativeAOT publications. The preceding
managed batch passes 82 starts/83 passes/zero failures. These focused results
remain separate from the later accepted `eeee523` common
suite and three fresh captures in [step977](../done/step977.md). The ACPI copy
and writer prerequisite are accepted within scope; [step981](../done/step981.md)
now also accepts the bounded reference executing-stack evidence. No real ExitBootServices, general
GC, Kestrel or readiness gate is promoted.

[Step979](../done/step979.md) now adds the original-stack getter and two-word
sentinel checks to the existing reference profile, without changing its 88
Compile items, 102 raw inputs or 38 flags. The full focused initializer passes
again, including fresh default/enabled publications with zero AssemblyRefs.
The exact reference body grows to 71 records; its 19-record reference block
adds `M04_REFERENCE_STACK` immediately before READY. The row requires the owned
stack geometry, initial observation, local witness, two fixed words, matching
armed/final reads and five checks. The production streamer rejects missing,
duplicate and late STACK records. [Step981](../done/step981.md) completes the
fresh `5d894c8` load/frame/storage and guest joins. The same compiled reader and
five checks support an 8,456-byte conservative local bound below the witness,
inside the 16 KiB ceiling; the actual guest reports both words preserved. The
full common suite passes 3,704 starts/3,705 passes/zero failures and 16 READY
lines. Default/reference/ACPI captures pass with 71 reference and 35 ACPI rows;
ordinary 750ms is configured drainage, not a separately measured quiet interval.
Pristine disks are reconstructed after capture from archived actual builders,
with bounded FAT/payload accounting. The source remains 88 Compile/102 raw/38
flags; this acceptance supplies no collecting GC or real ExitBootServices.

[Step975](../done/step975.md) corrects the reference READY marker to end at
the complete `allocations=6` token. The runner matches complete tokens; the
previous anchor ended inside `usedBytes=` and rejected the emitted numeric
field. The complete integration initializer now passes the actual dry-run
required, ordered, unique and forbidden vectors through the production
streaming matcher. It admits the full 70-record transcript and rejects the old
partial anchor, missing, duplicate and early READY records. The existing
`usedBytes=576`, `calls=23`, geometry and raw-transcript checks remain intact.
Its fresh five-group run passes, including both EFI publications; combined
committed-head validation and real guest evidence remain separate requirements.

[Step976](../done/step976.md) updates eight older M02 fixtures for the new
reference-profile conflicts. They retain exact guard counts and explicitly
assert every added conflict, including the IDTR diagnostic text. All eight
complete initializers pass together (37 cases, 40 starts, 41 passes and zero
failures), including the original default/enabled IDTR NativeAOT publications.
The earlier combined run failed on the stale checked-port count and remains
failed evidence. [Step977](../done/step977.md) records the later accepted clean
common gate and guest reviews; those historical results did not waive the
reference stack-canary requirement. The later proof and live checks in
[step981](../done/step981.md) satisfy it within the bounded profile.

### M03-003 host entropy-retention prerequisite

[Step978](../done/step978.md) adds a managed host contract that retains a bounded
32- or 64-byte guarded RNG result in caller-owned storage, clears its used
scratch slice and publishes status/length last. The normal HostTests build and
27-group focused run pass. This source is not admitted to the explicit EFI
closure and adds no probe property or native entry. Actual `EFI_RNG_PROTOCOL`
discovery, one `GetRNG` call, guarded retained bytes and genuine missing-source
guest evidence require separately scoped EFI/runner integration. #60 remains
open; no seed-bearing diagnostics, entropy quality, PRNG, TLS or real
`ExitBootServices` capability is claimed.

### M03-003 opt-in EFI entropy capture

`EnableM03FirmwareEntropySeedProbe` adds one mutually exclusive, default-false
profile. That checkpoint's explicit closure contained 89 ordered Compile entries, 103 raw
inputs and 39 requested flags. The normal managed retention contract remains
outside that no-stdlib closure; the EFI adapter uses numeric state and
explicit pointers.

The smoke command requires `-FirmwareRngSource` with this profile and
`-BuildEfi`. The values `virtio` and `missing` both mask RDRAND and RDSEED
on the `qemu64` CPU. Only `virtio` appends `rng-builtin` and `virtio-rng-pci`;
those arguments follow the existing devices to preserve their ordering.
Unknown, duplicate, empty and out-of-profile values are rejected before
side effects. For command inspection without a guest:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -BuildEfi -EnableM03FirmwareEntropySeedProbe -FirmwareRngSource virtio
```

The positive protocol has 27 records. The expected missing-provider protocol
has 19 records ending in a real `TEST_FAIL`; its caller remains in a terminal
loop. Ordinary smoke still rejects that failure. Its configured 750 ms drain
is not measured quiet evidence for the negative case: a separate capture-only
observer must establish the live interval, owned termination and exact EOF.
The positive and missing lanes must use identical enabled EFI bytes on
separate fresh disks. Ordinary image accounting reconstructs the pristine
image; the direct negative capture preserves its actual pre-QEMU pristine.

[Step980](../done/step980.md) records the whole five-module focused run
(201 starts, 202 passes, zero failures), the mapped geometry and both actual
publications. Independent offline review confirms the emitted ABI, original
firmware pointers, actual private readback/clearing, publication last and the
terminal failure branch. Six corrected legacy expectations retain all
original groups and assertions for the complete common suite. Full common
completion and fresh default/provider/missing-provider guest acceptance remain
pending; no firmware-exit or hosted-.NET gate is advanced here.

[Step984](../done/step984.md) corrects the positive firmware envelope after the
first real entropy capture completed all 27 owned records but ended before the
OVMF menu drawing. It admits only two exact observed suffixes: 251 bytes
(SHA256 `011BC86409F7A01C1494F57146920B37184A02063B7E995B44A99C6B083E6615`)
or 6945 bytes
(`BFBB6B92A5EEE7728E1AA54A2A4D262135215D7D2F267696E508A23BD514C61D`).
Stream counts follow the admitted variant; the owned protocol and negative
EOF contract are unchanged. The complete five-module regression passes
201 starts/202 passes/zero failures, including 329 raw/lifecycle controls and
both NativeAOT publications. The original capture remains failed; the missing
lane did not run. A new final-head common suite and three fresh captures remain
required. No seed values or entropy-quality claims are added.

[Step986](../done/step986.md) records the later successful three-lane capture
from accepted common head `8942ab7`. The positive image uses the exact 251-byte
suffix above and all 27 owned records. The missing-provider capture reuses
the complete enabled EFI bytes, emits 19 owned records ending in the expected
failure, and has no firmware suffix. Its separate capture-only observer proves
764.127 ms of quiet while the process remains live, then owned termination and
EOF. The unchanged ordinary streamer rejects its TEST_FAIL record.

All seven archived native/managed files per lane match the reviewed common
publications. The only sidecar differences are the exact output path and
timestamp, bound to each actual publication; the missing lane inherits the
positive publication. Independent disk accounting verifies the EFI payload,
GPT/FAT/FSInfo and all firmware NVVARS changes. Ordinary pristine images were
reconstructed after capture; the missing lane also retains its actual
prelaunch pristine image. None of this proves real ExitBootServices or a
post-firmware random service.

### M03-004 host adapter prerequisite

[Step988](../done/step988.md) compiles the actual typed one-attempt adapter
together with unchanged `FirmwareServicesMode.cs`, `MinimalRuntime.cs` and
`MethodImplAttribute.cs` under .NET 10/C# 14, NoStdLib and disabled implicit
framework references. Metadata inspection finds zero AssemblyRefs and actual
Prepare/Attempt IL bodies. That child assembly is not executed or published
as EFI; the existing EFI Compile list and all probe profiles remain unchanged.

The complete six-module focused harness passes 53 starts/54 passes/zero
failures, followed by a successful normal Release HostTests compilation.
Controlled unmanaged host callbacks prove exact opaque handle/key forwarding,
full-width status preservation, one-shot/reentry refusal and unchanged global
firmware mode. A separately claimed shared-file supplement must source-link
this core, inspect its actual emitted indirect ABI and prove a default-off
controlled callback profile under QEMU. The original firmware ExitBootServices
slot, final-map ordering, retry and post-exit behavior are outside this evidence.

### M03-004 controlled EFI profile integration

[Step991](../done/step991.md) supplies the #546 integration above. The mutually
exclusive `EnableM03TypedExitBootServicesCallProbe` flag defaults to false;
it source-links the existing typed adapter and adds a same-image unmanaged
callback. It uses the original EFI caller stack with all M01 flags disabled.
The exact closure contains 95 Compile items, 109 raw inputs and 41 flags.
Producer, artifact verifier and smoke-runner source/profile checks agree on
that closure. All original fixture initializers remain intact.

The new whole integration module passes five starts/six passes/zero failures,
including mapped callback and orphan-process controls, strict parser/streamer
cases, all profile conflicts, and fresh default/enabled NativeAOT publication.
Eight complete managed modules separately pass 115/116/0. Both EFI archives
have zero AssemblyRefs and are retained for independent emitted-code review.
The full common suite and fresh QEMU acceptance were pending at step991;
step1003 below supplies that evidence. Publication alone is not guest boot proof.

The intended enabled runner uses `-BuildEfi
-EnableM03TypedExitBootServicesCallProbe` with explicit fresh EFI/image/log
paths and the installed QEMU/OVMF pair. Its numeric grammar has 30 ordered,
unique records: 13 original preceding records, nine controlled records, then
eight original boot records. Actual firmware suffix bytes and ordinary-runner
lifecycle checks are retained. No original firmware exit pointer, final-map
key, retry, global mode transition or M01 stack capacity is supplied here.

### M04-011 opt-in current-image module registration

`EnableM04TypeManagerModuleRegistrationProbe` adds a mutually exclusive,
default-false profile requiring the original M01 loaded-image/stack profile.
Its adapter calls the shared one-module metadata core with the captured image
and a fixed, aligned 4096-byte owner buffer. That checkpoint's explicit closure contained 92 Compile
entries, 106 raw publication inputs and 40 requested flags. The core is admitted
by its exact canonical path and complete reviewed source hash; altered bytes
and the same source at another path remain rejected.

For command inspection without a guest:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -BuildEfi -EnableM04TypeManagerModuleRegistrationProbe -EnableM01LoadedImageStackProbe
```

The 45-record protocol covers undersized-storage rejection, one registration,
slot-derived handle/48-byte ABI consumption, present-empty versus missing
sections, repeated/foreign registration rejection and retained readback.
Canaries occupy the final 32 bytes of the admitted owner buffer after
registration; they are internal checks, not external guards or a stack probe.

[Step985](../done/step985.md) records the complete eleven-module managed batch
(194 starts/195 passes/zero failures), the complete five-group EFI initializer
(5 starts/6 passes/zero failures), both default/enabled NativeAOT publications
and a successful normal HostTests Release build. These were focused host and
publication results; step989 below records the subsequent common, native and
guest acceptance. No GC-static initialization, root enumeration, collecting GC
or hosted runtime is accepted.

[Step987](../done/step987.md) corrects four legacy fixtures that compare the
complete raw OS.csproj bytes. They now expect SHA256
`bc74c58deea0556a1e752c3f1b6b58e9a5f44fcc7c5901b1e5959e97d6bca953`
for the existing TypeManager closure. Their original assertions and complete
initializers are retained; the focused regression passes 27 starts/28 passes/
zero failures. No EFI input or profile behavior changed. The first full common
failure remains recorded.

[Step989](../done/step989.md) accepts the finite one-module adapter at tested
commit `34bd75c`: the complete common suite passes 3733 starts/3734 passes/zero
failures, and independent native, raw serial/lifecycle/provenance, numeric
association and disk reviews accept fresh default and TypeManager QEMU captures.
The executing image supplies one RTR16.0 module with 31 rows. Registration uses
1824 of 4096 admitted bytes, publishes the separate 48-byte ABI through the
16-byte slot, and a real slot-derived consumer reads all six ABI words. The
45 ordered unique records include the rejection paths and end at the expected
terminal record. General TypeManager/runtime support remains deferred.

The accepted guest commands used the ordinary runner, `-BuildEfi`, a 30-second
QEMU timeout, and explicit fresh artifact/image/serial/summary paths. The
enabled command additionally used both switches below; the default command
used neither:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi -EnableM04TypeManagerModuleRegistrationProbe -EnableM01LoadedImageStackProbe -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
```

Both wrappers returned zero; QEMU was runner-terminated with signed exit -1.
The 750ms quiet interval is configured, not independently measured. Pristine
disk images were reconstructed after capture; all changed disk bytes were
accounted for as OVMF NVVARS/FAT bookkeeping. Internal tail canaries were
initialized after registration; no external RAM oracle or numeric guest stack
canary measurement is claimed.

### M04-020 current-image unwind metadata acceptance

`EnableM04X64UnwindMetadataProbe` is disabled by default and requires
`EnableM04TypeManagerModuleRegistrationProbe`, which retains its original
`EnableM01LoadedImageStackProbe` prerequisite and exclusions. The child uses the
same registered module, a measured 64 KiB static snapshot with a 32-byte unused
tail guard, and the original 33-page stack. Two actual invoked method pointers
are looked up alongside their last in-range bytes and two expected misses.

Step1057 records the explicitly enabled C# EFI profile compiling with zero
warnings/errors before the indexed decoder extension. Its build command is:

```powershell
dotnet build .\src\OS\OS.csproj --nologo -m:1 -p:EnableM01LoadedImageStackProbe=true -p:EnableM04TypeManagerModuleRegistrationProbe=true -p:EnableM04X64UnwindMetadataProbe=true
```

The complete integration has 116 Compile items, 131 raw inputs, 48 public
properties (45 literal false defaults) and 21 diagnostic catalog entries.
Publisher/verifier/runner integration and focused checks passed on that source.
Development native publication and the ordinary QEMU wrapper passed the complete
67-record profile, as recorded in [step1057](../done/step1057.md). The indexed API
in [step1060](../done/step1060.md) changes decoder identity; those older native
bytes are not evidence for the changed source. Its exact publisher source guard
and dependent aggregate fixtures are reconciled in [step1062](../done/step1062.md).
Fresh current-source native and guest acceptance pass at `4625fd7` in
[step1071](../done/step1071.md), including the indexed decoder. The original full
host gate has 3,864 starts, 3,865 passes and no failures. Current default,
TypeManager and unwind profiles have 21/45/67 complete application records.
Independent reviews bind all current publisher inputs, emitted code, stack
bounds, raw serial, runner lifecycle and actual postboot FAT payloads. The
unwind graph uses 7,440 bytes below module entry and 8,768 below the original
continuation; firmware callback bodies are excluded. The decoder's bounded
stack buffer uses the existing fixed RX cookie-anchor convention; randomized
initialization remains separate hardening work.
The original TypeManager records remain intact; selected guest output adds a strict
22-record metadata tail. Do not accept a marker override or a host fixture as
real-image consumption. Version-one/listed-chain bounds remain enforced even if
an emitted compiler record is unsupported. No stack walk or handler executes.

### GC-static root diagnostic

The default-off `EnableM04GcStaticRootMaterializationProbe` uses the existing
M01 loaded-image/live-allocator and M04 endian/span/arithmetic/layout/heap/
RhpNewFast prerequisite chain. It excludes other allocation schedules and
firmware-exit diagnostics. The producer and verifier admit 98 explicit Compile
items, 112 raw inputs and 42 profile flags. The existing checked reference
helper is selected for this profile without changing its generated bytes.

[Step994](../done/step994.md) records a genuine compiled static object field,
one retained static-base allocation, and one ordinary value allocation through
the same cursor. Both enabled and default fresh QEMU boots pass. Complete
regression and final native/stack acceptance remain pending; this diagnostic
does not implement collection or general static initialization.

[Step995](../done/step995.md) adds original-owned-stack admission, two canaries
armed once, and five read-only checks around the actual GC operations. The
strict numeric transcript now has 60 CLROS lines and 49 ordered/unique anchors.
Fresh QEMU development evidence passes with both canaries retained. The
10,056-byte first-party coordinator bound excludes firmware body depth; final
common/default-and-enabled acceptance remains pending.

[Step997](../done/step997.md) records acceptance of the GC first-party native
review and the three-profile output-authority compatibility correction.
The combined full regression and final guest acceptance remain pending.

[Step998](../done/step998.md) corrects five older diagnostic fixtures whose
exact conflict lists omitted the GC-static profile. All five complete small
initializers pass; full combined and final guest acceptance remain pending.

[Step999](../done/step999.md) prepares the final-map/exit window for M03-005.
Its seven-source no-stdlib closure compiles with zero AssemblyRefs. It is not
yet included in the EFI project or runner, and adds no switch or guest claim.

[Step1000](../done/step1000.md) aligns five further EFI fixtures with reviewed
source hashes, both IDTR profile exclusions and the shared stack getter guard.
Production inputs and the accepted native review are unchanged; full combined
and final guest acceptance remain pending.

[Step1001](../done/step1001.md) prepares M03-006's bounded stale-key retry
coordinator over the single final-map/exit window. Its eight-source no-stdlib
closure has zero AssemblyRefs. It is not linked into the EFI project and adds
no profile, firmware transition or guest acceptance claim.

[Step1002](../done/step1002.md) corrects the helper-ledger fixture's source
declaration count for the two exclusive callback profiles. Complete diagnostic
checks of the remaining 218 initializers pass after resolving three diagnostic
dispatch errors. This is not a full common pass; production/native inputs are
unchanged and the corrected common run and final guest acceptance remain pending.

[Step1003](../done/step1003.md) accepts the combined typed-EBS and GC-static
batch at `2be3386`. The original full solution build and unfiltered HostTests
run pass (3787 starts/3788 passes/zero failures), followed by the ordinary
dry-run and fresh default, typed-EBS and GC-static QEMU captures. All 112 raw
publisher inputs match the reviewed snapshots; the 42 evaluated flags have
respectively 0, 1 and 9 enabled values. Each EFI image is byte-identical to
the independently reviewed native image. Native, raw/lifecycle, numeric
association and boot-disk accounting reviews pass.

The typed-EBS profile has 30 exact CLROS lines and four controlled callbacks.
GC has 60 exact CLROS lines, 49 ordered/unique anchors, two allocations/48 bytes,
two checked stores and five stack checks. The 10056-byte first-party stack
bound excludes original firmware console/serial bodies. No collecting GC,
real firmware exit, general runtime, hosted .NET/Kestrel or cloud claim follows.

[Step1004](../done/step1004.md) integrates the final-map window and bounded
stale-key retry into `EnableM03TypedExitBootServicesCallProbe`. The diagnostic
uses authentic GetMemoryMap captures into a private retained 128 KiB buffer,
with one controlled successful exit, stale-then-success, and two stale returns.
Five captures precede five controlled exits; all reporting occurs afterwards.
It never reads the original firmware exit slot. That source checkpoint contained
100 Compile items/114 raw inputs, with the same 42 profile flags. The complete
focused EFI fixture passes, including default/enabled publication and strict
45-record transcript checks. The default image retains step1003's exact bytes.
[Step1010](../done/step1010.md) accepts the original full suite
(3798starts/3799passes/zero failures), changed-native review and fresh ordinary
default/enabled QEMU captures with source/native/disk association. Original
firmware function-body stack depth remains excluded. At that milestone, the next
live boundary was #64: real firmware exit followed by permanent owned-CR3
adoption. Its later scoped acceptance is recorded in
[step1051](../done/step1051.md).

[Step1011](../done/step1011.md) admits the bounded collector through the existing
GC-static profile. That checkpoint's explicit closure contained 101 Compile items and 115 raw inputs;
the same 42 flags and nine enabled GC prerequisites remain. Exact reviewed
allocator/collector text identities and the canonical project check are retained.
The normal runner now requires 70 records and 59 ordered, unique anchors,
including nine collection records and `CLROS_M04_013_READY`. It checks the finite
compiler-object graph, collection counts, reclaimed-address identities, retained
root, allocation/store counters and all ten stack observations. The original GC
fixture and enabled publisher preflight pass.

[Step1041](../done/step1041.md) completes the integrated acceptance at `42a516e`:
the full host suite and fresh ordinary default/GC QEMU captures pass, with
source/native/lifecycle and boot-disk association. The final collector EFI is
byte-identical to the reviewed development image; the canonical COFF differs
only in debug information, while runtime bytes, symbols and relocations match.
The default and GC profiles contain 21 and 70 CLROS records respectively.

At the accepted `42a516e` revision, the runner's informational GC summary said
`lines=60` and `nonCollecting=true`; those fields were obsolete. The strict parser,
70-record raw capture and `CLROS_M04_013_READY` establish the scoped acceptance.
The first-exit/churn integration in [step1042](../done/step1042.md) corrects that summary. No general
runtime, hosted .NET or real firmware-exit claim follows from this profile.

## Bounded throw/catch profile

`EnableM04SimpleThrowCatchProbe` remains false by default and requires the
collecting-GC profile with its complete ancestry. The accepted `ead4fca` closure
has 121 explicit `Compile` items, 137 raw publication inputs, 51 public flags
with 48 literal false defaults, and six C# generator files with eight declared
inputs. The C# generator supplies the 1,350-byte COFF object; publisher/verifier
checks bind its actual throw, selection/continuation callbacks and exact EH
contribution.

[Step1075](../done/step1075.md) records the original common gate and three fresh
ordinary `-BuildEfi` captures: default, GC and simple-catch profiles emit 21, 70
and 82 application records respectively. The catch transcript includes rejection
of an invalid local RIP through the production admission predicate, then the
required real catch and continuation. Live context stays unchanged; this is not
terminal corrupted-context proof. Complete commands and final source, native,
raw, lifecycle and disk identities are retained in the step and each original
`wrapper.command.json`.

[Step1066](../done/step1066.md) retains the 81-record development guest and its
CLI-versus-wrapper limitation. Final acceptance uses fresh ordinary wrappers;
older counts and images are not relabeled. General EH and broader gates remain
incomplete.

## Single-finally development profile

[Step1088](../done/step1088.md) accepts the bounded #589 execution profile.
The default-off `EnableM04SingleFinallyCatchProbe` sibling uses
the same collecting-GC ancestry. It excludes simple catch, churn, constructors,
interface dispatch and the separate unwind-metadata profile. The current source
closure retains 121 explicit Compile items and 137 publication inputs, while
the flag vector expands to 52 with 49 literal false defaults.

The generator's `--single-finally-catch` mode emits a distinct 1,707-byte object
and `.finally.obj` publication sidecar. The accepted enabled image has 643 ILC
methods and 646 runtime-function rows. Its 136-byte Catcher and exact 11-byte
EH table select one returning cleanup, then one typed catch and its compiled
continuation. The bridge checks the parent-stack stamp, original exception/root,
register context and control fields; the Thrower-to-parent RSP delta is 48.
The measured CLROS stack bound is 14,240 bytes, excluding firmware body depth
and arbitrary interrupts. Steps1079–1085 retain implementation and native-review
history, including the corrected frame admission.

The fresh ordinary QEMU/OVMF capture has the complete
[84-line transcript](diagnostics.md#single-finally-development-transcript), one
cleanup/catch/continuation, and local-input rejection without live corruption.
Default, collecting-GC and legacy-catch regression captures also pass. The full
host gate at `3bfa349` passes 3,898 groups plus the suite summary; step1088 joins
that source to the guest source `736d10d` and records the retained failed runs.

The public probe remains false by default. Explicit smoke selection applies
exact artifact and whole-transcript checks. Dry-run alone is not boot proof.
This finite pre-exit fixture does not establish general EH, rethrow, hosted .NET,
Kestrel, cloud or production readiness; #90 and Gate4 remain incomplete.

## Owned subsystem survival and panic profiles

The disabled #67 survival profile requires EnableM01LoadedImageStackProbe,
EnableM01LiveAllocatorProbe, EnableAcpiPhysicalDiscoveryProbe,
EnableM03FirstRealExitBootServicesProbe and EnableM03OwnedSubsystemSurvivalProbe.
The separate terminal profile additionally selects EnableM03OwnedSubsystemPanicProbe.
All existing first-exit prerequisites and exclusions remain; survival and
firmware revocation are explicitly exclusive.

The initial development closure has 115 Compile entries, 130 raw publication
inputs and 49 referenced flags with 46 literal false defaults. These are the
specific pre-integration image's identities, not unconditional future counts.
Original locked preflight passes. Native compilation/linking produced an image,
but the original artifact verifier hit its unchanged 60-second limit without a
verdict. One unchanged verifier-only invocation later passed in 52,906 ms under
the same 60-second bound, with all 130 source, 56 native and 28 tool inputs
unchanged. Independent native review found no scoped blocker; no guest had run
at that development checkpoint. Preserve the original failed publication,
earlier Desktop module-resolution failure and all archives in
[step1067](../done/step1067.md). The fresh acceptance below supersedes that
pending status without changing the original failed receipt.

The accepted 4ef299f source has 117 ordered Compile items, 132 raw
publication inputs, 50 profile flags and 47 literal false defaults. Its fixture
reconciliation includes the final step1068 verifier identity. One fresh managed
build and 45 original focused checks plus two existing authority helpers pass;
the kernel, runner and shared DLLs are bound to that build. [Step1069](../done/step1069.md)
records the exact current hashes and preserved historical contracts. This is
focused source/profile evidence. [Step1074](../done/step1074.md) now accepts the
frozen 4ef299f source with the original full gate and four fresh ordinary
publications/captures: default 21, first-exit 66, survival 76 and panic 77 application
records. The panic runner returns 19; only its exact terminal transcript and
measured quiet interval allow the wrapper to return 0. Fresh native, raw/lifecycle
and GPT/FAT/EFI payload association pass. Complete executed command vectors and
source/tool/producer identities are retained per profile. All selected flags
remain disabled by default; this does not promote broader completion gates.

The step1068 verifier path-walk optimization is included in that accepted source.
Its approximately 8.74x measured gain applies to the helper benchmark only, with
unchanged authority checks and deadline. Symlink creation and ACL-denial fixture
coverage remain unavailable on this host, as recorded in the original results.
