# CLROS Reference Architecture and Implementation Comparisons

## Current Status

CLROS is a separate project. Its product, repository, and solution identity is
`CLROS`/`CLROS.slnx`; its existing assemblies, namespaces, source paths, and
host ABI remain `OS.*`, `src/OS`, `src/OS.Kernel`, and `OSHost_*`.

The primary modern reference is the local
`references/SharpOS-main` snapshot. The former documented path
`references/daniilvaino-SharpOS-main` does not exist. The snapshot has no
`.git` metadata, but a complete content comparison identifies it as upstream
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`, with the sole difference that
the archive omits upstream's top-level `LICENSE`. Its README links to a separate
`daniilvaino/dotnet-runtime-sharpos` CoreCLR fork, but that fork is not present
under `references/`.

The project owner supplied a Telegram permission record identifying
`daniilvaino/SharpOS` as CC0, explicitly permitting copying, and proposing
direct consolidation with CLROS. Both the identified archive commit and the
August-reviewed upstream commit
`0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` carry a top-level
CC0-1.0 license. SharpOS-authored source is therefore eligible for selective
adaptation. This does not relicense vendor code, the GPL ManagedDoom fixture,
WAD/media assets, or the absent CoreCLR fork; exact source paths, immutable
commits, local/fetched hashes, and adaptation boundaries remain mandatory.

The local archive's latest numbered milestone file is `done/step143.md`. The file does
not embed a trustworthy source date, and the archive's filesystem timestamps
identify only this local extraction, not the upstream revision. It reports
playable Managed DOOM on bare metal with GOP output, PS/2 keyboard input, HPET
timing, and exit code 42. `done/step076.md` reports a gated physical
`ExitBootServices` experiment with owned UART/framebuffer/PS/2 output after the
call. These are inspectable historical claims from another project, not CLROS
boot or completion evidence.

The August-reviewed upstream `main` at `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`
continues through step 165 and is 34 commits ahead of the archive. The reviewed
comparison is
`https://github.com/daniilvaino/SharpOS/compare/b80ee3a4175e1522d1a12ba7fe371a49da3207f5...0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`.
Those newer files remain upstream research inputs; they were not copied into
the immutable local snapshot and are not CLROS evidence.

### SharpOS evidence classes

| Local reference evidence | Class | What it can support | Why it is not reproducible CLROS proof |
| --- | --- | --- | --- |
| `references/SharpOS-main/OS/src/**`, `bootasm/**`, and numbered `done/stepNNN.md` files | Inspectable source and narrative evidence | Architecture review, bounded CC0 source adaptations, negative tests, and historical sequencing ideas | Owner permission and upstream CC0 clear SharpOS-authored reuse, and content comparison identifies archive commit `b80ee3a...`; the archive itself still lacks Git metadata and targets a different runtime/build boundary, so bind adaptations to exact paths and hashes |
| `references/SharpOS-main/media/doom.gif` and `media/doom_small.gif` | Visual historical evidence | Corroborates that the supplied snapshot was presented as running a DOOM demonstration | A GIF does not identify the exact kernel, app, WAD, CoreCLR/runtime, toolchain, command, log, or source revision that produced it |
| `references/SharpOS-main/media/pwsh.png` | Visual historical evidence | Corroborates the snapshot's reported hosted PowerShell milestone | The linked modified CoreCLR fork, immutable runtime revision, patch set, build output, and reproduction commands are absent |
| `references/SharpOS-main/media/screenshot.png` | Visual historical evidence | Shows the reference's historical console/launcher presentation | It is not a machine-readable boot log and carries no artifact or source hash |
| `references/SharpOS-main/apps_native/GPL_AHEAD_WARNING_DOOM_managed/screenshots/*.png` | Fixture screenshots and license-sensitive historical evidence | Useful only for understanding the isolated ManagedDoom fixture and its expected visual output | The screenshots do not grant WAD rights, do not prove the app ran on this snapshot, and do not permit GPL code to enter CLROS kernel/std source |
| Missing external `daniilvaino/dotnet-runtime-sharpos` tree and missing WAD inputs | Missing reproducibility inputs | Establishes an explicit evidence gap | Neither the reported PowerShell runtime nor DOOM asset closure can be rebuilt from the supplied snapshot alone |

CLROS may cite these files as historical engineering context. It must not use
them to pass a completion gate, and it must independently rebuild any analogous
workload from an identified source revision with compatible licensing,
artifact hashes, machine-readable logs, and the applicable live guest evidence.

## Baseline Comparison

| Area | CLROS | `references/SharpOS-main` | Decision |
| --- | --- | --- | --- |
| Product goal | Minimal, single-workload, cloud-first appliance for .NET 10 applications; no users, SSH, or remote administration by default. | General experimental C# OS with kernel-AOT, PE-app, and CoreCLR-hosted tiers plus interactive launcher/demo work. | Preserve the CLROS appliance and security model. |
| Build identity | `CLROS.slnx`; `OS.*` technical names retained. | `OS/OS.csproj` and SharpOS product naming. | Do not rename CLROS or wholesale import the SharpOS tree. |
| Target framework | .NET 10 and C# 14 for normal managed projects. | Kernel and freestanding app properties target `net8.0` with C# 14. | Treat all runtime layouts/helpers as version-sensitive. |
| Firmware path | EFI marker path, copied/normalized memory-map contracts, ACPI provenance, and host-tested readiness; no real first-party EBS transition. | Gated EBS experiment and a broader post-EBS substrate are reported. | Adapt ordering and failure tests only after CLROS owns stack, serial, reservations, paging, and firmware data. |
| Boot stack/memory | Boot-region and free-list readiness exists; no live mutable allocator/paging transition. | Contains owned stack switch, physical allocator, pager, heap, and GC work. | Reimplement in bounded slices using explicit physical/virtual/device address types and W^X. |
| Diagnostics | Host-tested plans/formatters for COM1, stubs, descriptor tables, faults, and panic; no live invocation or table loads. | Reports own UART, generated x64 stubs, IDT/fault paths, and post-EBS logs. | Reuse the phase order and test ideas, not firmware gates or permanent executable-writable allocation. |
| NativeAOT runtime | Useful no-runtime helpers and helper-gap/readiness contracts; GC, dispatch, EH, cctors, thread statics, and full metadata handling remain incomplete. | Reports a non-moving GC, exceptions, interface/delegate dispatch, cctors, std, and PE app support. | Validate each .NET 10 helper independently before replacing any fail-fast stub. |
| Platform/storage | ACPI/PCI/virtio/GPT/FAT/package code is predominantly host-tested parser/planner evidence. | Reports ACPI/HPET/PCI/AHCI/read-only FAT and app loading. | Prefer CLROS's virtio-blk cloud path; reuse FAT/PE edge cases and state-machine ideas. |
| Networking | Frame, checksum, ARP/IP/UDP/DHCP/DNS/TCP/HTTP contracts and planners; no live NIC RX/TX. | Snapshot documentation says its network work is not complete. | Do not infer networking from SharpOS's hosted/app claims; implement live virtio-net first. |
| Native applications | Manifest/package/mapping/invocation/lifecycle readiness only; no live entry invocation. | Reports NativeAOT PE apps, a launcher, and playable isolated GPL DOOM. | Define a versioned POD app ABI with opaque handles; never copy raw framebuffer/MMIO pointer handoff. |
| Hosted .NET | `OSHost_*` contracts and host mocks only; no exported live PAL or hosted runtime. | README reports hosted PowerShell through an external modified CoreCLR; local build inputs for that fork are absent. | Use a separately versioned .NET 10 CoreCLR fork and keep its C/C++ outside first-party CLROS source. |
| Kestrel/cloud | Mandatory future Minimal API milestone; no Kestrel, AWS, or Azure evidence. | No Kestrel or provider driver/proof path in the supplied snapshot. | QEMU/libvirt proof precedes independent AWS Nitro and Azure VM evidence. |
| Evidence | CLROS gates require first-party artifacts, logs, hashes, and stable markers. | README, media, probe reports, and 143-step history belong to the reference. | Never change a CLROS gate from reference evidence. |

## High-Value Crosswalk

### Upstream steps 144-165 reviewed on 2026-08-25

The post-archive delta strengthens CLROS's regression catalog without changing
its implementation order:

- Variant interface dispatch fixes at
  `25dafb69c43622fc5786a894ea63492c39745b29` and
  `c675b268f0901767a74c07113bd796894185a367` require compatible-interface and
  slot-owner tests in M04-016 (#85).
- Application GC/array/root work at
  `c8f814b0caee22645198ea605e3b4eaabb155fd5` and mapped-image/page release at
  `f2f21085a3f78d452052382d978f1981aa479b64` reinforce the existing M04 and
  app-loader ownership/lifecycle issues; they do not justify a duplicate GC or
  loader issue.
- The hardware-context fix at
  `00e99fb23b0e44223101285856d2801b1eff043a` and the current SIMD/unwind work
  at `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` add image-local unwind-base,
  MXCSR/XMM, and `UWOP_SAVE_XMM128`/FAR cases to #89, #91, and #183.
- Scheduler commits `caa7713f42c56137be08fbcea30e2008e0c57a2a`,
  `b83cbf36ec63d444063c739b7b562980e32a8959`,
  `d08a170d10cb43b67ac0cd23a6a52cc4dd2b5684`,
  `6e4d6a67940fafcba163a3abdaab5ea01d886d48`,
  `491730dc46e3316171d745c33d25cd8db408b84c`, and
  `c675b268f0901767a74c07113bd796894185a367` expose parked-root,
  interrupt-FP/SIMD, atomic-publication, preempted-allocation, real-CAS/lock,
  wait, and task cases for #113 and #183-#194. SharpOS's scheduler is
  single-core and is not a drop-in CLROS scheduler.
- Step-149 commit `934b4fd8e4e8849d13d774bacfef063870a18d05`
  and steps 150-151 add a bounded stale-enabled HPET restart, inherited
  cache-mapping repair, and explicit PCI truncation cases for #94, #96, and
  #99.
- Repeated hosted-runtime/default-load-context state and release-build fallback
  behavior become required negative cases for #211 and #212.
- Embedded PE `RT_MANIFEST` metadata may cross-check #163/#164/#177/#216 but
  must never replace CLROS's canonical package/workload manifest or weaken its
  defaults.

Full USB/xHCI and interactive terminal/PowerShell/Terminal.Gui work are useful
runtime breadth demonstrations, not prerequisites for the single-workload
Minimal API appliance. SharpOS still has no network driver, so the live
virtio-net/TCP/Kestrel prerequisites remain CLROS-owned.

### Upstream steps 166-184 reviewed on 2026-10-01

The revision reviewed on 2026-10-01 was
`dfad516b816d29b971d23ef116d4dbebeedcd25e` (2026-09-30), 65 commits beyond
the August review. `docs/reference-map.md` records the source-to-issue
crosswalk. The August revision and archive identities above are historical,
not the latest upstream head.

Prioritize the newly diagnosed root-walking, clock, and lifetime failures:

- The GC resolver now represents a native frame with valid unwind codes but
  no GC slot table separately from an unknown frame. A walker must continue
  through that frame to find deeper roots; treating it as a managed ILC
  trailer reads unrelated bytes, while treating it as end-of-stack loses roots.
  Do not copy upstream's fallback that treats an unregistered record as a
  kernel record: bind every table entry to a validated containing image. Also
  reject overflowing/truncated section extents and distinguish an absent
  managed-code section from positively identified managed code.
- Precise root lookup must distinguish an interrupted instruction pointer from
  the return address of a suspended call. Tests should retain objects reachable
  only from older frames, including parked threads and interleaved native code.
- HPET extension uses a signed 32-bit delta and a periodically refreshed epoch.
  Its half-range sampling assumption and stated lack of SMP correctness are
  mandatory limitations, not details to omit when adapting the idea.
- Wait publication, timeout registration, app-generation cleanup, and freeing
  stacks must be tested as one lifetime contract before hosted task workloads.
- Allocation counters need independently verified observations under the
  selected optimizing ILC. Upstream found that inlined reads around an
  allocation could report zero even though its C# allocator updated the counter.

Use upstream's common std manifest as a warning about duplicated file lists,
not a reason to expand CLROS's no-stdlib EFI closure wholesale. Its current
kernel remains net8/ILC8, and its optional CoreCLR fork and external toolchain
are separate prerequisites. Its documented networking gap remains unchanged.
Shell, USB security-key, CBOR and terminal features are optional later work for
CLROS; none supplies live networking or Kestrel evidence.

No upstream source was copied and no upstream build or guest run was performed
in this review. Historical SharpOS (`references/SharpOS-master`) remains a
different, GPL-licensed, test-concept-only reference. In particular its
`Arithmetic.cs` has a placeholder 64-bit division helper; CLROS must derive
independent expected results, never use that helper as an oracle.

### Upstream steps 185–188 reviewed on 2026-10-03

The subsequent five-commit review pins
`75c045963af300cc48072e12681eb90cd2c60e04`, after the historical
`dfad516b816d29b971d23ef116d4dbebeedcd25e` review above. See the
[current source-to-issue crosswalk](reference-map.md#sharpos-upstream-refresh-2026-10-03)
and its posted #79/#85/#82/#164 addenda. No upstream code was built, executed
or imported, and no issue scope or dependency changed.

Use current conditions over stale comments: `StelemRef_Helper` calls
`AreTypesAssignable`, including its array-to-array branch; the nearby older
unchecked-array limitation is not current. Checked stores and writable refs
still require independent .NET 10 caller/helper proof. Likewise, step188's
reported devirtualized calls demonstrate why resolver-only tests are
insufficient; their one/two/three/four-type behavior is not a .NET 10 promise.
Header publication/root-walking and service-table tail changes supply later
GC and ABI negatives, not current CLROS collector or app-runtime support.

Reject the reference's `RhpStackProbe` policy: its patcher writes `0xC3`
(`ret`) under a no-guard-page assumption. That proves neither CLROS stack
extent nor native ABI/page-touch safety. The new BCL formatting material is
net8-derived with component-specific dotnet MIT notices; root CC0 does not
relicense it. The separate runtime fork and upstream execution reports remain
unreproduced. String constructor work supplies corroboration only, and this
delta supplies no new memory-map/EBS implementation or CLROS evidence.

### Upstream step189 reviewed on 2026-10-06

The next commit, `d5a8ed0df90e2f182379edcf4c4d420c8348cd4c`, adds useful
precise-root decoder, interior-pointer and service-table compatibility cases.
See the [source-to-issue crosswalk](reference-map.md#sharpos-upstream-refresh-2026-10-06)
for #193/#176/#164. The kernel remains net8/ILC8; no new firmware revocation,
cctor or dispatch implementation changes CLROS's immediate dependency order.
The independently bounded collector acceptance remains valid for its declared
scope. This source review did not build, execute or import SharpOS code.

### Boot and diagnostics

- `references/SharpOS-main/OS/src/Boot/ExitBootServicesProbe.cs` demonstrates
  the important order: route diagnostics away from firmware, allocate the map
  buffer before the final map, perform no allocation between map capture and
  `ExitBootServices`, retry stale keys within a bound, and make no Boot
  Services call after success.
- `references/SharpOS-main/OS/src/Boot/BootStackSwitch.cs` demonstrates why the
  active stack must move into explicitly owned memory before reclaiming UEFI
  loader ranges. CLROS additionally requires loaded-image/old-stack/new-stack
  reservations, guards, and an `RSP` proof.
- `references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`
  demonstrates C# build-time materialization of audited x64 templates. CLROS
  starts with its existing fixed `In8`/`Out8` byte plans and introduces a
  generator only after another live stub family proves the need.
- SharpOS allocates some executable stubs as `EfiLoaderCode`. CLROS must not
  copy that permission model: materialize in writable, non-executable pages,
  validate/patch, then transition them to read-execute.
- Reference descriptor/interrupt behavior is useful for sequencing, but CLROS
  builds and loads its own GDT/TSS/IDT. It never copies firmware IDT gates.

### PE applications and storage

- `references/SharpOS-main/OS/src/Kernel/Pe/PeImageLayout.cs`,
  `references/SharpOS-main/OS/src/Kernel/Pe/PeRelocations.cs`, and
  `references/SharpOS-main/OS/src/Kernel/Pe/PeImports.cs` are review points
  for section layout, DIR64 relocation, and imports.
  CLROS's loader remains bounded, caller-owned, status-returning, W^X-aware,
  and restricted to a declared versioned service surface.
- `references/SharpOS-main/OS/src/Hal/Fat32.cs` supplies useful cluster-chain
  and directory behavior.
  CLROS adds explicit loop, bad-cluster, truncation, arithmetic-overflow,
  out-of-partition, and long-file-name tests before live use.
- `references/SharpOS-main/OS/src/Kernel/Threading/` supplies scheduler and
  wait state-machine ideas.
  CLROS still needs preemptive timer, race, timeout, cancellation, FP/SIMD,
  thread-static, and GC-safe-point proof.
- SharpOS's DOOM service table passes raw GOP and HPET information to its app
  tier. CLROS does not reproduce that ABI. `OSAppStartupV1` and `OSHost_*`
  expose POD values and opaque handles, not privileged physical/MMIO pointers.

### Runtime and hosted tier

- `references/SharpOS-main/OS/OS.csproj` and
  `references/SharpOS-main/apps_native/sdk/FreestandingPe.props` target
  `net8.0`.
  The SharpOS roadmap records a failed .NET 10 attempt because
  `System.Buffer.BulkMoveWithWriteBarrier` was missing. CLROS must implement and
  test the .NET 10 contract rather than accepting net8 layout/helper behavior.
- The reference documents useful object, array, string, GC, cctor, delegate,
  interface, exception, and unwind behaviors. They are behavioral oracles, not
  proof that their layouts match CLROS's pinned ILCompiler.
- The local tree does not contain the linked CoreCLR fork or a reproducible
  commit/patch manifest for it. Consequently, the reported hosted PowerShell
  result cannot bootstrap CLROS's hosted tier.
- The reference records unresolved risks that CLROS turns into early
  regressions: XMM nonvolatile state across unwind, collided unwind, finalizer
  shutdown, GC suspension, stack-overflow/IST behavior, cross-image exception
  identity, and incomplete array-store checks.

## Adoption Rules

1. Adapt one live boundary per issue; do not port a directory wholesale.
2. Record the exact reference path, upstream revision if recovered, applicable
   license, affected CLROS files, and copied-versus-reimplemented boundary.
3. Add an independent failure test for each known reference weakness used to
   shape the implementation.
4. Keep `BootSequence.cs`, `Probes.cs`, `std/no-runtime/shared`, the versioned
   app ABI, and `OSHost_*` as CLROS authorities.
5. Keep the external CoreCLR fork and all required C/C++ outside first-party
   `src/`, `std/`, and `apps/`.
6. Preserve CLROS's default-off dangerous probes, stable markers, status
   structs, caller-owned buffers, and no-secret logging.
7. Do not cite reference output as CLROS EBS, runtime, driver, application,
   Kestrel, cloud, or production evidence.

## Known Reference Limitations

- The archive has no embedded Git metadata or trustworthy extraction date.
  Independent byte comparison identifies upstream commit `b80ee3a...` except
  for the omitted top-level `LICENSE`; retain exact local hashes in adaptation
  records so this external identification remains auditable.
- There is no top-level SharpOS license file in the supplied snapshot, but the
  owner permission record and current upstream CC0-1.0 license establish reuse
  permission for SharpOS-authored source. Component licenses for Iced, PeNet,
  and the isolated ManagedDoom app retain their own scope.
- The external CoreCLR source and build products referenced by SharpOS are
  absent.
- The EBS experiment is a default-off, never-returning probe that halts rather
  than entering SharpOS's ordinary launcher.
- The reference kernel/app tier is net8, while CLROS is a .NET 10 project.
- No reference path proves live CLROS virtio, ENA, NVMe, VMBus, NetVSC,
  StorVSC, hosted .NET, Kestrel, AWS, Azure, or production behavior.

## RedPandaOS deep static review (2026-10-06)

Pinned source: [giawa/RedPandaOS](https://github.com/giawa/RedPandaOS/tree/3a2e08183847d4d9596fcfe1cd853474313c45b4),
178 files under `references/RedPandaOS-3a2e08183847`. The November 2025 tip
contains a BGA compile correction and test framework refresh after a long
gap; it is useful low-cadence source, not a newly created project. Inspection
covers source/control flow, not executed defect reproduction or an upstream
build certification. File links below are immutable; absence statements apply
only to this complete pinned source tree.

### Architecture and boot

`Bootloader/Stage1.cs`, `Bootloader/Stage2.cs`, `IL2Asm/`, `CPUHelper/` and
`RedPandaOS/Kernel/Init.cs` describe BIOS/x86 boot, a custom IL-to-NASM path,
fixed addresses and a custom core library. CLROS uses UEFI/x64 and the .NET 10
NativeAOT boundary. The useful comparison is explicit initialization order and
failure dependencies, not the compiler, BIOS calls, ABI or memory layout.

`Init.Start` sequences serial, TSS, heap, PIC/PIT, PCI, paging, storage and app
work. Its SMAP memory classification tests bit 0 of the type instead of an
exact usable-memory type. The sample-app loader reads into fixed 0x400000 and
skips 512 bytes, rather than validating an arbitrary PE image. The syscall path
that marks kernel mappings accessible to users is incompatible with CLROS
ownership/isolation. Do not adopt these behaviors or bypass `BootSequence.cs`.

### Memory, runtime and scheduling

`RedPandaOS/Kernel/Memory/BumpHeap.cs` and `SplitBumpHeap.cs` use fixed memory
areas and implicit ownership. Bump advancement is unchecked; freeing changes
accounting without reclaiming storage. Word-at-a-time initialization needs an
exact-length test for non-word-sized allocations. Large split allocations use
`(size >> 12) + 1`, so an exact page multiple is over-rounded, and large-free
behavior is not a general allocator. Derive exhaustion, overflow, alignment,
canary and no-mutation-on-error cases for #243 instead of porting either heap.

`Runtime/Memory/GCHeap.cs` contains a `StackHeap.Pop` unsigned decrement followed
by an impossible negative test. Its Allocate branch requires `!Borrow` with a
non-null result, opposite to the inspected `Runtime/Collections/ObjectPool.cs`
success contract. That pool truncates the bitmap word count for capacities not
divisible by 32 and does not establish safe double-return ownership. These
objects are not evidence of tracing-GC/root/write-barrier correctness. The
currently active #83 acceptance scope is intentionally not expanded.

`RedPandaOS/Kernel/Memory/Paging.cs` uses 32-bit two-level page structures and
raw mapping/copy operations, including disabling paging for a copy. It cannot
become a CLROS x64 paging implementation. `Scheduler.cs` saves a uint[16]
interrupt frame, allocates some scheduling/idle state lazily, and removes a
terminated task without a complete owned stack/page reclamation protocol.
`RedPandaOS/Runtime/InterruptDisabler.cs` uses one static nesting counter and
unconditionally enables interrupts at final unlock, losing the entering IF
state. Existing #183/#186/#188 cover full x64 context, switching and lifecycle;
their new comments contrast this design with ProtonOS extended-state/TLS save
and deferred cleanup. No assembly or custom-runtime source is imported.

### PCI and block-device behavior

`RedPandaOS/Kernel/Devices/PCI.cs` is a small readable config-port example with
important completeness failures: missing device 0/function 0 ends the bus scan;
multifunction slot 0 is omitted; endpoint classification uses the unmasked
header byte. `ProbeMemorySize` probes separate 32-bit BAR values without a
paired 64-bit resource/restoration transaction. #99/#101 get explicit hole,
multifunction, high-BAR and failed-restoration cases. Do not mistake these
diagnostic helpers for a cloud PCI discovery implementation.

`RedPandaOS/Kernel/Devices/PATA.cs` shows command/status sequencing but includes
unbounded busy and DMA waits. #309 derives progress-source, timeout, reset and
ambiguous-DMA ownership cases. CLROS still prioritizes virtio-blk; no PATA/AHCI
driver milestone is created simply because the reference has a driver.

### FAT32 and the exFAT distinction

`RedPandaOS/Kernel/Filesystem/FAT32.cs` provides a concrete sector/FAT/directory
walk, but it is not a complete robust filesystem. The initial data base already
includes root displacement, then SectorHandler adds cluster displacement again:
root-cluster-not-2 is an important geometry test. The real nested
`SectorHandler.GetNextCluster` reads FAT entries; the separate private method
that returns current+1 is a placeholder and must not be confused with it.
Unbounded chain walking needs cycle/hop limits; a failed advance cannot silently
authorize another read. `ExploreDirectory` handles only sixteen entries from
one sector and skips VFAT, motivating #127/#128 boundary cases alongside
DiscUtils host fixtures. Validate existing CLROS tests before duplicating them.

`ReserveEmptyCluster`/`AdvanceWrite` mutate freshly read FAT buffers without
corresponding persistence of those buffers in the inspected methods. This
supports #468's narrow in-place-write rejection and image/reboot proof cases,
not a proposal for cluster allocation or file growth. `BuildTools/FAT32Writer.cs`
is a host image-layout example with its own fixed assumptions, not a replacement
for the CLROS image builder.

No exFAT source exists in this snapshot; README lists it as planned. The future
boot-region parser uses `DiscUtils.ExFat/Internal/Partition/ExFatBootSector.cs`
and Microsoft's specification. Allocation bitmap, upcase tables, directory-set
checksums and NoFatChain files remain later slices; no general writable exFAT
issue is introduced.

### PE/app loading and diagnostics

`PELoader/PortableExecutableFile.cs` advances the PE32+ directory position using
the 32-bit optional-header size and unconditionally accesses the CLI directory.
That is unsuitable for a native NativeAOT image. `PELoader/VirtualMemory.cs`
illustrates the difference between validating a starting RVA and validating the
entire requested span. #172/#179 receive raw/mapped, no-CLI, truncated header,
cross-section, relocation and overflow fixture guidance, with AsmResolver as a
separate host comparison. The fixed-address sample app path is not a PE-loader
alternative.

`RedPandaOS/Kernel/Exceptions.cs` logs and spins, and its raw stack/symbol scanning is
not structured unwind evidence. `ILInterpreter.Tests/TestPrograms.cs` exercises
an IsPrime interpreter example; it is not a kernel regression suite. A project
having a test directory does not establish subsystem coverage.

### VGA, BGA and framebuffer future use

`RedPandaOS/Kernel/Devices/VGA.cs` demonstrates text cells at 0xB8000 and fixed
80x25 geometry. Coordinate/format parsing needs bounds, and this mode is not a
universal cloud console. `BGA.cs` demonstrates Bochs ports 0x1CE/0x1CF, mode
selection and vertical offset, but guesses between low 32-bit BARs and checks
little beyond width divisibility. It does not validate complete mode/aperture
or y-offset extents. `BitmapFont.cs` supplies signed glyph-offset and clipping
scenarios; its Inconsolata texture assets have separate OFL terms.

The optional future issue starts with caller-owned RGBX/BGRX bytes and a tiny
independent glyph, with no firmware/device calls. GOP/BGA integration, mode
setting, font assets, GUI and input remain out of scope. Serial stays primary.

## New kernel and foundation comparisons (2026-10-06)

Full immutable paths/commits, activity and issue links are in
[the reference map](reference-map.md#accepted-snapshots); exact copy boundaries
and license text paths are in [the license audit](license-audit.md#new-github-snapshot-provenance-2026-10-06).
Findings below are static observations, not tested upstream defects.

| Project | Useful implementation to study | CLROS adaptation and observed limits |
| --- | --- | --- |
| SharpMetal | `src/servers/drivers/storage.nvme/NvmeDriver.cs`: class discovery, 64-bit BAR pair, CAP stride, controller readiness, admin/I/O queues and PRPs. `src/servers/drivers/net.virtio/VirtioNetDriver.cs`: capability/transport path. | #257 studies a real driver sequence but rejects fixed ECAM/virtual addresses, unchecked DMA return, continuing after timeout, phase-only completion and arbitrary LBA 65535 canary writes. PRP construction assumes contiguous pages and needs capacity checks. #101/#105 preserve full BAR/capability extents. .NET 10 NativeAOT does not make native bridges/POSIX/multi-server ABI compatible. |
| SharpMetal harness | `build/scripts/Test-Harness.py`: bounded-looking runner and required regex set. | #236/#309 distinguish a budget from correct failure handling. Blocking readline can defeat a wall-clock deadline; collected panic/fault lines can coexist with reported success when required patterns match. CLROS must reject failure anywhere in the complete run. |
| DefenderOS | `src/Defender.Kernel/LinuxDriverPortFoundation.cs`, `ModernStorage.cs`, `DefenderKernel.cs`, `VirtioNetRuntime.cs`, `NetworkStack.cs`, and the named v0.50.9/v0.50.10/v0.59 fix notes. | #105/#106: preserve physical versus returned virtual MMIO address, including a reported 768-GiB BAR. Do not inherit its PML4 choices or advisory-BAR-size trust. Capability walking can stop on a cycle after finding required entries and still succeed; validate complete record extent before field reads. Partial mapping rollback needs explicit handling. #146 models pre-timer DHCP deferral; its regression script checks source tokens/order, not runtime progress. |
| ProtonOS | `src/kernel/Threading/Scheduler.cs`, `Thread.cs`, `src/kernel/PAL/Thread.cs`, `src/drivers/shared/virtio/VirtioDevice.cs`, `src/ddk/Network/Stack/DhcpClient.cs`. | #183/#186/#188: extended state, TLS, off-stack deferred cleanup and join ordering. #107: explicit status negotiation/read-back, with a separate required-Version1 check. #146: advertised T1 TODO and integer T2 overflow case. Independently reauthor AGPL scenarios; neither Linux syscalls nor custom JIT/bflat ABI replace OSHost_* or NativeAOT. |
| Inu | `SDK/src/Inu.Kernel.Networking/KernelDnsCodec.cs`, `KernelDhcpCodec.cs`, `KernelTcpObservationProtocol.cs`. | #156: question/server/rcode binding, pointer/name validation and bounded cache. The DNS parser can select an A answer using transaction/QR/class/type without the complete original-question identity. TCP flag observation is not sequence/checksum/retransmission proof. No core project license established; test concepts only. |
| SingNextOS | CapabilityAuthorityTests, RegionUseTests and Phase05ResourceBudgetTests under `tests/SingPlus.Tests`. | #290/#291: wrong owner, stale generation, revoked authority, allocation/transfer-versus-teardown and exact charge retention on refused cleanup. Hosted Tasks/reflection and capability/domain framework are not a freestanding isolation implementation. AGPL code is not copied. |
| Cosmos Gen3 | `Virtqueue.cs` and UART parser/patcher tests at canonical `81259dd941a3...`. | #109/#110: uint queue size versus ushort links/counts; zero-size final-index underflow; unchecked descriptor free. #236: incomplete suite and orphan/missing result records. The upstream completed-suite case can preserve a placeholder pass without an individual result; CLROS still requires complete explicit evidence. New UART tests justify the pinned canonical snapshot; queue logic mostly overlaps the original patcher. |
| AsmResolver | `test/AsmResolver.PE.File.Tests/PEFileTest.cs`, `test/AsmResolver.PE.Tests/Relocations/BaseRelocationTest.cs`, `src/AsmResolver.PE/Exceptions/X64UnwindInfo.cs`. | #172/#179: explicit raw/mapped and expected relocation records. The example relocation fixture is HIGHLOW/ABSOLUTE, not DIR64 coverage. #89: FromReader omits odd-count unwind padding before handler/chain; GetPhysicalSize handles exception-only versus termination inconsistently with Write. Use Microsoft's x64 ABI as the authority and independent malformed fixtures, not round-trip agreement. |
| PacketDotNet | `Test/PacketType/TcpPacketTest.cs`, `IPv4PacketTest.cs`, `PacketDotNet/Utils/ChecksumUtils.cs`. | #160: packet backing-length/header/option/checksum cases. `Test/Misc/ChecksumUtilsTest.cs` OnesSum prints without asserting; it is not a standalone oracle. Optional host differential comparison only, MPL file terms retained; captures require separate fixture provenance. |
| LTRData DiscUtils | `Library/DiscUtils.ExFat`, `Tests/LibraryTests/Fat`, `Tests/LibraryTests/Vhd/DiskBuilderTest.cs`, `Library/DiscUtils.Vhd/DiskBuilder.cs`. | #127/#128: filesystem fixture scenarios. #245: fixed VHD footer/type/size/payload cross-check. Future exFAT: checksum/geometry first, independent bounds before reading/shifting. Streams/arrays and volume-wide allocation behavior cannot be transplanted into no-runtime code. Neither decoded VHD nor reference tests prove provider import. |

The first-party result of this review is documentation and backlog guidance.
No runtime helper, boot ordering, ABI, project dependency, image, firmware or
completion gate changes. Source-level findings need independent reproduction
when the corresponding CLROS implementation issue is taken up.

## uACPI deep static review (2026-10-11)

**Decision: add an isolated source/design/test reference.** The installed
`references/uACPI-2cb5e71ef583` pins upstream
`2cb5e71ef58393839e1988ef9a8f1d8ba2845a8d` (6.1.1), with all 168 raw files
verified against Git blobs. Provenance, MIT notice and bundled-tool exclusions
are in [the license ledger](license-audit.md#uacpi-snapshot-provenance-2026-10-11);
[the issue crosswalk](reference-map.md#uacpi-selection-and-issue-crosswalk-2026-10-11)
records four optional follow-ups. This review changes no runtime or gate status.

The inspection covers 19 library compilation units, public/platform headers,
CMake/Meson source inventories, 77 ASL test cases, C API/resource/barebones
runners, generated test cases and CI. Source presence is distinct from executed
coverage: no upstream build, test, benchmark, firmware dump download or guest run
was performed. CMake, clang and iASL were absent from the review shell's PATH.
README speed/compatibility claims were not independently measured.

### Table ownership and firmware policy

`source/tables.c` maps headers and complete tables, records origin/reference
state, and supports early caller-provided metadata storage. Barebones mode
needs only RSDP lookup, mapping/unmapping and logging. This offers staged
initialization comparisons, but CLROS already retains five bounded bootstrap
tables and selects owned MADT/HPET/MCFG after exit (#59/#539/#95/#606).
FADT handling adds useful length/revision/legacy/extended/reset metadata ideas;
[#666](http://192.168.4.35:3000/admin/CLROS/issues/666) selects host parsing only.

Preserve CLROS's strict validation. Upstream bad-checksum rejection is
configurable; root iteration masks off incomplete pointer bytes and skips null
entries; its generic subtable walker can succeed after warning about trailing
bytes. FADT handling rewrites revision metadata and prefers legacy FACS for a
firmware quirk. These are compatibility choices, not CLROS policy. Independent
malformed fixtures and the ACPI specification must govern adaptations.

### GAS and resource framing

`source/io.c` separates GAS mapping from access and distinguishes SystemMemory
from SystemIO. [#665](http://192.168.4.35:3000/admin/CLROS/issues/665) uses this
for raw metadata and a pure register validator. A static concern in
`gas_get_access_bit_width` multiplies nonzero `access_size` by eight when the
first width branch does not apply. Encodings 3/4 then produce 24/32 bits,
while [ACPI 6.6 GAS](https://uefi.org/specs/ACPI/6.6/05_ACPI_Software_Programming_Model.html#generic-address-structure)
defines 32/64. This is not an executed upstream defect; independently authored
encoding/offset cases must prevent importing that arithmetic.

`source/resources.c` checks small/large resource lengths, payload bounds,
fixed/variable forms and EndTag presence. Its richer native/resource conversion
allocates buffers and applies item-specific compatibility behavior.
`tests/runner/resource_tests.c` supplies encoding/round-trip ideas; agreement
between related encoders is not an independent oracle.
[#667](http://192.168.4.35:3000/admin/CLROS/issues/667) is limited to framing,
EndTag/checksum and atomic caller-owned descriptor output. It does not evaluate
`_CRS`, decode every resource semantic or activate devices.

### AML execution, lifetimes and hardware authority

`source/interpreter.c` uses explicit execution/call-frame arrays, opcode handler
tables and parse programs rather than native recursion for AML method calls.
Its defaults include 256 method frames and a 30-second loop timeout.
`source/types.c` and `source/shareable.c` manage references and non-recursive
object teardown; `source/namespace.c` resolves names/object identity. These
inform a bounded C# interpreter. Loop timeouts alone do not bound instruction
count, namespace growth, allocation or every execution path.

`source/opregion.c` and `source/default_handlers.c` expose address-space/field
operations and PCI bridge/configuration behavior. `source/utilities.c` has
`_STA` and `_PRT` helpers; resource evaluation includes `_CRS`. This model goes
beyond MADT/MCFG scanning and introduces firmware-requested I/O and writes.
CLROS must retain required DSDT/SSDT bytes before firmware exit and admit
explicit owned capabilities before activating operation-region access.

The full `include/uacpi/kernel_api.h` boundary requires allocation/free,
monotonic time, stall/sleep, thread identity, mutex/event/spinlock semantics,
interrupt disable/restore and installation, PCI/port I/O, deferred work and
work/IRQ drain. `source/event.c`/`source/notify.c` use these for SCI/GPE/fixed
events and notifications. Reduced-hardware mode removes parts of this machinery
but does not provide CLROS services or eliminate every interpreter dependency.
Existing M09 #188/#189/#190/#194/#198 cover relevant future services.

[#668](http://192.168.4.35:3000/admin/CLROS/issues/668) requests staged C# AML
research: a bounded no-I/O integer host experiment first, then separate
device/resource capabilities and retained table ownership. The initial scheduling
blocker #662/#663/#664 is qualified in step1187 and closed. A 2026-10-12 tracker
read-back marks #668 ready and unassigned for research only; former catalog
children #658/#659/#660 are qualified/closed in step1184. Hardware execution has
stronger service dependencies than research readiness. No native uACPI library
is linked.

These optional reference issues do not replace the current
[NativeAOT-first implementation order](workload-profiles.md#nativeaot-first-execution-roadmap-2026-10-12).
The M09 service references above identify future hardware-AML requirements;
they do not reinstate the older CoreCLR-first sequence.

### Tests and unmerged fixes

The full C runner uses host threading/runtime adapters; the barebones runner
maps synthetic table pointers directly and proves no physical mapping.
CI covers Windows/Linux, 32/64-bit and OpenWatcom, with a separate reduced-
hardware/configuration compilation check. GCC/clang runner builds enable
address/undefined sanitizers; MSVC explicitly omits ASan. The Python harness
can clone external firmware dumps and invokes iASL/CMake. None ran here.

Upstream [PR #166](https://github.com/uACPI/uACPI/pull/166), reviewed separately
at head `e60f36fe89e020a50a847fe1f7c1a47af1d81731`, proposes 59-file changes.
Its negative cases address event/notify wait-under-lock deadlocks, GPE context
lifetime, field/operation-region replacement during callbacks, failed handler
allocation, package reference cycles and MSVC atomic compare/exchange ordering.
It also proposes counting-semaphore and preallocated-work-item kernel API
changes. These are independent rejection/lifetime test themes; the unmerged
implementation is absent from the installed master pin. That pin's callback API
must not be described as settled future CLROS architecture.

The first-party result is documentation, provenance and four verified
supplemental issues. Parser implementation, native/guest integration, general
AML/device discovery and completion-gate changes remain separate work.

## Validation

```powershell
Test-Path .\references\SharpOS-main\OS\src\Boot\ExitBootServicesProbe.cs
Test-Path .\references\SharpOS-main\OS\src\Boot\BootStackSwitch.cs
Test-Path .\references\SharpOS-main\bootasm\BootAsm.Generator\BootAsmGenerator.cs
Test-Path .\references\SharpOS-main\OS\src\Kernel\Pe\PeImageLayout.cs
Test-Path .\references\SharpOS-main\OS\src\Kernel\Pe\PeRelocations.cs
Test-Path .\references\SharpOS-main\OS\src\Kernel\Pe\PeImports.cs
Test-Path .\references\SharpOS-main\OS\src\Kernel\Threading
Test-Path .\references\SharpOS-main\OS\src\Hal\Fat32.cs
Test-Path .\references\SharpOS-main\done\step076.md
Test-Path .\references\SharpOS-main\done\step143.md
git diff --check
```

All listed `Test-Path` results must be `True`. They prove only that the
inspectable local files exist.
