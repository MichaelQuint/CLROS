# CLROS Reference Map

## NativeAOT-first implementation source index

The [current roadmap](workload-profiles.md) uses the following exact loci.
Record upstream revision, local raw SHA256, license/notice and adaptation
boundary in the implementing ticket. These sources are guidance, not CLROS
acceptance; do not link projects or prebuilt binaries from references.

| Work | Source | Use and limitation |
| --- | --- | --- |
| ELF loader (M16-004) | `references/SharpOS-main/OS/src/Kernel/Elf/ElfParser.cs`, `ElfLoader.cs` | Selective C# adaptation under recorded SharpOS grant; audit startup ABI and rollback. Historical ELF does not establish Linux compatibility. |
| ELF independent inspection (M16-001/004) | `references/elfsharp-master/ELFSharp/ELF/ELFReader.cs` | Host inspection/test vectors; its managed allocation/stream surface is unsuitable inside the freestanding loader. |
| Linux dispatch (M16-005) | `references/SharpMetal-b29c452f7d6c/src/kernel/Posix/PosixSyscallDispatch.cs` | MIT source/design starting point. Its small dispatch and global buffers are not a complete/thread-safe Linux runtime. |
| Thread wait/wake (M16-008) | `references/expressos-master/Source/ExpressOS.Kernel/Syscalls/Futex.cs` | Concept/tests pending exact provenance. Linux-helper delegation cannot be inherited. |
| Scheduler tests (#183-#194) | `references/ProtonOS-c4f6db236c0c/src/kernel/Threading/Scheduler.cs`, `Thread.cs` | Independently author tests; no AGPL implementation import. Reject guessed stack/module bounds and wrong FS/GS assumptions. |
| Continuing NIC (M07-026) | `references/SharpMetal-b29c452f7d6c/src/servers/drivers/net.virtio/VirtioNetDriver.cs` | Reuse CLROS's accepted driver groundwork first; audit all length/ownership/reset paths. |
| Wire-format oracle (#148-#153) | `references/PacketDotNet-8752306350ee/Test/PacketType/TcpPacketTest.cs` | Independent packet vectors/checks, not a replacement live network stack. |
| Runtime/platform contract (M16) | Official dotnet/runtime `src/coreclr/nativeaot/Runtime/unix/PalUnix.cpp`, `UnixSignals.cpp`, `src/native/libs/System.Native/pal_networking.c`; musl `src/thread/pthread_create.c` | Pin the actual shipping runtime/musl revisions in M16-001. Preserve genuine memory/thread/signal/epoll semantics, including failure paths. |

The former blanket prohibition on studying a POSIX adapter is superseded
only for the restricted M16 Linux ABI. Existing licensing/build firewalls
and exclusions for shells, user administration and broad compatibility remain.

## Current Status

The local `references/` directory contains 39 external project snapshots: the
29 originally supplied archives and ten immutable GitHub snapshots added on
2026-10-06. They are inspection inputs, excluded from `CLROS.slnx`, and cannot
supply completion-gate evidence. The new research and issue crosswalk are
[below](#github-discovery-and-selection-2026-10-06).

All snapshots lack `.git` metadata. The original 29 archive names did not
identify verified revisions and omitted top-level license files; the later
SharpOS/nativeaot-patcher investigations establish specific exceptions. The
ten new directories encode a commit prefix, preserve upstream license files,
and were verified against immutable GitHub trees and archive hashes. See
`docs/license-audit.md` for full commits, license scope and archive provenance.
Missing or incompatible license evidence permits conceptual study and
independently authored tests only, not source import.

Older documentation named `references/nanos-master`,
`references/Cosmos-master`, and
`references/daniilvaino-SharpOS-main`; none of those paths exists in this
workspace. The corresponding originally supplied Cosmos and SharpOS snapshots are
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
| `references/SharpOS-main` | Selective primary, owner-authorized CC0 reference | `OS/`, `std/`, `apps_native/`, `bootasm/`, `docs/`, and `done/` for modern C# NativeAOT boot/runtime, PE apps, PAL, scheduler, storage, and evidence patterns. The owner-supplied permission record and upstream CC0-1.0 license permit selective reuse of SharpOS-authored source. | The local archive's content matches upstream `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` except for its omitted top-level `LICENSE`; still bind adaptations to exact paths and hashes. The October 1 review pinned `dfad516b816d29b971d23ef116d4dbebeedcd25e`; the October 6 research refresh below pins `d5a8ed0df90e2f182379edcf4c4d420c8348cd4c`. SharpOS relies on a separate CoreCLR fork, still has no network driver, and contains separately licensed/GPL material. It is not CLROS architecture or evidence. |
| `references/SharpOS-master` | Test-vector reference | `Kernel/`, `AOT/`, and historical test material for arithmetic, casts, arrays, strings, generics, exceptions, delegates, interfaces, boxing, and cctor behavior. | Historical custom compiler/runtime; inspected CS test headers declare GPL v3 with a Classpath exception. Recreate tests independently and do not copy implementation. |
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

### SharpOS upstream refresh (2026-10-01)

Reviewed upstream `main` at `dfad516b816d29b971d23ef116d4dbebeedcd25e`
(2026-09-30), 65 commits after the previous reviewed revision
`0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`. The fetched checkout is disposable
research under `artifacts/references/SharpOS-20261001`; the supplied reference
archive remains unchanged. See [the pinned comparison](https://github.com/daniilvaino/SharpOS/compare/0c00476fa6d9cfebb6e83d5da34ab8fb1074163b...dfad516b816d29b971d23ef116d4dbebeedcd25e).

| New finding | Pinned-tree source / history | Existing CLROS work and required adaptation |
| --- | --- | --- |
| GC must distinguish an active interrupted instruction from a saved return address, and keep unwinding through native frames that have no GC slot table | `done/step175.md`, `done/step178.md`, `OS/src/Boot/EH/CoffMethodGcInfo.cs`, `OS/src/Boot/EH/CoffGcInfoDecoder.cs` | #82, #83, #89, #193: retain roots below intervening native frames; distinguish missing metadata, native-without-GC-info, and managed frames. Validate against CLROS's pinned .NET 10 ILC. |
| Fragmentation and diagnostic optimization can disguise allocator failures | `done/step178.md`, `std-no-runtime/GC/GcHeap.cs`, `std-no-runtime/GC/GcSweep.cs` | #75, #82, #83: same-segment coalescing, live separators, failed large requests, and trustworthy before/after allocation counters. Do not adopt size buckets without measurements. |
| A 32-bit HPET needs explicit wrap handling and refresh while idle; an older read must not invent a new epoch | `done/step177.md`, `OS/src/Hal/Timer/Hpet.cs`; app time diagnosis in `done/step182.md` | #96, #185, #191: width, wrap, stale samples, restart, idle refresh and deadline overflow tests. Upstream explicitly excludes SMP correctness; CLROS must not expose raw MMIO or latch pointers through its app/PAL ABI. |
| Wait registration must be atomic with sleeping; unloading an app must remove its queued work | `done/step174.md`, `OS/src/Kernel/Threading/` | #188, #191, #194, #212: wake-before-sleep, timeout/cancel races, generation ownership, timer/queue cleanup, and no callback into an unloaded image. Upstream still reports unreclaimed thread stacks. |
| Shared std compile lists drifted across kernel/app tiers; cross-host tools now have a manifest | `done/step184.md`, `std-no-runtime/Std.props`, `Std.Kernel.props`, `Std.App.props`, `toolchain.json` | #163, #177, #229, #236: share reviewed common declarations and record exact tool/runtime provenance. Keep CLROS's explicit EFI source closure and existing `std/no-runtime/shared` layout. |
| More runtime breadth is not a compatibility certificate | `std-no-runtime/Threading.Atomics.cs`, `done/step175.md`, `done/step183.md`, `done/step184.md` | Keep negative coverage for non-atomic fallbacks, strong-reference `ConditionalWeakTable`, stubbed `Enum.IsDefined`, and incomplete unwind handling. CBOR, USB/FIDO, shell and terminal work do not unblock the single-workload network/Kestrel path. |

The new upstream directory is `std-no-runtime/`; historical local paths above
still describe the immutable archive. The current kernel still targets
`net8.0` with ILC `8.0.27`; the .NET 10 SDK used for C# 14 and the separate
hosted runtime `10.0.5` do not make that kernel a .NET 10 NativeAOT port.
The README still reports no network-card driver. The upstream CC0 file does
not replace component-specific provenance. This review ran no SharpOS guest
and promotes no CLROS completion gate.

### SharpOS upstream refresh (2026-10-03)

Reviewed the five commits from `dfad516b816d29b971d23ef116d4dbebeedcd25e`
to `75c045963af300cc48072e12681eb90cd2c60e04`, including steps 185–188:
[pinned comparison](https://github.com/daniilvaino/SharpOS/compare/dfad516b816d29b971d23ef116d4dbebeedcd25e...75c045963af300cc48072e12681eb90cd2c60e04).
The prior 65-commit review above remains historical evidence. The research
clone stayed at its clean `dfad516b` checkout; target files were read through
Git without a build, guest run or source import.

| Current source / upstream report | Existing CLROS destination and posted addendum |
| --- | --- |
| `GcRuntimeExports.cs` now checks reference-array stores and exact element identity before a writable ref; its assignment helpers remain plain stores. Upstream reports callers without preceding bounds checks. | [#79](http://localhost:3000/admin/CLROS/issues/79#issuecomment-14520): derive actual .NET 10 helper/bulk-move contracts and no-write failures; keep later covariance/casts outside the approved POD slot/bulk slice. |
| Step188 reports direct/compare-select interface calls that bypass the resolver for its one/two/three-implementation cases. | [#85](http://localhost:3000/admin/CLROS/issues/85#issuecomment-14523): distinguish optimized call-site success from actual dispatch using same-image evidence; the upstream thresholds are not portable guarantees. |
| `GcHeap.cs` publishes headers inside both allocation critical sections; `KernelGcPreciseWalk.cs` follows roots below app transitions. | [#82](http://localhost:3000/admin/CLROS/issues/82#issuecomment-14525): later header-publication, retry and parked/interrupted-root cases with explicit stack/image authority. Its allocation-forbidden hook only counts, and its collector assumes one CPU. |
| `AppServiceTable.cs` adds tail fields while retaining V3; step185 reports a stale app reading a different tail. | [#164](http://localhost:3000/admin/CLROS/issues/164#issuecomment-14527): size/version/layout and old/new producer-consumer tests; retain POD/opaque `OSHost_*` boundaries and owner-lifetime checks. |

The addenda preserve issue bodies, ownership, states and dependencies. Exact
source lines, hashes and commit links are retained in the accepted research
report under `artifacts/orchestration-20261001/sharpos-refresh-20261003`.
This is source-review/test-vector material: the kernel remains net8, its
separate CoreCLR fork was not reproduced, and per-file dotnet MIT notices are
not overridden by root CC0. Do not import the new formatting implementation
or its `RhpStackProbe` patch to `ret`. Nothing here advances CLROS's .NET 10,
GC, EBS, hosted-runtime or completion-gate evidence.

### SharpOS upstream refresh (2026-10-06)

Reviewed the single new commit [step189, `d5a8ed0`](https://github.com/daniilvaino/SharpOS/commit/d5a8ed0df90e2f182379edcf4c4d420c8348cd4c),
dated October 4, after the October 3 revision above. Its GCInfo decoder fixes
untracked-slot flags and unsigned deltas, and reconstructs fully interruptible
state from recorded chunks. The walker excludes volatile registers in inactive
caller frames. An object-start bitmap resolves interior candidates before any
header read or mark-bit write.

These are future precise-root cases for [#193](http://localhost:3000/admin/CLROS/issues/193#issuecomment-15802)
and app metadata [#176](http://localhost:3000/admin/CLROS/issues/176#issuecomment-15804).
They do not invalidate the accepted bounded explicit-root collector or require
repeating its frozen acceptance run. The appended V3 service-table fields also
reinforce old/new producer compatibility in [#164](http://localhost:3000/admin/CLROS/issues/164#issuecomment-15805).
The region barrier exposes two useful compiler lessons: some emitted stores
bypass helpers, and a frameless helper needs a proven stack/call ABI before
calling a managed failure path. Do not import its deliberate-fault rejection
or allocating preflight.

The kernel still targets net8 with ILC 8.0.27; establish the .NET 10 metadata
and helper contract before adapting any algorithm. Per-file dotnet MIT notices
remain separate from root CC0. This static review imported no code, ran no
upstream tests, changed no completion gate, and supplies no direct firmware
revocation, constructor or dispatch implementation. Native pipes remain outside
the immediate single-workload path.

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

### nativeaot-patcher upstream refresh (2026-10-06)

The requested repository's `main` is pinned at [f910382](https://github.com/valentinbreiz/nativeaot-patcher/commit/f910382228d46e811f907c08c50ad860db7c1a74),
dated September 21. All 888 ordinary files in the supplied snapshot byte-match
[e33ed96](https://github.com/valentinbreiz/nativeaot-patcher/tree/e33ed962dd97d8d9cfd7529d1c94941306a2aad9),
dated July 25; only upstream's top-level `LICENSE` is omitted. The archive also
lacks the two gitlink dependencies. There are 459 newly reachable commits,
including merged branch history. This establishes matching content, not the
archive's extraction date or the latest state of the separately advertised
CosmosOS/Cosmos destination.

| Inspected code | CLROS implementation use |
| --- | --- |
| `src/Cosmos.Kernel.Core/Runtime/ManagedModule.cs`, `RunInitializers` | #84: initialize static storage, discover `EagerCctor` section 205, decode the target-specific signed rel32/native-pointer encoding, and invoke a managed function pointer. This walker already existed in the archive. CLROS must validate the entire table and executable targets against its retained module owner before invocation. It does not supply lazy once/recursion/failure behavior. |
| [Startup-order fix `0e10d09`](https://github.com/valentinbreiz/nativeaot-patcher/commit/0e10d0949b84983ded052fa05812b2c310e0e750), `SortAutoInitialedAssemblies.cs` and `Cosmos.Build.Ilc.targets` | #84: heap/runtime initialization precedes the framework constructor runner, which precedes kernel consumers. Encode prerequisites in CLROS's `BootSequence`; no plug-aware build sorter is needed. |
| [Pinned official `ClassConstructorRunner.cs`](https://github.com/dotnet/runtime/blob/baf4eb1e0b672338897fc20de49daffe5f84569c/src/coreclr/nativeaot/System.Private.CoreLib/src/System/Runtime/CompilerServices/ClassConstructorRunner.cs) | #84: verify the compiler context/call contract against primary .NET source. It clears the method address only on success, caches exceptions and tracks thread/cycle ownership. CLROS's bounded single-thread/status policy must not claim those complete CLR semantics. |
| `GCSegment.cs`, `GarbageCollector.PreciseStack.cs`, `GCHandleManager.cs` | #193: object-start lookup for interior roots and distinct handle kinds are useful later seams. Review pointer units and failed allocations independently; the inspected enumerator adds a byte count to a `GCObject*`, and handle allocation dereferences an unchecked result. These are static concerns, not reproduced failures or reasons to reopen #82/#83. |
| [EFI calling-convention fix `cdafa67`](https://github.com/valentinbreiz/nativeaot-patcher/commit/cdafa67d9515a6e3dae7f3901d8a213e420a67e2), `EfiNative.cs` and `EfiRtc.cs` | #65: distinguish calling convention, mapping authority and service lifetime. Cosmos bridges System V to Microsoft x64 and retains runtime services through Limine mappings. CLROS already targets win-x64 and needs its own all-adapter revocation; do not add the assembly thunk. |

Current source targets net10.0, but its SDK request `10.0.201` with
`latestFeature` and ILC `$(BundledNETCoreAppPackageVersion)` do not establish
CLROS's exact ILC 10.0.8 contract. Its runtime gitlink is
`baf4eb1e0b672338897fc20de49daffe5f84569c`. Patched CoreLib, plugs,
Limine/ELF and native/System V bridges remain separate architecture choices.
The newer ext2/symlink and DNS/socket work is future reference material;
`SocketProtocolSupportPalPlug.cs` still exposes IPv4 only. None provides
CLROS networking or Kestrel evidence.

The archive's MIT text and current BSD-3-Clause text are now recovered; see
[the license record](license-audit.md#nativeaot-patcher-provenance-refresh-2026-10-06).
File-local .NET MIT notices remain applicable. Exact hashes, source links and
the comparison are retained under
`artifacts/orchestration-20261001/nativeaot-patcher-refresh-20261006`.
This was static source research with no reference build, code import or gate
promotion. The next concrete use is #84's real discovery/invocation boundary,
after its existing churn dependency is accepted.

## GitHub discovery and selection (2026-10-06)

The user requested useful active/new C# OS and kernel references plus supporting
foundations, then identified SharpMetal as the desired kind of find. Selection
therefore favors inspectable boot/runtime/driver work, not a Cosmos-based shell,
desktop, README feature list, stars or the word "kernel" in an application name.

"Recent" here means observed repository/commit activity since 2025-10-06;
"new" means created since 2024-10-06. These are search filters, not maintenance
or quality guarantees. Commit dates and substantive examples are recorded
separately from `pushed_at`; a dependency/doc bump is identified as such.

GitHub REST repository search used `language:csharp` throughout the discovery
queries, with topic, UEFI, NativeAOT, virtio, filesystem and parser/network terms.
Twelve initial query families yielded 519 distinct repository results for
metadata triage. Source review focused on the accepted projects and named
near-misses below; 519 is not a count of code-audited projects. Small topic and
specialist result sets were paginated to exhaustion. The exact queries and
coverage returned at retrieval time were:

| Query (all prefixed by `language:csharp`) | Retrieved / reported total |
| --- | --- |
| `topic:operating-system archived:false` | 90 / 90 |
| `topic:osdev archived:false` | 17 / 17 |
| `topic:kernel archived:false -semantic` | 16 / 16 |
| `topic:uefi archived:false` | 20 / 20 |
| `topic:bare-metal archived:false` | 6 / 6 |
| `nativeaot kernel in:name,description,readme archived:false` | 125 / 125 |
| `"operating system" created:>=2024-10-06 archived:false -"semantic"` | 0 / 0 |
| `virtio in:name,description,readme archived:false` | 47 / 47 |
| `filesystem FAT32 in:name,description archived:false` | 5 / 5 |
| `PE parser in:name,description archived:false` | 83 / 83 |
| `network stack in:name,description archived:false` | 86 / 86 |
| `UEFI in:name,description archived:false` | 59 / 59 |

The quoted broad `"operating system"`/creation-date query returned zero and was
not accepted as proof of absence. Supplemental name/description discovery
sampled 100 of 355 results for `operating-system created:>=2024-10-06`, 100 of
1,137 for `kernel created:>=2024-10-06`, and all four new `topic:osdev` results.
The broad kernel results were heavily polluted by AI/application libraries.
Follow-up searches for `nativeaot topic:operating-system`, `UEFI kernel in:readme
created:>=2025-01-01`, `Limine in:readme`, and `bare-metal in:name,description`
returned 1/1, 85/85, 10/10 and 36/36; a newest-first kernel-name pass sampled
100/721. Results overlapped. Forks were separately considered, which surfaced
the maintained LTRData DiscUtils fork. Supplemental exFAT, DiscUtils and packet
searches and primary GitHub web searches completed the foundation discovery.
This is a documented deep sample, not an exhaustive census of GitHub.

Raw API responses, exact URLs/sort/page/incomplete flags, source trees, commit
history, per-file hashes, archives, drafts and read-back receipts are retained
locally in `artifacts/reference-research-20261006`. No search result, repository
instruction file or upstream setup script was executed. Public profile/site
claims were used only to locate possible source, not as proof of functionality.

### Accepted snapshots

All ten are static, ignored reference snapshots. No first-party code or package
dependency was imported and no upstream build/test was run. Full archive hashes
and license files are in [the provenance ledger](license-audit.md#new-github-snapshot-provenance-2026-10-06).

| Snapshot and immutable upstream | Activity and novelty | Use and boundary |
| --- | --- | --- |
| `references/RedPandaOS-3a2e08183847`; [giawa/RedPandaOS@3a2e08183847](https://github.com/giawa/RedPandaOS/tree/3a2e08183847d4d9596fcfe1cd853474313c45b4) | Created 2021-04-18; tip 2025-11-11. Low cadence: November 2025 BGA repair and .NET 6 test update; not a new project. | **Selective/test-vector.** BIOS/x86, custom IL compiler and core library; no NativeAOT or .NET 10 compatibility implied. |
| `references/ProtonOS-c4f6db236c0c`; [ProtonOS/ProtonOS@c4f6db236c0c](https://github.com/ProtonOS/ProtonOS/tree/c4f6db236c0cf8e979500efecd1d4002b34572c2) | Created 2025-12-04; tip 2026-01-27. Created December 2025; January 2026 context-switch and join corrections. | **Conceptual/test-vector.** bflat, custom JIT, native assembly and Linux syscall surface are not the CLROS architecture. Independently author tests; no source import. |
| `references/SharpMetal-b29c452f7d6c`; [Sadik00789/SharpMetal@b29c452f7d6c](https://github.com/Sadik00789/SharpMetal/tree/b29c452f7d6c8d8609d1a007b0b6ae8bdcc84807) | Created 2026-09-05; tip 2026-09-19. Created September 2026; September 19 storage, CI and release changes. | **Selective/test-vector.** NativeAOT comparison only; native stubs, multi-server design and POSIX emulation stay outside CLROS. |
| `references/DefenderOS-fb3b3290ef3d`; [Nobody-Home21/Defender-OS@fb3b3290ef3d](https://github.com/Nobody-Home21/Defender-OS/tree/fb3b3290ef3d79d00206034f64a478d1582f58ca) | Created 2026-07-17; tip 2026-08-15. Created July 17, 2026; August 15 source tip. Specific virtio/MMIO and pre-timer DHCP corrections inspected. | **Selective/test-vector.** Custom .NET 7 NativeAOT/no-stdlib kernel with C/MASM shims and a desktop/security product model. No Cosmos SDK reference in the inspected kernel project; do not import architecture, native code or unverified feature claims. |
| `references/SingNextOS-ad156e965850`; [yuriyyak23/SingNextOS@ad156e965850](https://github.com/yuriyyak23/SingNextOS/tree/ad156e965850b449bcc2593a6098b3c6d4bb27ee) | Created 2026-09-28; tip 2026-10-02. Created September 28, 2026; October 2 revision, with host conformance tests inspected. | **Conceptual/test-vector.** Managed research/conformance model; empty kernel subsystem placeholders are not bare-metal proof. No source import. |
| `references/Inu-8a1ede5246cb`; [ProjectLithos/Inu@8a1ede5246cb](https://github.com/ProjectLithos/Inu/tree/8a1ede5246cbf7420d7627a13ae0dc45d8c6cdf6) | Created 2026-09-06; tip 2026-10-06. Created September 2026; repeated October 6 build revisions, with DNS/TCP codec source inspected. | **License-blocked/test-vector.** Third-party font/FatFs notices do not license the core. Independently author behavioral cases only. |
| `references/Cosmos-gen3-81259dd941a3`; [CosmosOS/Cosmos@81259dd941a3](https://github.com/CosmosOS/Cosmos/tree/81259dd941a39b60226dc6f9db14befd6558e592) | Created 2014-12-04; tip 2026-09-27. Canonical gen3 branch: September 27 .NET runtime 10.0.12 update; pushed_at is later and is not the commit date. | **Selective/test-vector.** Separate pinned canonical snapshot, not a refresh of nativeaot-patcher-main. Do not import plugs, runtime patches or native code. |
| `references/AsmResolver-0715000c1b3a`; [Washi1337/AsmResolver@0715000c1b3a](https://github.com/Washi1337/AsmResolver/tree/0715000c1b3a5104531d5ff189a14ebef0a9d5ac) | Created 2013-02-20; tip 2026-08-29. August 29 contribution-doc update; July release/dependency activity. Recent repository maintenance is not a claim that the inspected parser just changed. | **Host/test-vector.** Host PE tooling and synthetic fixture ideas only; allocating object model is unsuitable for freestanding parsers. |
| `references/PacketDotNet-8752306350ee`; [dotpcap/packetnet@8752306350ee](https://github.com/dotpcap/packetnet/tree/8752306350ee8bfb8e88f8cf485110a11dddfe53) | Created 2015-05-27; tip 2025-12-22. November 12, 2025 .NET 10 support; December 22 test-adapter update. | **Host/test-vector.** Packet decoder, not a TCP stack. Preserve MPL file-level terms for any future reuse; no bundled captures imported. |
| `references/DiscUtils-96e8f6bc4d8d`; [LTRData/DiscUtils@96e8f6bc4d8d](https://github.com/LTRData/DiscUtils/tree/96e8f6bc4d8daa0da84e07556f5623c1e79a34a2) | Created 2020-03-04; tip 2026-09-22. Maintained fork, September 22, 2026 version and tool updates; original DiscUtils default-branch commit is from 2021. | **Host/test-vector.** Stream-based storage/image library is an oracle candidate, not a kernel filesystem. exFAT and VHD code require independent bounded adaptations. |

ProtonOS is not classified as a Cosmos derivative: the inspected README,
Makefile/build inputs and gitmodules identify its bflat/runtime forks, while
the tree contains its own JIT, scheduler and driver framework. Its source
license and native ABI still prevent a direct architectural transplant.

The canonical Cosmos Gen3 snapshot is retained for a concrete delta, not counted
as another independent OS design. Compared with `nativeaot-patcher-main`,
`UartMessageParserTests.cs` adds incomplete-suite/missing-result cases and its
parser changes accordingly. `Virtqueue.cs` has largely the same relevant queue
logic (the inspected differences are license header/class visibility); those
queue observations are newly documented audit cases, not new upstream features.
The old snapshots remain untouched.

### Rejected or deferred candidates

| Candidate | Inspected evidence and decision |
| --- | --- |
| `shirakibaka/Novellium` | `Novellium.csproj` directly consumes Cosmos.Sdk/Kernel 3.0.88; README emphasizes shell, jobs, syslog and CLI utilities. No incremental CLROS core implementation was established. Excluded. |
| `tails1154/TailsOS`, `yggdrasil-au/yindows`, `valentinbreiz/GlieseOS` | Repository metadata explicitly identifies Cosmos-based projects. Deferred without a full code audit because no unique needed core contribution was established. This is not a judgment that every derivative lacks value. |
| `xharp-cs/xharp` at `86a89a3aca28a64c673cdfde5a8b8b8b0ca2fd93` | Real small .NET 10 NativeAOT/Limine kernel candidate; tree/build/project inspection shows early corelib, framebuffer/font and native bridges. No additional bounded driver/runtime contribution beyond existing zero-runtime references established in this pass. Deferred, not a Cosmos rejection. |
| `RPDevJesco/tos-x86` at `a9a6c2902813063c96fbf260fdfacbbc6c2efb20` | Sixteen-file bflat/native boot demonstration with simulated task selection; no root license. Redundant early-boot concept, not an implementation source. |
| `meow-sci/gatOS` | README/project inspection identifies a game integration that runs Alpine Linux in a VM, not a C# kernel. Excluded. |
| `s6ft256/PhantomOS` | SentinelGuard architecture/desktop security project with hosted net8 service projects; source inspected at project/README level. No freestanding driver value established. Excluded. |
| KatanaOS website | Site points to the NinjahZ profile, not an identified public source repository. No immutable source snapshot could be established from that lead; hardware-proof claims are not adopted. |
| `nifanfa/BootTo.NET` at `57622afd2f4ab2dd38e737af2fab850d59ab489a` | Recent and substantial UEFI application/runtime work, but relies on firmware services and an MSIL-to-LLVM toolchain. Metadata/tree/README inspection only; large bundled binaries/assets and unresolved project license. No snapshot added in this pass. Revisit only for a specific pre-EBS contract. |
| `DiscUtils/DiscUtils` at `59d7cadab839c6d8dfcf52f8be5efe6d2ced190f` | Default-branch tip is from 2021 despite later push metadata. The maintained LTRData fork provides the useful current storage/image surface. |
| `picrap/ExFat` at `5827117d5f742ac7b6c665862f7d2facaa11664c` | 2018 MIT ancestor already represented by the LTRData exFAT subtree; not added as a duplicate. |
| `dot-sys/VortexFAT` | Metadata/tree indicate an active recovery GUI including exFAT. Implementation was not audited; deferred because DiscUtils provides a better-scoped first reference for bounded parsing. |
| Existing SharpOS, nativeaot-patcher, MOSA, MOOS, zerosharp | Preserve existing reviews and snapshots. No blanket reimport, duplicate issue or new readiness claim. |

### Existing issue crosswalk

Each issue receives a pinned-file comment containing the useful observation,
adaptation limits, exact issue-owned paths and independent acceptance cases.
Comments preserve managed bodies, labels, dependencies, assignment and state.
Several issue-owned paths are plans; listing them does not claim implementation.

| Issue | Reference purpose | Files to analyze |
| --- | --- | --- |
| [#89](http://localhost:3000/admin/CLROS/issues/89) | AsmResolver: unwind field layout and padding rejection cases | [`references/AsmResolver-0715000c1b3a/src/AsmResolver.PE/Exceptions/X64UnwindInfo.cs`](https://github.com/Washi1337/AsmResolver/blob/0715000c1b3a5104531d5ff189a14ebef0a9d5ac/src/AsmResolver.PE/Exceptions/X64UnwindInfo.cs)<br>[`references/AsmResolver-0715000c1b3a/src/AsmResolver.PE/Exceptions/X64RuntimeFunction.cs`](https://github.com/Washi1337/AsmResolver/blob/0715000c1b3a5104531d5ff189a14ebef0a9d5ac/src/AsmResolver.PE/Exceptions/X64RuntimeFunction.cs) |
| [#99](http://localhost:3000/admin/CLROS/issues/99) | RedPandaOS: PCI scan completeness and multifunction cases | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Devices/PCI.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Devices/PCI.cs) |
| [#101](http://localhost:3000/admin/CLROS/issues/101) | RedPandaOS and SharpMetal: BAR pairing, extents and restoration | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Devices/PCI.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Devices/PCI.cs)<br>[`references/SharpMetal-b29c452f7d6c/src/servers/drivers/net.virtio/VirtioNetDriver.cs`](https://github.com/Sadik00789/SharpMetal/blob/b29c452f7d6c8d8609d1a007b0b6ae8bdcc84807/src/servers/drivers/net.virtio/VirtioNetDriver.cs) |
| [#105](http://localhost:3000/admin/CLROS/issues/105) | DefenderOS and SharpMetal: capability parser trust boundaries | [`references/DefenderOS-fb3b3290ef3d/src/Defender.Kernel/LinuxDriverPortFoundation.cs`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/src/Defender.Kernel/LinuxDriverPortFoundation.cs)<br>[`references/DefenderOS-fb3b3290ef3d/src/Defender.Kernel/ModernStorage.cs`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/src/Defender.Kernel/ModernStorage.cs)<br>[`references/DefenderOS-fb3b3290ef3d/FIX_V0509_VIRTIO_CAPABILITY_LAYOUT.md`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/FIX_V0509_VIRTIO_CAPABILITY_LAYOUT.md)<br>[`references/SharpMetal-b29c452f7d6c/src/servers/drivers/net.virtio/VirtioNetDriver.cs`](https://github.com/Sadik00789/SharpMetal/blob/b29c452f7d6c8d8609d1a007b0b6ae8bdcc84807/src/servers/drivers/net.virtio/VirtioNetDriver.cs) |
| [#106](http://localhost:3000/admin/CLROS/issues/106) | DefenderOS: high physical BAR mapped to a distinct virtual window | [`references/DefenderOS-fb3b3290ef3d/src/Defender.Kernel/DefenderKernel.cs`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/src/Defender.Kernel/DefenderKernel.cs)<br>[`references/DefenderOS-fb3b3290ef3d/src/Defender.Kernel/LinuxDriverPortFoundation.cs`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/src/Defender.Kernel/LinuxDriverPortFoundation.cs)<br>[`references/DefenderOS-fb3b3290ef3d/src/Defender.Kernel/VirtioNetRuntime.cs`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/src/Defender.Kernel/VirtioNetRuntime.cs)<br>[`references/DefenderOS-fb3b3290ef3d/FIX_V0510_HIGH_PCI_MMIO_WINDOW.md`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/FIX_V0510_HIGH_PCI_MMIO_WINDOW.md) |
| [#107](http://localhost:3000/admin/CLROS/issues/107) | ProtonOS: observable virtio feature negotiation | [`references/ProtonOS-c4f6db236c0c/src/drivers/shared/virtio/VirtioDevice.cs`](https://github.com/ProtonOS/ProtonOS/blob/c4f6db236c0cf8e979500efecd1d4002b34572c2/src/drivers/shared/virtio/VirtioDevice.cs) |
| [#109](http://localhost:3000/admin/CLROS/issues/109) | Cosmos Gen3: queue-width and zero-size hazards | [`references/Cosmos-gen3-81259dd941a3/src/Cosmos.Kernel.HAL/Devices/Virtio/Virtqueue.cs`](https://github.com/CosmosOS/Cosmos/blob/81259dd941a39b60226dc6f9db14befd6558e592/src/Cosmos.Kernel.HAL/Devices/Virtio/Virtqueue.cs) |
| [#110](http://localhost:3000/admin/CLROS/issues/110) | Cosmos Gen3: descriptor ownership and double free | [`references/Cosmos-gen3-81259dd941a3/src/Cosmos.Kernel.HAL/Devices/Virtio/Virtqueue.cs`](https://github.com/CosmosOS/Cosmos/blob/81259dd941a39b60226dc6f9db14befd6558e592/src/Cosmos.Kernel.HAL/Devices/Virtio/Virtqueue.cs) |
| [#127](http://localhost:3000/admin/CLROS/issues/127) | RedPandaOS and DiscUtils: bounded FAT chain traversal | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Filesystem/FAT32.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Filesystem/FAT32.cs)<br>[`references/DiscUtils-96e8f6bc4d8d/Tests/LibraryTests/Fat/FatFileSystemTest.cs`](https://github.com/LTRData/DiscUtils/blob/96e8f6bc4d8daa0da84e07556f5623c1e79a34a2/Tests/LibraryTests/Fat/FatFileSystemTest.cs) |
| [#128](http://localhost:3000/admin/CLROS/issues/128) | RedPandaOS and DiscUtils: directory boundaries and VFAT sequences | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Filesystem/FAT32.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Filesystem/FAT32.cs)<br>[`references/DiscUtils-96e8f6bc4d8d/Tests/LibraryTests/Fat/FatFileNameTest.cs`](https://github.com/LTRData/DiscUtils/blob/96e8f6bc4d8daa0da84e07556f5623c1e79a34a2/Tests/LibraryTests/Fat/FatFileNameTest.cs)<br>[`references/DiscUtils-96e8f6bc4d8d/Tests/LibraryTests/Fat/FreeDirectoryEntryTableTest.cs`](https://github.com/LTRData/DiscUtils/blob/96e8f6bc4d8daa0da84e07556f5623c1e79a34a2/Tests/LibraryTests/Fat/FreeDirectoryEntryTableTest.cs) |
| [#146](http://localhost:3000/admin/CLROS/issues/146) | ProtonOS: DHCP renewal/expiry arithmetic and state cases | [`references/ProtonOS-c4f6db236c0c/src/ddk/Network/Stack/DhcpClient.cs`](https://github.com/ProtonOS/ProtonOS/blob/c4f6db236c0cf8e979500efecd1d4002b34572c2/src/ddk/Network/Stack/DhcpClient.cs)<br>[`references/DefenderOS-fb3b3290ef3d/src/Defender.Kernel/NetworkStack.cs`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/src/Defender.Kernel/NetworkStack.cs)<br>[`references/DefenderOS-fb3b3290ef3d/FIX_V059_BOOT_TIME_DHCP_DEADLOCK.md`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/FIX_V059_BOOT_TIME_DHCP_DEADLOCK.md)<br>[`references/DefenderOS-fb3b3290ef3d/build/Test-v059BootNetworkDeferral.ps1`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/build/Test-v059BootNetworkDeferral.ps1) |
| [#156](http://localhost:3000/admin/CLROS/issues/156) | Inu: DNS transaction binding and hostile name cases | [`references/Inu-8a1ede5246cb/SDK/src/Inu.Kernel.Networking/KernelDnsCodec.cs`](https://github.com/ProjectLithos/Inu/blob/8a1ede5246cbf7420d7627a13ae0dc45d8c6cdf6/SDK/src/Inu.Kernel.Networking/KernelDnsCodec.cs) |
| [#160](http://localhost:3000/admin/CLROS/issues/160) | PacketDotNet: independent wire-format corpus and checksum oracles | [`references/PacketDotNet-8752306350ee/Test/PacketType/TcpPacketTest.cs`](https://github.com/dotpcap/packetnet/blob/8752306350ee8bfb8e88f8cf485110a11dddfe53/Test/PacketType/TcpPacketTest.cs)<br>[`references/PacketDotNet-8752306350ee/Test/PacketType/IPv4PacketTest.cs`](https://github.com/dotpcap/packetnet/blob/8752306350ee8bfb8e88f8cf485110a11dddfe53/Test/PacketType/IPv4PacketTest.cs)<br>[`references/PacketDotNet-8752306350ee/PacketDotNet/Utils/ChecksumUtils.cs`](https://github.com/dotpcap/packetnet/blob/8752306350ee8bfb8e88f8cf485110a11dddfe53/PacketDotNet/Utils/ChecksumUtils.cs)<br>[`references/PacketDotNet-8752306350ee/Test/Misc/ChecksumUtilsTest.cs`](https://github.com/dotpcap/packetnet/blob/8752306350ee8bfb8e88f8cf485110a11dddfe53/Test/Misc/ChecksumUtilsTest.cs) |
| [#172](http://localhost:3000/admin/CLROS/issues/172) | RedPandaOS and AsmResolver: PE32+ and file/mapped address domains | [`references/RedPandaOS-3a2e08183847/PELoader/PortableExecutableFile.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/PELoader/PortableExecutableFile.cs)<br>[`references/RedPandaOS-3a2e08183847/PELoader/VirtualMemory.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/PELoader/VirtualMemory.cs)<br>[`references/AsmResolver-0715000c1b3a/test/AsmResolver.PE.File.Tests/PEFileTest.cs`](https://github.com/Washi1337/AsmResolver/blob/0715000c1b3a5104531d5ff189a14ebef0a9d5ac/test/AsmResolver.PE.File.Tests/PEFileTest.cs) |
| [#179](http://localhost:3000/admin/CLROS/issues/179) | PE negative corpus: range checks and relocation interpretation | [`references/RedPandaOS-3a2e08183847/PELoader/VirtualMemory.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/PELoader/VirtualMemory.cs)<br>[`references/RedPandaOS-3a2e08183847/PELoader/PortableExecutableFile.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/PELoader/PortableExecutableFile.cs)<br>[`references/AsmResolver-0715000c1b3a/test/AsmResolver.PE.Tests/Relocations/BaseRelocationTest.cs`](https://github.com/Washi1337/AsmResolver/blob/0715000c1b3a5104531d5ff189a14ebef0a9d5ac/test/AsmResolver.PE.Tests/Relocations/BaseRelocationTest.cs)<br>[`references/AsmResolver-0715000c1b3a/test/AsmResolver.PE.File.Tests/PEFileTest.cs`](https://github.com/Washi1337/AsmResolver/blob/0715000c1b3a5104531d5ff189a14ebef0a9d5ac/test/AsmResolver.PE.File.Tests/PEFileTest.cs) |
| [#183](http://localhost:3000/admin/CLROS/issues/183) | RedPandaOS versus ProtonOS: explicit context-state ownership | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Scheduler.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Scheduler.cs)<br>[`references/ProtonOS-c4f6db236c0c/src/kernel/Threading/Scheduler.cs`](https://github.com/ProtonOS/ProtonOS/blob/c4f6db236c0cf8e979500efecd1d4002b34572c2/src/kernel/Threading/Scheduler.cs)<br>[`references/ProtonOS-c4f6db236c0c/src/kernel/Threading/Thread.cs`](https://github.com/ProtonOS/ProtonOS/blob/c4f6db236c0cf8e979500efecd1d4002b34572c2/src/kernel/Threading/Thread.cs) |
| [#186](http://localhost:3000/admin/CLROS/issues/186) | RedPandaOS and ProtonOS: interrupt-mask and switch ordering | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Runtime/InterruptDisabler.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Runtime/InterruptDisabler.cs)<br>[`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Scheduler.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Scheduler.cs)<br>[`references/ProtonOS-c4f6db236c0c/src/kernel/Threading/Scheduler.cs`](https://github.com/ProtonOS/ProtonOS/blob/c4f6db236c0cf8e979500efecd1d4002b34572c2/src/kernel/Threading/Scheduler.cs) |
| [#188](http://localhost:3000/admin/CLROS/issues/188) | ProtonOS and RedPandaOS: exit/join and deferred reclamation | [`references/ProtonOS-c4f6db236c0c/src/kernel/Threading/Scheduler.cs`](https://github.com/ProtonOS/ProtonOS/blob/c4f6db236c0cf8e979500efecd1d4002b34572c2/src/kernel/Threading/Scheduler.cs)<br>[`references/ProtonOS-c4f6db236c0c/src/kernel/PAL/Thread.cs`](https://github.com/ProtonOS/ProtonOS/blob/c4f6db236c0cf8e979500efecd1d4002b34572c2/src/kernel/PAL/Thread.cs)<br>[`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Scheduler.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Scheduler.cs) |
| [#236](http://localhost:3000/admin/CLROS/issues/236) | SharpMetal and Cosmos Gen3: negative smoke-harness cases | [`references/SharpMetal-b29c452f7d6c/build/scripts/Test-Harness.py`](https://github.com/Sadik00789/SharpMetal/blob/b29c452f7d6c8d8609d1a007b0b6ae8bdcc84807/build/scripts/Test-Harness.py)<br>[`references/Cosmos-gen3-81259dd941a3/tests/Cosmos.Tests.Patcher/UartMessageParserTests.cs`](https://github.com/CosmosOS/Cosmos/blob/81259dd941a39b60226dc6f9db14befd6558e592/tests/Cosmos.Tests.Patcher/UartMessageParserTests.cs)<br>[`references/Cosmos-gen3-81259dd941a3/tests/Cosmos.TestRunner.Engine/Protocol/UartMessageParser.cs`](https://github.com/CosmosOS/Cosmos/blob/81259dd941a39b60226dc6f9db14befd6558e592/tests/Cosmos.TestRunner.Engine/Protocol/UartMessageParser.cs) |
| [#243](http://localhost:3000/admin/CLROS/issues/243) | RedPandaOS: allocator and pool state must survive rejection | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Memory/BumpHeap.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Memory/BumpHeap.cs)<br>[`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Memory/SplitBumpHeap.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Memory/SplitBumpHeap.cs)<br>[`references/RedPandaOS-3a2e08183847/Runtime/Memory/GCHeap.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/Runtime/Memory/GCHeap.cs)<br>[`references/RedPandaOS-3a2e08183847/Runtime/Collections/ObjectPool.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/Runtime/Collections/ObjectPool.cs) |
| [#245](http://localhost:3000/admin/CLROS/issues/245) | DiscUtils: fixed VHD construction and independent artifact checks | [`references/DiscUtils-96e8f6bc4d8d/Tests/LibraryTests/Vhd/DiskBuilderTest.cs`](https://github.com/LTRData/DiscUtils/blob/96e8f6bc4d8daa0da84e07556f5623c1e79a34a2/Tests/LibraryTests/Vhd/DiskBuilderTest.cs)<br>[`references/DiscUtils-96e8f6bc4d8d/Library/DiscUtils.Vhd/DiskBuilder.cs`](https://github.com/LTRData/DiscUtils/blob/96e8f6bc4d8daa0da84e07556f5623c1e79a34a2/Library/DiscUtils.Vhd/DiskBuilder.cs) |
| [#257](http://localhost:3000/admin/CLROS/issues/257) | SharpMetal: NVMe queue initialization, DMA and completion audit | [`references/SharpMetal-b29c452f7d6c/src/servers/drivers/storage.nvme/NvmeDriver.cs`](https://github.com/Sadik00789/SharpMetal/blob/b29c452f7d6c8d8609d1a007b0b6ae8bdcc84807/src/servers/drivers/storage.nvme/NvmeDriver.cs) |
| [#290](http://localhost:3000/admin/CLROS/issues/290) | SingNextOS: owner/generation checks for private process handles | [`references/SingNextOS-ad156e965850/tests/SingPlus.Tests/Capabilities/CapabilityAuthorityTests.cs`](https://github.com/yuriyyak23/SingNextOS/blob/ad156e965850b449bcc2593a6098b3c6d4bb27ee/tests/SingPlus.Tests/Capabilities/CapabilityAuthorityTests.cs)<br>[`references/SingNextOS-ad156e965850/tests/SingPlus.Tests/Ownership/RegionUseTests.cs`](https://github.com/yuriyyak23/SingNextOS/blob/ad156e965850b449bcc2593a6098b3c6d4bb27ee/tests/SingPlus.Tests/Ownership/RegionUseTests.cs) |
| [#291](http://localhost:3000/admin/CLROS/issues/291) | SingNextOS: resource budgets and failed teardown accounting | [`references/SingNextOS-ad156e965850/tests/SingPlus.Tests/Runtime/Phase05ResourceBudgetTests.cs`](https://github.com/yuriyyak23/SingNextOS/blob/ad156e965850b449bcc2593a6098b3c6d4bb27ee/tests/SingPlus.Tests/Runtime/Phase05ResourceBudgetTests.cs)<br>[`references/SingNextOS-ad156e965850/tests/SingPlus.Tests/Ownership/RegionUseTests.cs`](https://github.com/yuriyyak23/SingNextOS/blob/ad156e965850b449bcc2593a6098b3c6d4bb27ee/tests/SingPlus.Tests/Ownership/RegionUseTests.cs) |
| [#309](http://localhost:3000/admin/CLROS/issues/309) | RedPandaOS and SharpMetal: progress sources and timeout recovery | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Devices/PATA.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Devices/PATA.cs)<br>[`references/SharpMetal-b29c452f7d6c/src/servers/drivers/storage.nvme/NvmeDriver.cs`](https://github.com/Sadik00789/SharpMetal/blob/b29c452f7d6c8d8609d1a007b0b6ae8bdcc84807/src/servers/drivers/storage.nvme/NvmeDriver.cs)<br>[`references/DefenderOS-fb3b3290ef3d/src/Defender.Kernel/VirtioNetRuntime.cs`](https://github.com/Nobody-Home21/Defender-OS/blob/fb3b3290ef3d79d00206034f64a478d1582f58ca/src/Defender.Kernel/VirtioNetRuntime.cs) |
| [#468](http://localhost:3000/admin/CLROS/issues/468) | RedPandaOS: distinguish data writes from durable FAT metadata | [`references/RedPandaOS-3a2e08183847/RedPandaOS/Kernel/Filesystem/FAT32.cs`](https://github.com/giawa/RedPandaOS/blob/3a2e08183847d4d9596fcfe1cd853474313c45b4/RedPandaOS/Kernel/Filesystem/FAT32.cs) |

### Optional future work

Only two narrow gaps were selected: a host-only exFAT boot-region parser and a
host-only owned framebuffer/glyph surface. Their bodies state exact source
paths, blocked dependencies, acceptance cases, commands, stable markers and
deferred work. They have no milestone assignment and do not extend current
M05/M06 exit criteria or precede the main kernel/runtime path. Created as [#568](http://localhost:3000/admin/CLROS/issues/568)
(exFAT) and [#569](http://localhost:3000/admin/CLROS/issues/569)
(framebuffer), both open with `agent:blocked`/`priority:p3`. Read-back receipts
are recorded in [step1049](../done/step1049.md).

RedPandaOS has FAT32, VGA and Bochs BGA source; **exFAT is only a roadmap item**
at the pinned revision. DiscUtils is the actual exFAT implementation reference.
Graphics does not imply a GUI, default shell, remote administration or cloud GPU
support. Detailed static findings are in [the comparison](fork-diff.md#redpandaos-deep-static-review-2026-10-06).

## Tests

Documentation-only changes run:

```powershell
$directories = Get-ChildItem .\references -Directory
if ($directories.Count -ne 39) { throw "reference inventory changed" }
Test-Path .\references\SharpOS-main\done\step143.md
Test-Path .\references\nativeaot-patcher-main\src\Cosmos.Kernel.HAL\Devices\Virtio
dotnet run --project tools/OS.Tooling -- repo check
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
