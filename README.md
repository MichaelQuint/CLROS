<p align="center">

  <img src="img/clros_logo.png" alt="CLROS logo" width="500">
</p>

<h1 align="center">CLROS</h1>

The bounded modern RX/header/ARP batch is [qualified](done/step1164.md):
4,441 full-host checks, linked native review, a 194-record ARP guest capture,
independent request/reply libpcap and thirteen ancestor regressions pass. The
default-off profile receives one matching reply and checks supplied-tick neighbor
cache controls. The [prior driver batch](done/step1162.md) remains qualified.
Application execution, continuing storage/networking and Gate7 remain incomplete.
Visual Studio 2026 Insiders and dotnet host builds pass.

<p align="center">
  <strong>A C#-first NativeAOT operating system for minimal cloud workloads.</strong>
</p>

<p align="center">
  .NET 10 applications are the long-term first-class workload target. ASP.NET
  Minimal API and Kestrel support is a mandatory milestone, not a demo claim.
</p>

> Current status: an experimental C# NativeAOT boot/runtime prototype with
> accepted QEMU/OVMF evidence for real firmware exit, owned serial diagnostics,
> and bounded allocation, collection, constructors, interface dispatch, and typed catch.
> It does not yet run a standalone .NET application or Kestrel. It is not a
> functional general-purpose kernel, not cloud-ready, and not production-ready.

## Progress

The #646-#648 bounded physical-frame dispatch batch is [qualified](done/step1171.md):
4,488 full-host checks, final native review, three byte-identical 88-record frame
guests and seven ancestor regressions pass at frozen source `7168906`. Its separate
default-off profile executes one middle-frame cleanup, an outer typed catch and
compiled continuation using distinct caller contexts and the original exception.
General EH, constructor cached failures, #90 and Gate4 remain incomplete.

The #650-#652 constructor-failure batch has [actual transfer calibration](done/step1174.md).
Its diagnostic remains default-off. Both throws and compiled continuation pass
development capture checks; full-host and stable batch qualification remain pending.

The bounded frame-search/type-predicate/unhandled batch is [qualified](done/step1169.md):
4,473 full-host checks, native review, three 73-record expected-terminal guests
and six ancestor captures pass with independent source/process/image association.
The default-off profile stops after three frames and one nonmatching catch.
General multi-frame dispatch, general EH, constructor cached failures, #90 and
Gate4 remain incomplete.

The bounded typed-search/two-cleanup EH batch is [qualified](done/step1167.md):
4,456 full-host checks, reviewed native controls, three byte-identical 89-record
guests and five ancestor regressions pass. The annotation/digest follow-up has
six focused checks and three fresh guests with the same EFI bytes. General EH,
constructor cached failures, #90 and Gate4 remain incomplete.

The bounded GPT validation/backup/EFI partition batch is [accepted](done/step1150.md):
4,336 full-host checks, native review and exact 207/208-record intact/damaged-primary
guest captures pass. EFI partition location is established; manifest/package loading,
continuing storage and Gate 7 remain incomplete.

A one-attempt live block adapter and protective LBA0 validation are
[accepted](done/step1147.md): 4,316 full-host checks, native ABI/stack review and
158/138/122/79-record contract/read/queue/ordinary guest captures pass. The
existing block interface returns the checked 512-byte read through a closed
constrained call, then validates the protective MBR. Guest manifest/package
loading, continuing storage and Gate 7 remain incomplete.

The bounded APIC/MSI-X batch is [accepted](done/step1137.md): 4,277 full-host
checks, strict native review, one real MSI-X queue completion and an ordinary
boot regression pass. This completes the 21 keyed M05 child scopes; broader
platform services remain incomplete. See [drivers](docs/drivers.md) for scope.

Bounded rethrow and post-firmware UC/WC/WB mapping are qualified in
[step1120](done/step1120.md): the resumed full host run passes 4,146 checks and
six fresh QEMU profiles pass with independent image association. General EH,
general device support and workload execution remain incomplete. Host runs support
validated [checkpoint resume](docs/host-requirements.md#resume-an-interrupted-or-failed-host-run)
and finish the fast phase before admitting native work.

Host tests now use explicit C# discovery and a fast default lane:
`dotnet run --project tests/OS.HostTests`. Use `-- --list` or
`-- --filter <fixture-or-case>` for selection; full host execution requires
`-- --lane full`. See [runner commands and migration status](docs/host-requirements.md#c-host-test-runner).
Build, verification, smoke, CI, and evidence commands now use the .NET 10
`OS.Tooling` console application. A fast pass is not full host or guest acceptance.
The completed migration and [latest qualification](done/step1098.md) passed
3,072 fast checks in 40.3 seconds and all 4,120 host checks in 23 minutes
18 seconds (previously 93 minutes 28 seconds). All 39 declared real guest
captures passed in [step1097](done/step1097.md); their production inputs remain
unchanged after the test-only setup optimization.
Shared reviewed expectations live in `tools/OS.Tooling/EfiContracts` partials;
stale compiled inputs are rejected before execution and checked again afterward.

**Status snapshot: October 8, 2026.** The preceding accepted runtime implementation is recorded through
`master` commit `1e6a8dbd11e3220c1ca433b72d57e418cdc86b05`, including [step1075](done/step1075.md).
Evidence records retain their original tested revisions; acceptance does not
extend automatically to later source changes. Development has resumed on the
new Windows host, with a separately validated SDK/toolchain migration below.
The Windows handoff remains the preserved record of the preceding clean stop.

The development toolchain now qualifies SDK `10.0.401` and NativeAOT
compiler/runtime pack `10.0.12` for the finite profile matrix in
[step1078](done/step1078.md): the full host gate, ten native/guest profiles and
three current-image rejection controls pass. [Step1077](done/step1077.md)
retains the preceding SDK-only checkpoint. This does not establish a hosted
.NET runtime or expand the accepted runtime feature set.

Progress is gate-based; issue counts and test counts are not completion
percentages. The goal remains a C#-first NativeAOT operating system for one
configured workload, with .NET 10 applications and mandatory ASP.NET Minimal
API/Kestrel support, followed by verified Azure and AWS deployment.

The [completion gates](docs/completion-gates.md) are the source of truth. Only Gate 2 passes; all other numbered gates remain **Fail**. Ordinary boot now requires bounded verified firmware exit, while explicit diagnostic profiles retain their separate contracts. This does not complete the broader firmware, diagnostics or runtime gates, or establish application hosting.

### Latest accepted milestones

- **Ordinary verified firmware exit:** [step1094](done/step1094.md) accepts #68/#594 at tested source `3c986e8`. The full host gate passes 3,903 groups plus its summary; three fresh ordinary 79-record boots and five diagnostic regressions pass with reviewed native and image evidence. Ordinary success follows the original owned allocator, copied ACPI and descriptor checks, then halts. Probe defaults stay false.
- **Real firmware exit and collector churn:** the first-exit profile leaves UEFI
  boot services, adopts owned page tables and CPU descriptors, checks retained
  state, and reports through owned COM1 before halting. A separate pre-exit
  collector profile completes 192 rounds, 476 direct allocations/reclamations,
  and 198 collections using explicit and retained-static roots.
  [Step1051](done/step1051.md) records the accepted scope.
- **Firmware-access revocation and static constructors:** all eight retained
  firmware capabilities deny access before entering their backends after exit.
  One eager and seven lazy compiler-generated constructors execute once, with
  tested same-thread reentry and partial-static behavior. Exception caching and
  rethrow remain separate work.
  [Step1053](http://192.168.4.35:3000/admin/CLROS/src/commit/2f17dc1995197e305a32adfe5f406fd439db566e/done/step1053.md)
  records the acceptance.
- **Post-exit diagnostics and interface dispatch:** owned COM1 emits the complete
  post-firmware readiness tail. The bounded interface profile executes 15 real
  compiler-generated calls across two receiver types, including covariance,
  contravariance, sealed/virtual resolution, ten cache entries, and five cache
  hits. A same-image null-receiver control rejects before target entry.
  [Step1063](http://192.168.4.35:3000/admin/CLROS/src/commit/96c3a7c945c618ef2f267a3cd4208c18cb0de824/done/step1063.md)
  records the accepted integration.
- **Exception-type matching, host-only:** a bounded, allocation-free matcher
  validates retained object/module authority and class hierarchies. Seven
  focused host test groups and independent review passed. This accepted helper
  does not itself execute exceptions in the guest.
  [Step1058](http://192.168.4.35:3000/admin/CLROS/src/commit/9e0afe190a6b7661a798f2894562ab9b5a71d106/done/step1058.md)
  records its evidence boundary.
- **Current-image unwind metadata (#89/#574):** the guest validates retained
  runtime-function/unwind metadata and locates actual compiled methods through
  the original module owner. This is metadata lookup, not live stack unwinding.
  [Step1071](done/step1071.md) records final host, native and three-profile guest
  acceptance.
- **Exception prerequisites, host-only (#578/#579):** accepted helpers recover
  one caller context from admitted stack/metadata bytes and decode bounded
  NativeAOT EH clauses. They do not themselves restore CPU registers or execute
  handlers. [Step1059](done/step1059.md) and [step1061](done/step1061.md) preserve
  their focused evidence and original development boundaries; their metadata
  dependency is now accepted.
- **Owned subsystems after exit (#67) and verifier optimization (#584):** the
  positive guest performs and restores an original-allocator operation, reads
  original copied ACPI and checks installed descriptor/canary identities. A
  separate installed page-fault control reports through owned COM1 and halts.
  [Step1074](done/step1074.md) accepts this bounded scope and the included
  verifier path-walk optimization; its measured improvement is helper-only.
- **First bounded compiler throw/catch (#582):** one real throw crosses a frame
  into a typed base-class catch and resumes its compiled continuation with the
  original rooted exception. The same-image invalid-local-RIP check rejects
  through the production admission predicate before selection, without changing
  live context. [Step1075](done/step1075.md) records final acceptance.
- **Bounded compiler finally (#589):** one inner cleanup runs before the typed
  catch and compiled continuation, preserving the original exception and
  validating its parent-stack side effect. [Step1088](done/step1088.md) accepts
  the reviewed native image and fresh ordinary guest capture.
- **Bounded compiler rethrow (#596):** the same rooted exception reaches an
  outer typed catch and compiled continuation after the original cleanup and
  inner catch. [Step1120](done/step1120.md) accepts this finite profile; general
  EH and arbitrary rethrow remain incomplete.
- **Post-firmware physical mapping (#94/#598):** an owned scratch leaf changes
  through UC/WC/WB with required invalidation, serialization and neighbor
  preservation. Step1120 accepts this bounded primitive, without live devices.

The bounded compiler-finally acceptance full host suite passed at implementation `3bfa349`, with
**3,898 `TEST_START`, 3,899 `TEST_PASS` including `host.tests`, and zero failures**.
The clean solution build and four fresh ordinary captures passed: default,
collecting GC, bounded throw/catch, and bounded finally/catch, with 21/70/82/84
application records. [Step1088](done/step1088.md) binds the guest source
`736d10d`, native images, complete transcripts and disk evidence to the later
host source and its test-only repairs. Step1075 preserves the earlier catch
acceptance, step1074 the separate owned-subsystem acceptance, and step1063 the
earlier runtime profiles and controls. No broad kernel or runtime gate is promoted.

The [live Gitea backlog](http://192.168.4.35:3000/admin/CLROS/issues) tracks current
ownership and dependencies. Development evidence and issue closure do not
substitute for the acceptance records and completion gates.

## What Works Today

These capabilities have different evidence scopes. Optional guest profiles are
disabled by default and are not all combined into one continuing kernel boot.

| Area | Current evidence |
| --- | --- |
| Build and EFI image | .NET 10/C# 14 host builds and an explicit no-stdlib NativeAOT EFI artifact; host tools produce the GPT/FAT32 boot image. |
| Default boot | Ordinary `VerifiedExit` completes the accepted bounded firmware-exit and owned-subsystem checks before terminal success; explicit `Diagnostic` retains firmware-visible profiles. `BootSequence.cs` owns phase ordering and `Probes.cs` owns diagnostic toggles. |
| Owned memory and firmware handoff | Separate bounded profiles exercise an owned stack, allocator arena, paging transitions, retained memory-map/ACPI data, real firmware exit, and typed capability revocation. Bounded post-exit allocator use, copied-ACPI readback and installed descriptor/canary checks are accepted; general page reclamation and continuing platform services remain incomplete. |
| Fault and serial diagnostics | Accepted bounded profiles cover owned descriptor/IST state, UD2, page fault, double fault, unexpected-vector panic, and direct COM1 output. Post-exit readiness reporting is accepted; general fault recovery is not. |
| NativeAOT runtime subset | Guest evidence covers bounded fixed-object, byte/long-array and String allocation, checked reference helpers, one-module registration, static roots, nonmoving collection, compiler-generated constructors, and ordinary-instance interface dispatch. Current-image unwind metadata is accepted; one-frame restoration and EH-clause helpers retain their bounded host contracts, and one separate profile executes a real throw, typed catch and compiled continuation. This is a limited runtime subset, not the full .NET libraries, GC or exception runtime. |
| Shared utilities | Host-tested ASCII/text, numeric formatting, memory, endian, checksum, and collection helpers; selected fixed conformance catalogs also execute in the EFI guest. |
| Bounded platform and Virtio | Separate accepted diagnostics cover retained ACPI, a polled clock, UC mappings, PCI/BAR access, DMA lifetime, modern Virtio negotiation, one polling TX completion and one MSI-X TX completion. These are finite single-BSP profiles, not continuing device services or packet-delivery proof. |
| Bounded storage | Accepted terminal diagnostics cover one-sector block reads, protective LBA0, primary/backup GPT and EFI partition selection, plus bounded FAT chains, a cross-cluster VFAT name and one fragmented known-file CRC32. Each read has a fresh negotiated lifetime; manifest/package loading and continuing storage remain incomplete. |
| PAL, networking, and app groundwork | Host-tested ABI contracts, parser/planner logic, mock PAL tables, package/image tools, protocol frames, and app launch/lifecycle metadata. These do not establish continuing drivers, packet I/O, package loading, or app execution. |
| Evidence tooling | Stable test/serial markers, source and artifact checks, host image tools, QEMU runners, CI dry-run scaffolding, and immutable `done/stepNN.md` records. Executed CI and cloud-provider acceptance remain separate gates. |

## What Is Not Complete

| Area | Missing before claim |
| --- | --- |
| Continuing post-firmware kernel | General firmware-page reclamation, continuing allocator/platform services, interrupt/timer services, and recovery beyond the accepted bounded or terminal profiles. |
| General runtime correctness | Automatic stack-root scanning, broader allocation/GC support, full throw/catch/finally/rethrow, constructor failure caching, thread statics, and remaining runtime helpers. Bounded dispatch does not support every interface/type shape or concurrent execution. |
| Scheduler and synchronization | Live scheduling, thread lifecycle, blocking waits, hardware timer integration, ThreadPool, `Monitor`, and `Task` support. |
| Live platform and storage | Broader device coverage, continuing MMIO/interrupt/DMA services, storage drivers, and post-exit disk-backed manifest/package loading. |
| Live networking | Guest packet RX/TX, DHCP/DNS, working TCP connections/listeners, and externally observed HTTP traffic. |
| App execution and PAL | A live app entry contract, executable loading/relocation/protection, app runtime registration, output routing, verified exit status, and working platform services for the supported app target. |
| Hosted .NET and Kestrel | No standalone .NET 10 application or ASP.NET Minimal API server runs in the guest. The full Kestrel milestone below remains mandatory. |
| Cloud and production | Azure/AWS import and boot evidence, production hardening, update/rollback, supervisor/isolation, and completed CI/security gates. Local QEMU results are not cloud-provider proof. |

## Path To A NativeAOT Hello World

CLROS already executes NativeAOT-compiled C# boot and diagnostic code. It has
**not yet loaded and run a separate .NET 10 console application**, including
Hello World. The current image uses a deliberately small no-stdlib runtime;
that does not establish standard `Console.WriteLine` or general .NET library
compatibility.

The [native workload milestone](http://192.168.4.35:3000/admin/CLROS/issues/161)
requires a defined app entry contract, executable mapping and relocation,
a guarded app stack, runtime metadata/root/constructor registration, log
routing, and a verified return/exit status. The planned fixture is tracked by
[compile/package #177](http://192.168.4.35:3000/admin/CLROS/issues/177) and
[load/execute/log #178](http://192.168.4.35:3000/admin/CLROS/issues/178).
Its current dependency chain includes runtime correctness and live storage.
A NativeAOT app does not require a JIT, but it still needs a compatible runtime,
library subset, and platform boundary. No console-app completion date is claimed.

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
dotnet run --project tools/OS.Tooling -- repo check
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
dotnet run --project tools/OS.Tooling -- ci smoke -DryRun
dotnet run --project tools/OS.Tooling -- efi build
git diff --check
```

Real QEMU smoke on the current Windows host:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Optional cumulative pre-`ExitBootServices` owned-COM1, GDT-encoding, and guarded
IST arena profile:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe -EnableM02PanicSafeOwnedCom1Probe -EnableM02OwnedX64GdtEncodingProbe -EnableM02GuardedIstStackAllocationProbe
```

That optional profile rebuilds the EFI/image inputs, preserves the accepted
M02-005 marker pair, M02-006 reporter/evidence pair, and M02-007 encoder marker,
then requires the fixed M02-008 marker exactly once. It forbids every cumulative
failure marker. M02-008 uses a temporary six-page UEFI LoaderData arena and
does not install a TSS, IST, GDT, or IDT. It proves only temporary guest-arena
allocation, reviewed layout metadata, stack-range write/readback, lease release,
and firmware
`FreePages` before `ExitBootServices`. It does not make guard pages non-present,
enforce NX, execute `lgdt`/`ltr`, install active descriptor state, trigger a
panic or fault, or provide post-`ExitBootServices`, physical-device, provider,
CloudReady, or production evidence.

Optional cumulative M02-009 terminal GDT/TSS development profile:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe -EnableM02PanicSafeOwnedCom1Probe -EnableM02OwnedX64GdtEncodingProbe -EnableM02GuardedIstStackAllocationProbe -EnableM02OwnedGdtTssTerminalProbe
```

That profile adds `TEST_START m02.009`, the exact `CLROS_M02_009_READY`
record, and `TEST_PASS m02.009` to the ordered/unique contract. The guest then
intentionally remains halted, so the runner must terminate QEMU and report
`disposition=runner-terminated`. It uses the real QEMU path directly rather
than the M01 raw-observation shim and rejects an explicit
`-M01RawObservationLog`.
`done/step918.md` records the fresh real QEMU/OVMF acceptance
for the exact terminal contract; the implementation is still
pre-`ExitBootServices` and does not prove page permission enforcement,
W^X/NX, an IDT or fault handler, post-firmware diagnostics, hosted .NET,
Kestrel, cloud readiness, or production readiness.

Optional M04-003 span/memory conformance profile:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -BuildEfi -EnableM04IndependentEndianConformanceProbe -EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe
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
dotnet run --project tools/OS.Tooling -- qemu smoke -BuildEfi -EnableAcpiPhysicalDiscoveryProbe -MarkerPreset acpi -EfiArtifact artifacts/efi/BOOTX64-ACPI.EFI -Image artifacts/images/os-acpi.raw -SerialLog artifacts/logs/qemu-smoke-acpi.log -SummaryLog artifacts/logs/qemu-smoke-acpi.summary.log -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
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
| `tools/OS.Tooling` | C# build, verification, smoke, evidence, and repository commands. |
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
- [Windows development-machine handoff](docs/windows-handoff.md) - preserve source, evidence and tracker access, then verify the destination.
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
