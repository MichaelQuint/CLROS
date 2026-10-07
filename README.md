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

> Current status: an experimental C# NativeAOT boot/runtime prototype with
> accepted QEMU/OVMF evidence for real firmware exit, owned serial diagnostics,
> and bounded allocation, collection, constructors, and interface dispatch.
> It does not yet run a standalone .NET application or Kestrel. It is not a
> functional general-purpose kernel, not cloud-ready, and not production-ready.

## Progress

**Status snapshot: October 7, 2026, 12:58 a.m. EDT.** Accepted work is recorded
through upstream `master` commit `7489823`; development work is listed separately
below. Evidence links pin the accepted revisions so historical checkouts can
still locate the corresponding records.

Progress is gate-based; issue counts and test counts are not completion
percentages. The goal remains a C#-first NativeAOT operating system for one
configured workload, with .NET 10 applications and mandatory ASP.NET Minimal
API/Kestrel support, followed by verified Azure and AWS deployment.

The [completion gates](docs/completion-gates.md) are the source of truth. In the
[accepted gate ledger](http://localhost:3000/admin/CLROS/src/commit/7489823d0ad6668c76bff444418e252688f7a9ab/docs/completion-gates.md),
only Gate 2 passes; all other numbered gates remain **Fail**. Gate 2 covers the
firmware-visible boot smoke. Separate accepted default-off profiles now prove a
bounded real `ExitBootServices` transition and runtime operations; those results
do not complete the broader firmware, diagnostics, or runtime gates.

### Latest accepted milestones

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
  [Step1053](http://localhost:3000/admin/CLROS/src/commit/2f17dc1995197e305a32adfe5f406fd439db566e/done/step1053.md)
  records the acceptance.
- **Post-exit diagnostics and interface dispatch:** owned COM1 emits the complete
  post-firmware readiness tail. The bounded interface profile executes 15 real
  compiler-generated calls across two receiver types, including covariance,
  contravariance, sealed/virtual resolution, ten cache entries, and five cache
  hits. A same-image null-receiver control rejects before target entry.
  [Step1063](http://localhost:3000/admin/CLROS/src/commit/96c3a7c945c618ef2f267a3cd4208c18cb0de824/done/step1063.md)
  records the accepted integration.
- **Exception-type matching, host-only:** a bounded, allocation-free matcher
  validates retained object/module authority and class hierarchies. Seven
  focused host test groups and independent review passed. This accepted helper
  does not itself execute exceptions in the guest.
  [Step1058](http://localhost:3000/admin/CLROS/src/commit/9e0afe190a6b7661a798f2894562ab9b5a71d106/done/step1058.md)
  records its evidence boundary.

The latest accepted full host suite completed with **3,845 `TEST_START`,
3,846 `TEST_PASS` including the final `host.tests` summary, and zero failures**
at implementation `d5bef46`. The solution build had zero warnings/errors.
Six fresh ordinary QEMU/OVMF captures passed: default, collector churn, first
exit, firmware revocation, constructors, and interface dispatch. Three fresh
controls rejected canary corruption, static-payload corruption, and a null
receiver. These are recorded results from step1063, not new runs for this README
or a claim that every later development commit passed the same suite.

### In development, not yet accepted

| Work | Evidence and remaining boundary at this snapshot |
| --- | --- |
| [Unwind metadata (#89/#574)](http://localhost:3000/admin/CLROS/issues/574) | A development QEMU profile consumes the actual NativeAOT image's unwind metadata and locates compiled methods. The final full host suite is underway; final native/guest acceptance remains pending. Metadata lookup alone is not stack unwinding. |
| [One-frame unwind (#578)](http://localhost:3000/admin/CLROS/issues/578) and [EH clauses (#579)](http://localhost:3000/admin/CLROS/issues/579) | Implementations and focused host reviews are complete; integration depends on accepted unwind metadata. Host tests do not prove restoration of live CPU state or execution of handlers. |
| [First real throw/catch (#582)](http://localhost:3000/admin/CLROS/issues/582) | One development QEMU run threw across a frame into a typed catch and continued to `OS_SMOKE_PASS`. Freshness reconciliation, negative-control evidence, accepted dependencies, the full host gate, and fresh ordinary-wrapper acceptance remain pending. This is not general exception handling. |
| [Post-exit subsystem survival (#67)](http://localhost:3000/admin/CLROS/issues/67) | A retained allocator round trip passes focused host tests. Integration of continuing allocator, descriptor, ACPI, and panic operations after exit is underway; guest acceptance is pending. |

The [live Gitea backlog](http://localhost:3000/admin/CLROS/issues) tracks current
ownership and dependencies. Development evidence and issue closure do not
substitute for the acceptance records and completion gates.

## What Works Today

These capabilities have different evidence scopes. Optional guest profiles are
disabled by default and are not all combined into one continuing kernel boot.

| Area | Current evidence |
| --- | --- |
| Build and EFI image | .NET 10/C# 14 host builds and an explicit no-stdlib NativeAOT EFI artifact; host tools produce the GPT/FAT32 boot image. |
| Default boot | QEMU/OVMF reaches the ordered `OS_BOOT_OK` and `OS_SMOKE_PASS` markers while firmware remains available. `BootSequence.cs` owns phase ordering and `Probes.cs` owns diagnostic toggles. |
| Owned memory and firmware handoff | Separate bounded profiles exercise an owned stack, allocator arena, paging transitions, retained memory-map/ACPI data, real firmware exit, and typed capability revocation. General page reclamation and continuing platform services remain incomplete. |
| Fault and serial diagnostics | Accepted bounded profiles cover owned descriptor/IST state, UD2, page fault, double fault, unexpected-vector panic, and direct COM1 output. Post-exit readiness reporting is accepted; general fault recovery is not. |
| NativeAOT runtime subset | Guest evidence covers bounded fixed-object, byte/long-array and String allocation, checked reference helpers, one-module registration, static roots, nonmoving collection, compiler-generated constructors, and ordinary-instance interface dispatch. This is a limited runtime subset, not the full .NET libraries or GC. |
| Shared utilities | Host-tested ASCII/text, numeric formatting, memory, endian, checksum, and collection helpers; selected fixed conformance catalogs also execute in the EFI guest. |
| PAL, storage, networking, and app groundwork | Host-tested ABI contracts, parser/planner logic, mock PAL tables, disk/package/image tools, protocol frames, and app launch/lifecycle metadata. These do not establish live guest drivers, packet I/O, package loading, or app execution. |
| Evidence tooling | Stable test/serial markers, source and artifact checks, host image tools, QEMU runners, CI dry-run scaffolding, and immutable `done/stepNN.md` records. Executed CI and cloud-provider acceptance remain separate gates. |

## What Is Not Complete

| Area | Missing before claim |
| --- | --- |
| Continuing post-firmware kernel | General firmware-page reclamation, allocator/platform survival, interrupt/timer services, and recovery beyond the accepted bounded or terminal profiles. |
| General runtime correctness | Automatic stack-root scanning, broader allocation/GC support, full throw/catch/finally/rethrow, constructor failure caching, thread statics, and remaining runtime helpers. Bounded dispatch does not support every interface/type shape or concurrent execution. |
| Scheduler and synchronization | Live scheduling, thread lifecycle, blocking waits, hardware timer integration, ThreadPool, `Monitor`, and `Task` support. |
| Live platform and storage | Device discovery, MMIO/interrupt/DMA integration, storage drivers, and post-exit disk-backed manifest/package loading. |
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

The [native workload milestone](http://localhost:3000/admin/CLROS/issues/161)
requires a defined app entry contract, executable mapping and relocation,
a guarded app stack, runtime metadata/root/constructor registration, log
routing, and a verified return/exit status. The planned fixture is tracked by
[compile/package #177](http://localhost:3000/admin/CLROS/issues/177) and
[load/execute/log #178](http://localhost:3000/admin/CLROS/issues/178).
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
.\scripts\run-qemu-smoke.ps1 -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30 -BuildEfi -EnableM02SafeStubProbe -EnableM02CheckedPortIoBindingProbe -EnableM02Com1OwnedPortIoProbe -EnableM02OwnedCom1ReadyProbe -EnableM02PanicSafeOwnedCom1Probe -EnableM02OwnedX64GdtEncodingProbe -EnableM02GuardedIstStackAllocationProbe -EnableM02OwnedGdtTssTerminalProbe
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
