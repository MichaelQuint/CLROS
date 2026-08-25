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
| `references/SharpOS-main` | Selective primary, owner-authorized CC0 reference | `OS/`, `std/`, `apps_native/`, `bootasm/`, `docs/`, and `done/` for modern C# NativeAOT boot/runtime, PE apps, PAL, scheduler, storage, and evidence patterns. The owner-supplied permission record and upstream CC0-1.0 license permit selective reuse of SharpOS-authored source. | The local archive's content matches upstream `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` except for its omitted top-level `LICENSE`; still bind adaptations to exact paths and hashes. Current upstream `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` is research-only until selectively adapted. SharpOS relies on a separate CoreCLR fork, still has no network driver, and contains separately licensed/GPL material. It is not CLROS architecture or evidence. |
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

### SharpOS upstream refresh (2026-08-25)

Static comparison against `https://github.com/daniilvaino/SharpOS` established
these immutable anchors:

- Local archive content: upstream
  `b80ee3a4175e1522d1a12ba7fe371a49da3207f5`, with no content difference
  except that the archive omits upstream's top-level `LICENSE`.
- Current reviewed upstream `main`:
  `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`, dated 2026-08-21.
- Comparison:
  `https://github.com/daniilvaino/SharpOS/compare/b80ee3a4175e1522d1a12ba7fe371a49da3207f5...0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`.
- Delta: 34 commits beyond the archive, including steps 144-165; 24 commits
  are newer than the previously reviewed step-149 commit
  `934b4fd8e4e8849d13d774bacfef063870a18d05`.

The newer source is not copied into `references/`. CLROS uses the following
findings as source-linked regression inputs to existing backlog issues:

| Upstream finding | Immutable examples | Existing CLROS destination |
| --- | --- | --- |
| Variant interface dispatch must resolve the compatible interface and owning slot correctly | `25dafb69c43622fc5786a894ea63492c39745b29`, `c675b268f0901767a74c07113bd796894185a367` | #85 / M04-016 |
| Unwind lookup needs the containing image base; SIMD makes XMM/MXCSR and `UWOP_SAVE_XMM128` handling live | `00e99fb23b0e44223101285856d2801b1eff043a`, `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` | #89, #91, and thread-context #183 |
| Application GC/root/array coverage and mapped-image/page release require repeatable ownership tests | `c8f814b0caee22645198ea605e3b4eaabb155fd5`, `f2f21085a3f78d452052382d978f1981aa479b64` | #74-#91 and #172-#179 |
| LAPIC preemption, stop-the-world, waits, locks, tasks, and preempted allocation expose atomic-publication and parked-root cases | `caa7713f42c56137be08fbcea30e2008e0c57a2a`, `b83cbf36ec63d444063c739b7b562980e32a8959`, `d08a170d10cb43b67ac0cd23a6a52cc4dd2b5684`, `6e4d6a67940fafcba163a3abdaab5ea01d886d48`, `491730dc46e3316171d745c33d25cd8db408b84c`, `c675b268f0901767a74c07113bd796894185a367` | #113 and #183-#194 |
| Firmware can leave HPET enabled but stalled; inherited mappings and PCI enumeration must fail explicitly when repair/traversal is incomplete | `934b4fd8e4e8849d13d774bacfef063870a18d05` and steps 150-151 | #94, #96, and #99 |
| Hosted runtime reuse can leak default-load-context/static state; release builds must reject unresolved host fallbacks | current steps 154-165 | #211 and #212 |
| Embedded PE `RT_MANIFEST` metadata can cross-check a package but cannot replace CLROS's canonical workload manifest | `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` | #163, #164, #177, and #216 |

Full USB/xHCI, terminal engines, Terminal.Gui, PowerShell, NES, and DOOM remain
optional or deferred. SharpOS's current work still does not supply a network
driver, so it does not shorten CLROS's M07 live-network or M10 Kestrel path.
Every adopted case still requires CLROS-owned contracts, tests, and fresh guest
evidence at the boundary it claims.

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

1. Use the recorded SharpOS base/tip anchors; for any adaptation, record the
   exact upstream commit and source path plus the exact local or fetched file
   hash.
2. Record the exact source path, license evidence, changed CLROS paths, and
   adaptation boundary in the implementing issue and `done/stepNN.md`.
3. Keep reference-derived tests independent and include negative cases that
   prevent importing known reference weaknesses.
