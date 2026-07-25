# CLROS and SharpOS Reference Diff

## Current Status

CLROS is a separate project. Its product, repository, and solution identity is
`CLROS`/`CLROS.slnx`; its existing assemblies, namespaces, source paths, and
host ABI remain `OS.*`, `src/OS`, `src/OS.Kernel`, and `OSHost_*`.

The primary modern reference is the local
`references/SharpOS-main` snapshot. The former documented path
`references/daniilvaino-SharpOS-main` does not exist. The snapshot has no
`.git` metadata, so its upstream revision cannot be proven from the archive.
Its README links to a separate
`daniilvaino/dotnet-runtime-sharpos` CoreCLR fork, but that fork is not present
under `references/`.

The project owner supplied a Telegram permission record identifying
`daniilvaino/SharpOS` as CC0, explicitly permitting copying, and proposing
direct consolidation with CLROS. Current upstream commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5` also carries a top-level
CC0-1.0 license. SharpOS-authored source is therefore eligible for selective
adaptation. This does not identify the local archive's commit and does not
relicense vendor code, the GPL ManagedDoom fixture, WAD/media assets, or the
absent CoreCLR fork; exact local file hashes remain mandatory in adaptation
records.

SharpOS's latest numbered milestone file is `done/step143.md`. The file does
not embed a trustworthy source date, and the archive's filesystem timestamps
identify only this local extraction, not the upstream revision. It reports
playable Managed DOOM on bare metal with GOP output, PS/2 keyboard input, HPET
timing, and exit code 42. `done/step076.md` reports a gated physical
`ExitBootServices` experiment with owned UART/framebuffer/PS/2 output after the
call. These are inspectable historical claims from another project, not CLROS
boot or completion evidence.

### SharpOS evidence classes

| Local reference evidence | Class | What it can support | Why it is not reproducible CLROS proof |
| --- | --- | --- | --- |
| `references/SharpOS-main/OS/src/**`, `bootasm/**`, and numbered `done/stepNNN.md` files | Inspectable source and narrative evidence | Architecture review, bounded CC0 source adaptations, negative tests, and historical sequencing ideas | Owner permission and current upstream CC0 clear SharpOS-authored reuse, but the archive has no recoverable Git revision and the code targets a different runtime/build boundary; bind adaptations to local file hashes |
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

- The snapshot revision and archive date are not cryptographically recorded.
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
