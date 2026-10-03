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

### Current implementation checkpoint (updated through 2026-10-02)

- M04-004 now has independently reviewed host and fresh default/enabled
  QEMU/OVMF evidence. The opt-in NativeAOT EFI profile executes 57 integer
  arithmetic, shift, cast, enum and division/remainder vectors with fingerprint
  `45FDFA24DD72EA28`; the default profile emits no M04 conformance markers.
  The settled host gate passed 3,516 starts and 3,517 passes with zero failures.
  See `done/step929.md` for exact source, artifact and serial identities,
  including the corrected audit of OVMF output after firmware return.
  This fixed-catalog proof leaves Gate 4 at **Fail**.
- M02-014 now has independently reviewed default and opt-in terminal page-fault
  QEMU/OVMF evidence at source `7c71e72`. The enabled profile observes vector 14,
  CR2/error zero, an owned root, absent null/guard pages and IST2 frame capture
  before `ExitBootServices`. The complete host suite passed 3,558 starts and
  3,559 passes with zero failures. See [step944](done/step944.md) for the bounded
  fault, source/artifact identities and image accounting. Defaults stay off;
  recovery and persistent descriptor ownership remain unsupported. Gates 3/4
  remain **Fail**.
- M02-015 adds an independently reviewed host double-fault byte emitter and
  armed-IDT audit. Focused tests pass 9 starts/10 passes with zero failures;
  the actual no-stdlib closure has zero AssemblyRefs. This is data and modeled
  execution evidence only. The complete host gate passes in
  [step949](done/step949.md). The separate default-off EFI adapter now has
  reviewed source and default/enabled NativeAOT artifact evidence in
  [step950](done/step950.md). The settled-source host suite and fresh default,
  page-fault and double-fault QEMU captures are independently accepted in
  [step951](done/step951.md), including vector 8/error 0 and the complete capture
  on original IST1. PR #522 merge and issue closure are separate tracker actions.

- M04-007 has accepted bounded `RhpNewFast` guest allocation evidence over the
  original retained heap authority. The complete host suite passes 3,573 starts
  and 3,574 passes with zero failures. Five fresh QEMU profiles prove default
  isolation, standalone heap behavior, four compiled-new/six direct allocations,
  before-bind rejection and exhaustion. Native ABI, managed metadata, relocated
  prefixes, raw transcripts, lifecycle and disk evidence are independently
  reviewed in [step949](done/step949.md). General allocation, GC and Gate 4
  remain incomplete.

- M04-008 has accepted bounded byte/long SZ-array guest evidence sharing that
  original allocation page with fixed objects. The complete host suite passes
  3,611 starts/3,612 passes with zero failures. Eight fresh profiles cover the
  mixed allocations, before-bind rejection, exhaustion and default/heap/fixed/
  page-fault/double-fault regressions. Independent native, transcript, lifecycle
  and disk reviews are recorded in [step954](done/step954.md). General arrays,
  managed reference stores, GC, strings and Gate 4 remain incomplete.

- M04-009 adds host-tested String storage to the same retained allocation page
  and cursor. Complete fixed/array/String modules pass 31 starts/32 passes,
  including UTF-16 storage, terminators, signed-length checks and unchanged
  rejection storage. [Step955](done/step955.md) records this host checkpoint;
  compiled String constructors and guest execution remain pending.

- The 2026-08-06 local Gitea snapshot had **273 open issues** (issues only,
  excluding pull requests). That was ten fewer than the earlier 283-issue
  snapshot even though narrowly scoped integration/review issues were added to
  make shared-file ownership explicit. The reconstructed backlog is
  intentionally granular: its raw count is a queue size, not a completion
  percentage.
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
- M02-005 is reviewed, merged, evidence-backed, and closed. Its allocation-free
  C# writer emits `OWNED_COM1_READY` directly through checked
  `In8(0x3FD)`/`Out8(0x3F8)` under QEMU/OVMF, followed by a separately identified
  UEFI-serial evidence line. This remains a bounded pre-`ExitBootServices`
  marker path, not owned post-firmware diagnostics.
- M02-006 is reviewed, merged, evidence-backed, and closed. Its production C#
  is a no-stdlib-compatible, status-coded panic formatter/reporter over
  `Com1OwnedPortIoBackend`. The accepted guest profile emitted its harmless
  readiness marker directly through checked COM1 and separate evidence through
  UEFI serial; it did not trigger a panic or prove post-firmware diagnostics.
- M02-007 is reviewed, merged, and backed by real QEMU/OVMF evidence. Its
  allocation-free encoder creates and reads back the exact 40-byte x64 GDT in
  caller-owned guest memory without executing `lgdt` or `ltr`.
- M02-008 is reviewed, merged, evidence-backed, and closed. Its serialized
  integration links the reviewed guarded-IST
  arena contract into the explicit no-stdlib EFI closure. The opt-in path uses
  a temporary six-page UEFI LoaderData arena, validates the three guard/stack
  layouts, writes and reads back only the stack ranges, releases the lease, and
  frees the arena before returning. M02-008 does not install a TSS, IST, GDT, or IDT.
  The development source closure is now 60
  compile items and 73 source-authority inputs after the later default-off
  M02-009 terminal stub, M02-010 normalized interrupt-entry page, and M04-003
  span/memory catalog sources were added.
  The complete host run caught an older M01-025 source-firewall regression in
  the first implementation; the integration now uses a fixed-size local stack
  workspace instead of `stackalloc`, preserving both the 104/40-byte caller-
  owned contract and the established firewall. A corrected development
  QEMU/OVMF run then accepted all 56 ordered/unique markers, rejected all 40
  forbidden markers, and observed the intact `selectors=0,8,16,24` and
  `CLROS_M02_008_READY` markers exactly once. The artifact SHA-256 is
  `58464cd09ece7ce392252588d5c3327ccf64cfe19af4087454292e6646366d05`;
  the serial-log SHA-256 is
  `96eaea7e7126332514b3ffa1316152e7471d5b67a82e20839f18d3ed620f865c`.
  The uncompromised host gate then completed 3,446 starts and 3,447 passes with
  zero failures and one terminal `TEST_PASS host.tests`. Independent `admin`
  review #20 approved exact head
  `845798836c010c388262c062efdac04cefbb80d3`; PR #471 merged that exact tree as
  `5b6de7d08b348c9094dcb38dc7183f2c8182cb16`. Supplemental issue #467 and
  managed backlog issue #48 are closed, with the latter reconciled through the
  canonical idempotent manifest synchronizer. That dependency closure makes
  the next terminal integration possible without changing M02-008 evidence.
- M02-009 is implemented in this development tree behind the default-false
  `EnableM02OwnedGdtTssTerminalProbe` switch. It prepares one disjoint
  `EfiLoaderCode` page and one `EfiLoaderData` page before normal boot markers,
  validates the reviewed 2,987-byte terminal stub and 168-byte context, then
  enters it only after `OS_SMOKE_PASS` through a serialized Windows-x64 call.
  The expected path loads its private GDT/TSS, emits its final records through
  checked owned COM1, and halts. Source-bound tests and build preflight are in
  place. `done/step918.md` records the fresh real QEMU/OVMF acceptance for the
  exact 59-required/43-forbidden terminal profile. This remains
  pre-`ExitBootServices` development work and does not prove W^X, an IDT/fault path, post-firmware
  diagnostics, hosted .NET, Kestrel, cloud readiness, or production readiness.
- M02-010 is now integrated behind the default-false
  `EnableM02NormalizedInterruptFramePageProbe` switch. The source-bound
  NativeAOT EFI profile allocates one temporary 4,096-byte `EfiLoaderData`
  page, materializes all 256 fixed interrupt-entry slots against a modeled
  common-target delta of 4,096, validates all bytes through the production
  readback operation, requires FNV-1a64 `428D0353A96B7ACE`, and releases the
  page before ordinary memory-map capture. An EFI-successful allocation is
  treated as owned even when its returned address is invalid or zero, so that
  rejected result is also released exactly once. The final accepted QEMU/OVMF
  run at exact source head
  `b88a485c8562a0022ffaae760fb47ca9d5addba2` observed the exact start/ready/pass
  transcript once under the 17-required/five-ordered/
  three-unique/three-forbidden profile. Its 67,072-byte EFI artifact has
  SHA-256 `9D7EE566D93ED876DDBE1825CC5629330CF46DEEB92F23473F8C6E3A06249B6D`;
  the serial log has SHA-256
  `4E03A7CFCF8BD78018E3ABF35897EE3371B6E299FED70F32E56D4CEDE6EEB678`.
  `done/step920.md` records the bounded proof. No generated slot or common
  target was executed, no executable mapping was established, no IDT/IDTR/IST
  was installed, and no live interrupt or fault was produced.
- A pinned SharpOS comparison has been preserved in seven detailed addenda on
  existing Gitea issues #85, #94, #96, #229, #236, #315, and #316, plus three
  new blocked supplemental issues: #468 for bounded in-place FAT32 boot-log
  writes, #469 for bounded xHCI/USB discovery, and #470 for bounded console
  input/wait/terminal-feed semantics. Every entry pins upstream source and
  provenance, includes relevant source excerpts and CLROS C# contract sketches,
  identifies dependencies and hostile tests, and preserves evidence/claim
  boundaries. No SharpOS code has been imported into first-party CLROS source
  by that review.
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
| Diagnostics planning | Host-tested COM1, panic-text, panic-serial, boot-event records, diagnostics-chain readiness, GDT, TSS/IST, IDT, descriptor-table, fault-entry, diagnostic-stub invocation, diagnostic-stub callable-binding formatting, and controlled-fault-probe planning contracts exist. The accepted M02-005/M02-006 profiles exercise bounded direct COM1 marker writes before `ExitBootServices`, M02-007 validates caller-owned GDT bytes in the EFI guest, and the accepted M02-008 profile executes the reviewed guarded-IST arena layout and lease contract against temporary UEFI memory. The default-off M02-009 profile has QEMU/OVMF evidence for loading and reading back a terminal private GDT/TSS after `OS_SMOKE_PASS`, then emitting the exact terminal transcript through owned COM1. The separate default-off M02-010 profile materializes and reads back the frozen 4,096-byte interrupt-entry page in one temporary LoaderData allocation and frees it before memory-map capture. Neither profile installs an IDT, triggers or handles a fault, or proves post-firmware diagnostics. |
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
| Owned post-firmware diagnostics | The opt-in M02-005 and M02-006 profiles can emit bounded markers directly through COM1 before firmware exit, M02-007 validates GDT bytes in caller-owned guest memory, M02-008 remains a pre-firmware-exit temporary-arena profile, M02-009 is an explicitly pre-exit terminal profile, and M02-010 only materializes, reads back, and frees one LoaderData page before memory-map capture. The live boot path does not yet own serial output after firmware services are gone and panic routing is not live. |
| Allocator and paging | The live EFI path initializes bounded static free-list state from conventional-memory records and emits `BOOT_PAGE_ALLOCATOR_READY`, but it does not yet subtract every reservation, execute a live allocation/free/coalesce cycle, configure page tables, enforce NX/W^X, or provide a managed heap. |
| CPU fault handling | Descriptor and fault plans exist. The default-off M02-009 profile has real-QEMU evidence for loading and reading back its private GDT and TSS before halting, and M02-010 has separate real-QEMU evidence for materializing and reading back—but never executing—its 256 interrupt-entry slots. No live IDT, fault handlers, register capture, guard enforcement, or QEMU fault probe exists. |
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

Optional cumulative pre-`ExitBootServices` owned-COM1, GDT-encoding, and guarded
IST arena profile:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe -EnableM02PanicSafeOwnedCom1Probe -EnableM02OwnedX64GdtEncodingProbe -EnableM02GuardedIstStackAllocationProbe
```

That optional profile rebuilds the EFI/image inputs, preserves the accepted
M02-005 marker pair, M02-006 reporter/evidence pair, and M02-007 encoder marker,
then requires the fixed M02-008 marker exactly once. It forbids every cumulative
failure marker. M02-008 proves only temporary guest-arena allocation, reviewed
layout metadata, stack-range write/readback, lease release, and firmware
`FreePages` before `ExitBootServices`. It does not make guard pages non-present,
enforce NX, execute `lgdt`/`ltr`, install active descriptor state, trigger a
panic or fault, or provide post-`ExitBootServices`, physical-device, provider,
CloudReady, or production evidence.

Optional cumulative M02-009 terminal GDT/TSS development profile:

```powershell
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe -EnableM02PanicSafeOwnedCom1Probe -EnableM02OwnedX64GdtEncodingProbe -EnableM02GuardedIstStackAllocationProbe -EnableM02OwnedGdtTssTerminalProbe
```

That profile adds `TEST_START m02.009`, the exact `CLROS_M02_009_READY`
record, and `TEST_PASS m02.009` to the ordered/unique contract. The guest then
intentionally remains halted, so the runner must terminate QEMU and report
`disposition=runner-terminated`. It uses the real QEMU path directly rather
than the M01 raw-observation shim and rejects an explicit
`-M01RawObservationLog`. `done/step918.md` records the fresh real QEMU/OVMF
acceptance for the exact terminal contract; the implementation is still
pre-`ExitBootServices` and does not prove page permission enforcement,
W^X/NX, an IDT or fault handler, post-firmware diagnostics, hosted .NET,
Kestrel, cloud readiness, or production readiness.

Optional M04-003 span/memory conformance profile:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -BuildEfi -EnableM04IndependentEndianConformanceProbe -EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe
```

The default-false M04-003 switch requires the M04-002 endian switch and cannot
be combined with the terminal M02-009 switch. Its dry-run contract contains 16
required, four ordered, two unique, and four forbidden records, including the
expected exact `CLROS_M04_003_READY` line after M04-002 and before the memory-map
marker. The command above only inspects the profile; it is not boot proof. A
fresh exact-head run recorded in `done/step919.md` built the 59-compile-item,
72-source-authority-input NativeAOT EFI closure and accepted the 16/4/2/4
profile under real QEMU/OVMF. Both M04 guest markers appeared exactly once in
the required order, the returning profile continued to `OS_SMOKE_PASS`, and no
forbidden marker appeared. See `docs/qemu.md` for the executable evidence
contract. This fixed-catalog guest result does not provide a general span or
memory runtime surface, and Gate 4 remains `Fail`.

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
