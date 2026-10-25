<p align="center">
  <img src="img/clros_logo.png" alt="CLROS logo" width="500">
</p>

<h1 align="center">CLROS</h1>

<p align="center">
  <strong>A C#-first NativeAOT operating system for minimal cloud workloads.</strong>
</p>

CLROS is an experimental C#-first operating system for VM images containing
configured .NET 11 applications. It uses a restricted Linux x64 compatibility
layer and statically published NativeAOT applications targeting **`linux-musl-x64`**.
First-party kernel, shared-library and application source is C# 14.

The goal is a **small, fully functioning OS that continuously hosts supported
applications**. The first integrated workload combines a NativeAOT Minimal API,
a Blazor Static WebAssembly frontend calling its API endpoints, and persistent
SQLite data through `Microsoft.Data.Sqlite`, including restart and recovery.
Finite guest tests verify progress toward that system; they are not the final
service model. The planned public GitHub update and live cloud demo app will
showcase the working OS after the corresponding capabilities are qualified.

**The pinned Hello and bounded .NET runtime workloads already run inside CLROS.
Real Kestrel also serves external HTTP in qualified finite guest profiles.**
The complete local single-app Gate 14 remains open; CLROS is not cloud-ready or
production-ready.

## Current status — October 23, 2026

[PR #790](http://192.168.4.35:3000/admin/CLROS/pulls/790) is merged to master at
`82656076`. The live tracker confirms #748, #225 and the finite #737 integration
closed, following the fourteen predecessor closures reviewed in
[step1481](done/step1481.md).

| Capability | Accepted scope |
| --- | --- |
| Boot and firmware exit | Ordinary `VerifiedExit` leaves boot services, checks owned subsystems and halts. [Step1094](done/step1094.md). Application probes are explicit, default-off profiles. |
| .NET 11 applications | Pinned Hello, focused GC/ThreadPool, RuntimeSmoke and RuntimeMatrix execute in single-vCPU QEMU/OVMF guests. [Step1307](done/step1307.md). |
| TCP and Linux sockets/epoll | Finite retained NIC/TCP lifetimes and unchanged upstream synchronous/asynchronous socket fixtures. [Step1332](done/step1332.md), [step1351](done/step1351.md). |
| Kestrel health/configuration/logging | Exact external HTTP entities, startup, vectors, endpoint policy and finite UTC/Date behavior. [Step1380](done/step1380.md), [step1389](done/step1389.md), [step1405](done/step1405.md), [step1419](done/step1419.md). |
| Repeated service and memory controls | Original v1 lifetime and dedicated memory-fault restoration controls; corrected awaited host-runner qualification. [Step1479](done/step1479.md), [step1481](done/step1481.md). |
| Quota termination and async churn | Original signal-race Smoke, quota at 256/512 MiB, positive configured V2 and 20 ancestor profiles. [Step1556](done/step1556.md). |

The latest accepted unfiltered full-host run at `28a79996` passes **6,258 checks
across 847 fixtures (753 Fast, 94 Native)**, with unchanged inputs and zero reuse.
This is retained acceptance evidence, not a test run of this documentation update.
Original Smoke (`32c14ccd`) and quota/V2 (`20e665ad`) keep their own source,
native and producer identities. Failed originals remain failed.

Quota proves offending-workload exit 99 and cleanup after physical denial;
V2 separately completes managed cancellations and drains 59 lifetimes; Smoke
proves continuous cancelled churn with the directed-signal/readiness race.
The **36 MiB dynamic pool is shared**; the 16 MiB GC limit and declared 20 MiB
native headroom are not independently enforced partitions.

Only numbered Gate 2 passes. [Completion gates](docs/completion-gates.md)
and the [Kestrel ledger](docs/kestrel.md) define the exact accepted boundaries.
Historical .NET 10 receipts retain their original toolchains; the pinned .NET 11
migration is recorded in [step1225](done/step1225.md).

## NativeAOT-first architecture

The selected route uses the existing **`linux-musl-x64`** target and a controlled
static publish recipe. CLROS implements the required Linux x64 application
syscalls in C#; pinned upstream NativeAOT runtime, musl and native payload
libraries stay with the application. Application GC and exception handling remain
upstream responsibilities. A Linux RID alone does not make an executable
freestanding or prove CLROS compatibility. Step1225 qualifies the .NET 11 publish
recipe and records native dependencies and observed runtime/ABI requirements;
the guest profiles above qualify only their measured CLROS service subsets.

First-party kernel, shared library and app source committed here is C#.
`src/OS` retains an explicit no-stdlib NativeAOT EFI build. Upstream native
components may be built externally and packaged with applications; reference
repositories are not imported into the first-party build. The stable `OSHost_*`
ABI remains separate from Linux register, errno and descriptor semantics.

CLROS is designed as a single-workload appliance with no guest shell, terminal,
SSH or command-execution administration endpoint. Core OS services must remain
reusable by the Linux adapter and the future custom PE path. Compatibility grows
only for the supported .NET workload contract; general Linux or Windows coverage
is not a product goal. See the [core/adapter contract](docs/architecture.md#core-services-and-application-adapters).

The initial guest target is x86-64 QEMU/OVMF with one vCPU, an immutable application
package and one configured HTTP endpoint. CoreCLR/framework-dependent hosting,
SMP and cloud-provider qualification follow the first working NativeAOT service.
The [workload profiles and roadmap](docs/workload-profiles.md) are canonical;
[architecture](docs/architecture.md) and [PAL](docs/pal.md) explain the boundaries.

Keep kernel C# deliberately restricted: new kernel GC, exception or runtime
features need a demonstrated product requirement. Preserve upstream guest
runtime behavior and reusable OS services beneath the Linux adapter. Automate
compiler-dependent checks so normal supported app deployment needs no manual
disassembly or per-app kernel changes. Judge progress by complete guest static-file,
persistent-upload, SQLite-recovery and isolation scenarios alongside focused tests.
These are [development guardrails](AGENTS.md#runtime-and-qualification-guardrails)
and [acceptance targets](docs/workload-profiles.md#product-scenarios-and-verification),
not additional capabilities claimed by this documentation update.

## Next implementation

1. Complete [#226](http://192.168.4.35:3000/admin/CLROS/issues/226) evidence
   packaging, then [#227](http://192.168.4.35:3000/admin/CLROS/issues/227) local
   Gate 14 acceptance. The finite #737 integration is closed; preserve its
   accepted evidence and refresh live ownership before claiming successor work.
2. Finish the open host repairs: [#780](http://192.168.4.35:3000/admin/CLROS/issues/780)
   tracker conflict protection and [#781](http://192.168.4.35:3000/admin/CLROS/issues/781)
   → [#782](http://192.168.4.35:3000/admin/CLROS/issues/782) FAT selection/traversal
   bounds. The FAT repairs precede #769 disk-fixture acceptance.
3. Follow the [disk-backed static-file roadmap](docs/workload-profiles.md#disk-backed-static-file-roadmap-2026-10-18)
   for JSON endpoints and read-only HTML/CSS/JS from a real guest disk. Staged
   files and demand disk reads have separate acceptance; this feature is planned.
4. Qualify fault containment and two isolated local apps before provider work.
   Measured TCP/audit/logging optimization and host Roslyn/ABI tooling retain
   their later dependency order.

Use the [managed issue manifest](.gitea/backlog/clros-issues.json) for exact
acceptance and dependencies, and the [LAN tracker](http://192.168.4.35:3000/admin/CLROS/issues)
for live state and ownership. Older planning text is not implementation evidence.

## Build and test

Start with [host requirements](docs/host-requirements.md) for the selected .NET 11
SDK, native linker and qualified Visual Studio setup.
Use that document's current activation instructions before running commands;
the preceding pinned .NET 10 recovery remains historical context. EFI publishing
requires the documented Windows native toolchain, while Linux payload publishing
uses its separate pinned builder. A successful host build does not qualify either
guest execution or a toolchain migration.

Run from the repository root:

```powershell
# Build once, then run the default fast lane.
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project tests/OS.HostTests --no-build

# List or select focused checks; full lane admits native fixtures too.
dotnet run --project tests/OS.HostTests --no-build -- --list
dotnet run --project tests/OS.HostTests --no-build -- --lane full --filter SchedulerTimerTests

# Full host acceptance requires an unfiltered run on stable inputs.
dotnet run --project tests/OS.HostTests --no-build -- --lane full

# Repository and ABI checks.
dotnet run --project tools/OS.Tooling -- repo check
dotnet run --project tools/OS.Tooling -- contracts check
git diff --check
```

Rebuild after source or test metadata changes before using `--no-build`.
Keep inputs stable during qualification; consult the host guide for cancellation,
checkpoint reuse and receipt rules. Fast success emits `TEST_PASS host.tests.fast`;
only an unfiltered full run can emit `TEST_PASS host.tests`.

EFI publication and smoke planning use compiled C# tooling:

```powershell
dotnet run --project tools/OS.Tooling -- efi build
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
```

Use [EFI build](docs/efi-build.md) and [QEMU](docs/qemu.md) for real guest commands,
profile prerequisites and evidence capture. Dry runs and host tests are not boot
proof. Guest acceptance requires the actual image, QEMU/OVMF execution, serial
capture and the profile's exact success, failure and restoration checks.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/OS` | Explicit no-stdlib NativeAOT EFI shell and live adapters, included in `CLROS.slnx` under stricter build rules. |
| `src/OS.Kernel` | Kernel services, boot, drivers, PAL, networking and app contracts. |
| `src/OS.Std.NoRuntime.Shared` | Host project linking the audited source under `std/no-runtime/shared`. |
| `std/no-runtime/shared` | Shared no-runtime utility implementations; do not duplicate or casually move these EFI inputs. |
| `apps` | Application fixtures and workload examples. |
| `tools/OS.Tooling` | C# build, verification, smoke, evidence and backlog commands. |
| `tools` | Other host-side image, package and manifest tools. |
| `tests/OS.HostTests` | Explicit test discovery, fast/native lanes and machine-readable receipts. |
| `docs` / `done` | Canonical documentation / immutable evidence history. |
| `references` | Upstream source references, excluded from first-party build and acceptance evidence. |
| `work` / `artifacts` | Design work / generated outputs. |

## Documentation and contributions

Read [AGENTS.md](AGENTS.md) before changing code. Keep batches small, prefer
simple C# status-based APIs, preserve other agents' work and use the ticket's
exact source references. Shared helpers belong under `std/no-runtime/shared`.
[BootSequence.cs](src/OS.Kernel/Boot/BootSequence.cs) owns boot ordering;
[Probes.cs](src/OS.Kernel/Diagnostics/Probes.cs) keeps dangerous probes off by default.

- [Documentation map](docs/documentation-map.md): find the canonical source for each topic.
- [Boot](docs/boot.md), [runtime limits](docs/nativeaot-nostdlib-limits.md) and [drivers](docs/drivers.md): current low-level contracts.
- [App model](docs/app-model.md), [networking](docs/networking.md) and [Kestrel](docs/kestrel.md): workload requirements and remaining acceptance.
- [Reference map](docs/reference-map.md) and [license audit](docs/license-audit.md): source reuse and provenance.
- [Issue workflow](docs/issue-labels.md) and [Windows handoff](docs/windows-handoff.md): ownership, evidence and machine transfer.

Every meaningful milestone needs a `done/stepNN.md` record tying source, tests,
native review and guest evidence to its exact scope. Test and serial markers
include `TEST_START`, `TEST_PASS`, `TEST_FAIL`, `OS_BOOT_OK`, `OS_SMOKE_PASS` and
`OS_SMOKE_FAIL`. A marker, issue closure or test count alone is not a completed
runtime, network service or security boundary.

## Security direction

The intended appliance has manifest-bound immutable workloads and explicit
resource/endpoint permissions, with no general shell, SSH or remote administration
surface by default. This reduces exposed functionality but does not eliminate
application vulnerabilities or establish superiority over hardened Linux or
Windows. General process isolation and production security remain unqualified.
See [security](docs/security.md) for the policy and required protection evidence.
