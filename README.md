<p align="center">
  <img src="img/clros_logo.png" alt="CLROS logo" width="500">
</p>

<h1 align="center">CLROS</h1>

<p align="center">
  <strong>A C#-first NativeAOT operating system for minimal cloud workloads.</strong>
</p>

<p align="center">
  .NET 10 applications are the long-term first-class workload target. ASP.NET
  Minimal API and Kestrel support is a mandatory milestone, not a demo claim.
</p>

> Current status: early boot/runtime boundary prototype with strong host-tested
> contracts and QEMU/OVMF smoke evidence for the firmware-visible marker path.
> It is not a functional kernel, not cloud-ready, and not production-ready.

## Progress

Progress is gate-based; the repository does not publish a subjective completion
percentage. The full goal is a production-ready CloudReady C# NativeAOT
operating system that can run on Azure Virtual Machines and AWS EC2/Nitro
instances with first-class hosted .NET 10 Minimal API app support.

`docs/completion-gates.md` remains the source of truth for claims. As of this
snapshot, only Gate 2 passes, and that pass is limited to the documented
QEMU/OVMF firmware-visible boot-smoke marker path. The project has several
partial gates and many prerequisite contracts, but it has not yet passed
`ExitBootServices`, owned-driver, live storage, live networking, native-app
execution, exported PAL, hosted .NET, Kestrel, Azure, AWS, or production gates.

### Current implementation checkpoint (2026-08-05)

- The live local Gitea tracker has **276 open issues** (issues only, excluding
  pull requests). That is seven fewer than the earlier 283-issue snapshot even
  though several narrowly scoped integration/review issues were added to make
  shared-file ownership explicit. The reconstructed backlog is intentionally
  granular: its raw count is a queue size, not a completion percentage.
- M02-004 is now reviewed, merged, evidence-backed, and closed together with
  its three supplemental integration issues. Its production C# corrected the
  Windows x64 `Out8` register flow, bound the checked port-I/O function
  pointers, and executed exactly seven pre-`ExitBootServices` COM1
  configuration writes under QEMU/OVMF. The exact guest marker records
  `guestPortIo=true`, `guestIn8=false`, `markerTransport=uefiSerial`, and
  `postExitBootServices=false`; it is not owned post-firmware serial evidence.
- M04-002 is now reviewed, merged, evidence-backed, and closed. Its production
  C# adds an allocation-free endian catalog with 25 explicit read vectors and
  seven reverse vectors plus a no-stdlib EFI validation path. The accepted
  QEMU/OVMF marker still records `hostConformance=false`,
  `guestIntegrated=true`, and `gate4=Fail`; the slice did not complete general
  `BinaryPrimitives` compatibility or NativeAOT runtime correctness.
- The active M02-005 wave is real production and integration work. Managed
  issue #45 adds the 303-line allocation-free
  `OwnedCom1ReadyExclusivelyOwnedWriter`; supplemental issue #450 links and
  calls it from the explicit no-stdlib EFI closure; and bounded tooling issue
  #451 raises only the QEMU runner's marker ceiling from 256 to 320 characters.
  Focused C# tests cover exact COM1 port/byte order, delayed readiness,
  timeout, invalid limits, an unready binding, default-result safety, transport
  separation, EFI ordering, and the 259/320/321-character runner boundary.
- A development QEMU/OVMF run of that claimed wave has already observed the
  standalone `OWNED_COM1_READY` line from checked `In8(0x3FD)` /
  `Out8(0x3F8)` execution, followed exactly once by the separate UEFI-serial
  `CLROS_M02_005_READY ... guestPortIo=true guestIn8=true
  postExitBootServices=false` evidence line. Issues #45, #450, and #451 remain
  `agent:claimed` until the complete final-head gate and exact-head independent
  review pass. M04-003 (#72) is the sole `agent:ready` issue and will not be
  claimed across this serialized integration wave.
- These changes do not advance a numbered completion gate. Gate 2 remains the
  only passing gate, scoped to the firmware-visible QEMU/OVMF marker path;
  Gate 4 remains `Fail`. The M04 marker proves only fixed-catalog validation in
  the EFI guest. There is still no proof of general `BinaryPrimitives`
  compatibility, allocation/GC/exception completion, real `ExitBootServices`,
  owned storage/networking, guest app execution, hosted .NET, Kestrel,
  Azure/AWS support, CloudReady status, or production readiness.

## What Works Today

| Area | Current evidence |
| --- | --- |
| Build baseline | `CLROS.slnx` builds locally with .NET 10/C# 14.0 project settings. Host tests emit stable `TEST_START`, `TEST_PASS`, and `TEST_FAIL` markers. Source naming and source-firewall scripts exist. |
| EFI boot artifact | `src/OS` publishes a minimal explicit no-stdlib NativeAOT EFI application. It is included in `CLROS.slnx` but retains stricter no-stdlib build rules. |
| QEMU/OVMF smoke path | The raw GPT/FAT32 ESP image boots under QEMU/OVMF to firmware-visible markers including `OS_BOOT_OK` and `OS_SMOKE_PASS`. |
| Boot ordering | `src/OS.Kernel/Boot/BootSequence.cs` is the boot-order source of truth. Host tests verify ordered boot phases and marker emission. |
| UEFI handoff | The EFI path captures UEFI memory-map metadata, copies descriptor bytes into OS-owned storage, normalizes compact boot-region records, and carries ACPI RSDP provenance into the handoff. |
| ACPI discovery | The default path carries ACPI RSDP provenance. An explicit opt-in ACPI probe artifact has QEMU evidence for pre-`ExitBootServices` RSDP, root SDT, and MCFG discovery markers. |
| Diagnostics planning | Host-tested COM1, panic-text, panic-serial, boot-event records, diagnostics-chain readiness, GDT, TSS/IST, IDT, descriptor-table, fault-entry, diagnostic-stub invocation, diagnostic-stub callable-binding formatting, and controlled-fault-probe planning contracts exist. The opt-in M02-005 EFI profile now has development QEMU/OVMF evidence for a bounded pre-`ExitBootServices` COM1 line-status read and exact marker payload write; final-head review is pending and no post-firmware serial claim follows. |
| `std/no-runtime` base | Host-tested no-runtime-compatible helpers exist for ASCII byte/text behavior, decimal and hexadecimal formatting/parsing, memory/buffer operations, endian primitives, native integer arithmetic, math helpers, CRC32, internet checksum, and `ArrayHelpers` search/sort contracts. The opt-in EFI path now validates the fixed 25-read/7-reverse M04-002 endian catalog under real QEMU/OVMF, but this does not create a canonical BCL surface or complete Gate 4. Runtime-helper gaps remain explicit. |
| PAL contracts | `OSHost_*` POD-style status/result contracts and host-tested mock tables/facades exist for memory, package files, config/environment, sockets, socket connect metadata, timers, waits/events, threads, random bytes, status formatting, and app logs. Recent work hardened default/zero result values so they do not accidentally count as success evidence. |
| Storage groundwork | GPT, caller-owned GPT partition selection, FAT32, package parser, package filesystem, package file staging, native-app package/payload staging, generated image package placement, manifest loading, caller-buffer package-loader GPT selector integration, and virtio-blk planning slices are host-tested. |
| Networking groundwork | Ethernet, ARP, IPv4, ICMPv4, UDP, DHCP, DNS, TCP handshake/connection-table/close/payload planning, active close FIN/ACK generation, receive dispatch, checksum validation/generation, HTTP request parsing, HTTP response writing, static HTTP transactions, and mock HTTP GET transactions are host-tested. |
| App model groundwork | Native app manifest parsing, package selection, payload header validation, executable mapping/planning, invocation metadata, lifecycle readiness, stdout/stderr log tables, and app status marker formatting are host-tested. |
| Image tooling | Raw GPT/FAT32 image generation exists. QEMU smoke tooling, diagnostics marker presets, qcow2 conversion wrappers, libvirt intent/dry-run/preflight paths, baseline artifact summaries, and cloud-provider requirement docs are present. |
| CI smoke scaffolding | A Gitea workflow skeleton and host-tested CI smoke wrapper dry-run path exist for Windows agents, including qemu-img discovery, baseline evidence planning, artifact upload metadata, and `git diff --check` coverage. Completion Gate 11 is guarded to remain failed until executed Gitea runner evidence is preserved. |
| Recoverable backlog | `.gitea/backlog/clros-issues.json` preserves 16 milestones, 68 managed labels, 16 epics, and 303 small child issues. `scripts/sync-gitea-backlog.ps1` previews by default and idempotently synchronizes the local `admin/CLROS` tracker only with explicit `-Apply`. |
| Documentation | Repository docs define the canonical architecture, host requirements, completion gates, boot flow, PAL boundary, Kestrel milestone, cloud-provider requirements, and immutable `done/stepNN.md` evidence history. |

## What Is Not Complete

| Area | Missing before claim |
| --- | --- |
| Real firmware transition | No live `ExitBootServices` transition has completed. Fresh-map and transition planning exist, but the real firmware shutdown path is still future work. |
| Owned post-firmware diagnostics | The opt-in M02-005 profile can emit one bounded marker directly through COM1 before firmware exit, but the live boot path does not yet own serial output after firmware services are gone and panic routing is not live. |
| Allocator and paging | The live EFI path initializes bounded static free-list state from conventional-memory records and emits `BOOT_PAGE_ALLOCATOR_READY`, but it does not yet subtract every reservation, execute a live allocation/free/coalesce cycle, configure page tables, enforce NX/W^X, or provide a managed heap. |
| CPU fault handling | Descriptor and fault plans exist, but real IDT/GDT/TSS loading, handlers, register capture, and QEMU fault probes are not implemented. |
| Runtime correctness | GC, exceptions, interface dispatch, reflection policy, thread/runtime helpers, and hosted-runtime integration are not complete. |
| Scheduler and synchronization | Wait/event/timer mock tables exist, but there is no scheduler, blocking wait, thread parking, hardware timer interrupt path, ThreadPool, `Monitor`, or `Task` runtime support. |
| Live platform drivers | ACPI/PCI/virtio/ENA/NVMe/Hyper-V/VMBus work is parser/planning evidence only. There is no live PCI ECAM mapping, BAR mapping, interrupt setup, device probing, or DMA-safe driver execution. |
| Live storage | GPT/FAT/package logic is host-tested, but the guest does not yet read manifests or packages from a live virtio-blk/NVMe disk after boot. |
| Live networking | Protocol parsers and planners are host-tested, but there is no live virtio-net/ENA packet I/O, interrupt processing, DHCP lease acquisition, TCP listener, or HTTP server path. |
| Exported PAL | The `OSHost_*` ABI shape is documented and host-tested through facades, but unmanaged PAL exports are not wired to a hosted runtime. |
| Native app execution | Launch planning exists, but the guest does not yet map and invoke a packaged native workload in the live boot path. |
| Hosted .NET 10 | No hosted .NET 10 runtime starts in the guest. No CoreCLR/NativeAOT hosted-app boundary is complete. |
| ASP.NET Minimal API/Kestrel | No Minimal API app is loaded, bound, served, or externally probed. Kestrel remains a mandatory future milestone. |
| Cloud provider proof | No Azure VM or AWS EC2/Nitro boot/import evidence exists. Local QEMU/OVMF evidence is not a cloud-provider claim. |
| Production readiness | No production hardening, supervisor, app isolation, update story, release logging policy, or full security gate evidence exists. |

## Informal Subsystem Summary

This table is navigation context, not a second gate ledger. Its descriptive
states do not override `docs/completion-gates.md`, where Gate 2 is `Pass` and
every other gate is currently `Fail`.

| Gate | Current state | Plain-language meaning |
| --- | --- | --- |
| Clean build | Partial | Local solution and focused host builds pass, but formal clean-checkout gate logs still need to be preserved as release evidence. |
| Boot | Pass, scoped | QEMU/OVMF reaches the documented firmware-visible boot-smoke markers, but this does not prove `ExitBootServices`, owned drivers, storage, networking, or app execution. |
| Fault diagnostics | In progress | Strong plans and host tests exist, but real descriptor-table installation and panic handling are not live. |
| std/runtime | In progress | Useful no-runtime helpers exist, but runtime helpers, GC, exceptions, and dispatch are incomplete. |
| UEFI encapsulation | Partial | Firmware-facing boundaries are taking shape; real `ExitBootServices` and owned post-firmware platform services remain. |
| Platform/drivers | In progress | ACPI/PCI/virtio/Nitro/Azure planning is substantial, but live hardware interaction is not proven. |
| Storage | In progress | Parsers, package models, and virtio-blk plans exist; live disk-backed package loading is not proven. |
| Networking | In progress | Protocol and virtio-net planning coverage is broad; live packet I/O and a serving TCP stack do not exist yet. |
| Native app model | In progress | Manifest/package/launch planning exists; no guest app is executed yet. |
| Hosted .NET/Kestrel | Not passed | No hosted .NET 10 app or Kestrel server runs in the guest. |
| Cloud images | Partial | Local image and QEMU tooling exist; Azure/AWS import and boot proof are missing. |
| CI/security/docs | Mixed | Docs are strong, local scripts exist, and dry-run CI workflow scaffolding is host-tested. CI remains incomplete until a compatible Gitea Windows runner executes the workflow from a clean checkout and preserves logs/artifacts. Production security gates are not complete. |

## Kestrel Milestone Boundary

The required Kestrel milestone is specific:

1. Boot the OS image.
2. Load one configured workload package.
3. Start the hosted .NET 10 runtime or documented supported hosted-app target.
4. Provide the required `OSHost_*` PAL contracts for memory, files, config,
   sockets, timers, threads, waits, and logging.
5. Bind an ASP.NET Minimal API app through Kestrel to the manifest endpoint.
6. Serve `GET /healthz` over the OS network stack.
7. Emit the required serial and runner markers, including
   `TEST_START kestrel.smoke`, `KESTREL_APP_START`, `KESTREL_BIND_READY`,
   `HTTP_PROBE_PASS`, and `TEST_PASS kestrel.smoke`.

Anything short of that is prerequisite work, not Kestrel evidence.

## Build And Smoke Commands

Run from the repository root:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\run-ci-smoke.ps1 -DryRun
.\scripts\build-efi-shell.ps1
git diff --check
```

Real QEMU smoke on the current Windows host:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Optional pre-`ExitBootServices` owned-COM1 marker profile:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe
```

That optional profile rebuilds the EFI/image inputs, requires the standalone
owned-COM1 marker and its separate evidence marker exactly once, and forbids
the M02-005 failure marker. It is not post-`ExitBootServices`, physical-device,
provider, CloudReady, or production evidence.

Optional ACPI physical discovery probe:

```powershell
.\scripts\build-efi-shell.ps1 -EnableAcpiPhysicalDiscoveryProbe -EfiOutputPath artifacts/efi/BOOTX64-ACPI.EFI -LogPath artifacts/logs/efi-shell-publish-acpi.log
.\scripts\run-qemu-smoke.ps1 -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15 -MarkerPreset acpi
```

Dry-run QEMU output is not boot proof. Boot proof requires a generated image,
OVMF/QEMU execution, captured serial logs, and expected markers.

## Repository Layout

| Path | Purpose |
| --- | --- |
| `src/OS` | Explicit no-stdlib NativeAOT EFI shell project included in `CLROS.slnx` under stricter build rules. |
| `src/OS.Kernel` | Host-testable kernel, boot, diagnostics, platform, PAL, storage, networking, and app-model contracts. |
| `std/no-runtime/shared` | No-runtime-compatible shared utility and BCL-like surface. Shared helpers go here before kernel/app usage. |
| `apps` | App fixtures and workload placeholders. |
| `tools` | Host-side image/package/build tooling. |
| `tests/OS.HostTests` | Host test harness with stable machine-readable markers. |
| `scripts` | PowerShell build, smoke, evidence, and helper scripts. |
| `docs` | Canonical architecture, operation, gate, and subsystem documentation. |
| `done` | Immutable step evidence history. Do not delete step files. |
| `work` | Design work in progress, including PAL design notes. |
| `references` | Reference repositories only. They are not first-party source. |
| `artifacts` | Generated output. Do not commit generated artifacts unless explicitly required. |

## Evidence Markers

Serial and test output uses stable markers so humans and automation can trust
the same logs:

```text
TEST_START <name>
TEST_PASS <name>
TEST_FAIL <name> <reason>
OS_BOOT_OK
OS_SMOKE_PASS
OS_SMOKE_FAIL
```

## Documentation

Start here:

- `AGENTS.md` - project invariants, agent workflow, and evidence rules.
- `docs/documentation-map.md` - where canonical information belongs.
- `docs/host-requirements.md` - Windows/.NET/QEMU/toolchain requirements.
- `docs/completion-gates.md` - claim boundaries and current gate evidence.
- `docs/boot.md` and `docs/boot-order.md` - boot flow and marker order.
- `docs/pal.md` - hosted runtime and `OSHost_*` PAL boundary.
- `docs/kestrel.md` - mandatory Minimal API/Kestrel milestone ledger.
- `docs/cloud-provider-requirements.md` - Azure/AWS provider requirements.

Repo docs are canonical for build/run commands, ABI contracts, completion
gates, subsystem status, and evidence. The Gitea wiki may contain
reader-friendly summaries only, and those summaries must link back to repo docs.

## Project Rules

- Project name: `CLROS`.
- First-party kernel/std/app source committed here must be C#.
- Do not commit `.c`, `.cpp`, `.h`, `.asm`, or `.s` files.
- Use C# 14.0 and .NET 10 for normal managed projects.
- Keep `src/OS.Kernel/Boot/BootSequence.cs` as the boot-order source of truth.
- Keep dangerous probes disabled by default in
  `src/OS.Kernel/Diagnostics/Probes.cs`.
- Do not claim hosted .NET, Kestrel, Azure, AWS, cloud readiness, or production
  readiness without matching evidence in `docs/completion-gates.md`.
