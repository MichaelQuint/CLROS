# CLROS Reference Map

## Current Status

The local `references/` directory contains 29 external project snapshots. They
are inspection inputs for CLROS; they are not first-party source, are not
included by `CLROS.slnx`, and cannot supply completion-gate evidence.

The snapshots have no `.git` metadata. A directory suffix such as `-main` or
`-master` identifies the archive name, not a verified branch or commit. No
snapshot has a top-level file named `LICENSE`, `LICENCE`, `COPYING`, or
`NOTICE`; some READMEs identify a license and some trees contain component
licenses. `docs/license-audit.md` records those distinctions. Until an exact
upstream revision and applicable license are recorded, use a snapshot for
architecture study and independently authored tests only.

Older documentation named `references/nanos-master`,
`references/Cosmos-master`, and
`references/daniilvaino-SharpOS-main`; none of those paths exists in this
workspace. The actual corresponding modern Cosmos and SharpOS snapshots are
`references/Cosmos-gen2` and `references/SharpOS-main`. There is no local Nanos
snapshot, so its appliance model may be discussed as product context but not
as inspected local source.

`references/AGENTS.override.md` makes the directory read-mostly and build
isolated. Reference projects must not be added to `CLROS.slnx`, imported from a
first-party project, or used to rename CLROS or replace its architecture.

## Evidence Classes

| Class | Meaning |
| --- | --- |
| Selective reference | Inspect a small, named design or algorithm. Reimplement it against CLROS contracts after provenance and license review; do not port a subsystem wholesale. |
| Test-vector reference | Derive independent positive, negative, overflow, malformed-input, or state-machine tests. Reference code is not copied into the product. |
| Conceptual reference | Use only to compare architecture, threat models, or subsystem decomposition. |
| Ignore for implementation | The project does not advance the current CLROS path. It may still document a failure pattern or incompatible design. |

All four classes are weaker than first-party evidence. A reference README,
screen capture, `done/stepNN.md`, test, or binary proves only what its own
project reports. CLROS claims require CLROS source, generated artifacts,
captured logs, stable markers, and the matching entry in
`docs/completion-gates.md`.

## Inventory

| Snapshot | Class | Useful locus and intended use | Boundary |
| --- | --- | --- | --- |
| `references/abanu-master` | Test-vector reference | `src/` and `build/` material for seeded allocator stress, shadow-content validation, fragmentation, and coalescing scenarios. | x86/custom-compiler/native implementation is incompatible with the C# 14/.NET 10 NativeAOT path. |
| `references/AtomOS-master` | Conceptual reference | `src/` for paging, threading, timer, ACPI, FAT, syscall, and shared-memory topic comparison. | Do not adopt its 32-bit, POSIX-like, or custom compiler/toolchain architecture. |
| `references/Aura-Operating-System-master` | Test-vector reference | `SRC/` for package-acquisition and archive-handling threat cases. | Its shell/package-manager posture conflicts with the minimal appliance. Convert unsigned HTTP and path traversal behavior into rejection tests, not features. |
| `references/Brovan-main` | Test-vector reference | `Brovan/` for hostile PE/ELF/raw-image inputs and host-side emulation/tracing ideas. | README identifies GPL-2.0. Keep implementation and emulator/native dependencies outside first-party kernel/std/app code. |
| `references/CosmosELF-master` | Ignore for implementation | `CosmosELF/`, `CosmosELFCore/`, and `TestBinary/` may contribute malformed-loader cases. | The parser is incomplete and uses incompatible packing/format assumptions; it is not a CLROS loader base. |
| `references/Cosmos-gen2` | Test-vector reference | `source/`, `Tests/`, and `Tools/` for older device, filesystem, network, and QEMU test census. | Do not adopt IL2CPU, plugs, assembly, bundled tools, or the Gen2 runtime architecture. Prefer the Gen3 snapshot when it has equivalent coverage. |
| `references/CSharpLLVM-master` | Conceptual reference | `CSharpLLVM/` and `runtime/` for compiler/runtime-boundary comparison. | It is not NativeAOT, omits GC, and has incompatible core-type/generic limitations. |
| `references/dotnet-aot-xc-main` | Ignore for kernel work | `src/` may inform future host-side NativeAOT cross-build tooling. | Linux glibc/musl cross compilation is not the CLROS kernel or `OSHost_*` PAL. |
| `references/EfiSharp-main` | Selective reference | `EfiSharp/`, `EfiSharp.CoreLib/`, and build files for UEFI structure/link/build cross-checks and firmware-RNG failure tests. | Do not import its old custom corelib, mini-libc, native source, or any entropy fallback that silently returns zero bytes. |
| `references/elfsharp-master` | Test-vector reference | `ELFSharp/` and `Tests/` for host-side ELF corpus ideas if ELF remains a tooling format. | Its exception/allocation-heavy parser is unsuitable for boot/kernel paths; PE32+ remains the native application format. |
| `references/expressos-master` | Conceptual reference | `Source/` for ownership, capability, and secure-storage threat-model comparison. | Do not add L4/Linux/POSIX dependencies or translate policy uncertainty into permissive behavior. |
| `references/FlingOS-develop` | Conceptual reference | `Documentation/`, `Drivers/`, `Kernel/`, and `Testing/` for educational decomposition and documentation/test ideas. | README identifies GPLv2. Do not copy its custom compiler, assembly, or implementation into first-party code. |
| `references/GlieseOS-main` | Ignore | `src/` contains at most a negative allocation-per-frame example. | It is a Cosmos-based watch experiment and supplies no relevant kernel/runtime/cloud substrate. |
| `references/Kernel-master` | Ignore | `Kernel/`, `Root/`, and `System/` can illustrate incomplete bootstrap/fake-runtime failure modes. | Native-heavy, incomplete, and not a usable NativeAOT architecture. |
| `references/KernelSharp-master` | Test-vector reference | `src/` for fixed-memory, allocator, and paging failure cases. | Do not reuse its unsafe fixed-address allocator/runtime implementation or `libs_asm/`. |
| `references/libcs-master` | Ignore | `libcs/` and `libcs.SourceGenerator/` may supply incomplete syscall-generator negative cases. | Its POSIX/syscall direction is outside the current PAL design. |
| `references/libvirt-csharp-main` | Selective host-only reference | `tests/` for `test:///default` lifecycle, error, and cleanup scenarios. | Package metadata identifies LGPL-2.1-only. Do not link it into an image or copy unchecked P/Invoke ownership; the first real CLROS path uses guarded `virsh`. |
| `references/MOOS-master` | Conceptual/test-vector reference | `Kernel/`, `Corlib/`, and networking/storage trees for device topic census and unsafe ABI negative cases. | Native-heavy portions and managed-string/raw-pointer service ABIs are not valid across `OSHost_*`. |
| `references/MOSA-Project-master` | Test-vector reference | `Source/` and `Tests/` for independently authored `BinaryPrimitives`, `MemoryMarshal`, `Unsafe`, span, collection, stream, allocator, and PCI cases. | Do not adopt the MOSA compiler/runtime. Treat bundled tools and third-party components separately. |
| `references/nativeaot-patcher-main` | Selective reference | `src/` and `tests/` for .NET 10 NativeAOT/GCInfo behavior, PCI/MSI-X, virtio, NVMe, networking, scheduler/runtime failures, and QEMU/UART evidence patterns. | Do not import Limine, patcher/plugs, C/assembly, HHDM assumptions, exception-based driver failures, or flat exports that bypass `OSHost_*`. |
| `references/Nitrocid-main` | Ignore for bare-metal work | `public/` may provide optional hosted shell/UI or simulator ideas after the appliance is complete. | GPL-3.0-or-later metadata and a users/shell/remote-management model conflict with the default CLROS posture. |
| `references/PatienceOS-main` | Test-vector reference | `src/` for host-testing unsafe console/buffer routines against caller-owned stack memory. | Do not adopt its 32-bit Multiboot, VGA, or assembly path. |
| `references/RoseOS-master` | Test-vector reference | `Loader/`, `Kernel/`, and `WindowsTest/` for UEFI memory-map sizing, `ExitBootServices`, PE validation, reservation, and allocator regressions. | Do not copy its map-buffer sizing, loaded-image reclamation, fixed allocator assumptions, C, or assembly. |
| `references/Sharpen-master` | Conceptual reference | `kernel/` and `user/` for a historical subsystem/device/filesystem/network census. | Do not copy the old native-heavy x86 implementation. |
| `references/SharpOS-main` | Selective primary, owner-authorized CC0 reference | `OS/`, `std/`, `apps_native/`, `bootasm/`, `docs/`, and `done/` for modern C# NativeAOT boot/runtime, PE apps, PAL, scheduler, storage, and evidence patterns. The owner-supplied permission record and current upstream CC0-1.0 license permit selective reuse of SharpOS-authored source. | Adapt bounded slices and record the exact local source hash because the archive commit is unknown. The snapshot is net8 for its kernel/app tier, relies on an absent external CoreCLR fork, has no live network stack, and contains separately GPL-governed DOOM code. It is not CLROS architecture or evidence. |
| `references/SharpOS-master` | Test-vector reference | `Kernel/`, `AOT/`, and historical test material for arithmetic, casts, arrays, strings, generics, exceptions, delegates, interfaces, boxing, and cctor behavior. | Historical custom compiler/runtime; license is not locally established. Recreate tests independently and do not copy implementation. |
| `references/smolsharp-main` | Conceptual reference | `src/` and build properties for binary-size, section-stripping, CRT-removal, and DirectPInvoke threat-model review. | Size cannot remove unwind/protection/diagnostic data. The snapshot has no top-level license evidence and points at AGPL ZeroLib work; do not copy it into CLROS. |
| `references/xOS-main` | Ignore | `xOS/` and `xOSTests/` describe a Cosmos shell application. | It is not a kernel/runtime architecture; its bundled `CosmosOS-License` applies to Cosmos-derived material, not automatically to every dependency. |
| `references/zerosharp-master` | Selective reference | `efi-no-runtime/`, `no-runtime/`, and `with-runtime/` for `[RuntimeExport("EfiMain")]`, explicit no-stdlib build properties, EFI subsystem/entrypoint checks, and reference removal. | Do not import prebuilt/custom CoreLib artifacts without exact provenance and applicable license evidence. |

## Primary SharpOS Crosswalk

The following local paths exist and are the first review points for the named
CLROS backlog slices:

| Local path | CLROS use | Required adaptation |
| --- | --- | --- |
| `references/SharpOS-main/OS/src/Boot/ExitBootServicesProbe.cs` | Fresh-map/`ExitBootServices` ordering and bounded retry study. | CLROS must first own serial, stack, reservations, allocator, paging, and copied firmware data. Use typed status returns and prove no allocation between the final map and call. |
| `references/SharpOS-main/OS/src/Boot/BootStackSwitch.cs` | Owned boot-stack transition study. | Reserve the loaded image and old/new stack ranges explicitly; add guards and prove `RSP` before reclaim. |
| `references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs` | C# build-time x64 stub-generation design. | Begin with CLROS's already audited fixed byte plans. Add a generator only when a second live stub family justifies it and keep emitted pages RW-to-RX, never permanent RWX. |
| `references/SharpOS-main/OS/src/Kernel/Pe/PeImageLayout.cs` | PE32+ layout and range-check ideas. | Use bounded caller-owned data and independent malformed-input tests. |
| `references/SharpOS-main/OS/src/Kernel/Pe/PeRelocations.cs` | Checked DIR64 relocation study. | Accept only supported relocation kinds and reject all overflow/out-of-image targets. |
| `references/SharpOS-main/OS/src/Kernel/Pe/PeImports.cs` | Import-table study. | Resolve only the declared versioned CLROS app/PAL surface; never expose arbitrary kernel addresses. |
| `references/SharpOS-main/OS/src/Kernel/Threading/` | Scheduler, wait, and thread-state-machine study. | Preserve CLROS's fixed-capacity/status-code style and add preemption, GC-safe-point, race, timeout, and ownership tests. |
| `references/SharpOS-main/OS/src/Hal/Fat32.cs` | FAT32 edge-case study. | Keep storage read-only first and add loop, bad-cluster, truncation, bounds, and long-name tests. |
| `references/SharpOS-main/done/step076.md` | Historical account of a gated physical EBS experiment. | It is not CLROS proof and explicitly halts rather than entering the default launcher path. |
| `references/SharpOS-main/done/step143.md` | Historical account of playable Managed DOOM with GOP, PS/2, and HPET. | It is not locally reproduced; GPL app code and WAD assets remain isolated and optional. |

## Secondary NativeAOT/Cosmos Gen3 Crosswalk

These local paths also exist:

- `references/nativeaot-patcher-main/src/Cosmos.Kernel.Core/Memory/GarbageCollector/`
- `references/nativeaot-patcher-main/src/Cosmos.Kernel.Core/Runtime/ExceptionHandling/`
- `references/nativeaot-patcher-main/src/Cosmos.Kernel.HAL/Devices/Virtio/`
- `references/nativeaot-patcher-main/src/Cosmos.Kernel.HAL/Devices/Network/VirtioNet.cs`
- `references/nativeaot-patcher-main/src/Cosmos.Kernel.HAL/Devices/Storage/NvmeController.cs`
- `references/nativeaot-patcher-main/tests/Cosmos.TestRunner.Engine/Hosts/QemuX64Host.cs`
- `references/nativeaot-patcher-main/tests/Cosmos.TestRunner.Engine/Protocol/UartMessageParser.cs`

Use them to cross-check specifications, derive independent tests, and identify
failure modes. A CLROS implementation still follows `BootSequence.cs`,
`Probes.cs`, `std/no-runtime/shared`, `OSHost_*`, caller-owned buffers, status
results, stable markers, and the documented no-reference-build boundary.

## Tests

Documentation-only changes run:

```powershell
$directories = Get-ChildItem .\references -Directory
if ($directories.Count -ne 29) { throw "reference inventory changed" }
Test-Path .\references\SharpOS-main\done\step143.md
Test-Path .\references\nativeaot-patcher-main\src\Cosmos.Kernel.HAL\Devices\Virtio
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
git diff --check
```

The naming/source-firewall script scans the solution plus first-party project,
props, and targets files and fails for any build-time reference import. It
does not build reference projects or turn them into first-party evidence.

## Next Steps

1. Record an immutable upstream URL and revision before adapting any source.
2. Record the exact source path, license evidence, changed CLROS paths, and
   adaptation boundary in the implementing issue and `done/stepNN.md`.
3. Keep reference-derived tests independent and include negative cases that
   prevent importing known reference weaknesses.
