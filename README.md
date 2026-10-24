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

Only Gate 2 passes. [Completion gates](docs/completion-gates.md)
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

## Current implementation batch

The approved next milestone serves an actual Standalone Blazor WebAssembly app
from a FAT32 webroot through the NativeAOT MinimalApi. Persistent logs/uploads
and SQLite follow the read milestone. [Step1558](done/step1558.md) records current
host-only development: tracker/FAT repairs, reproducible disk fixtures and the
published Blazor candidate. [Step1559](done/step1559.md) separates the static
server from accepted smoke inputs, retains the Linux research trace and adds a
14-check host-tested continuing block owner. The final separate-app Linux
reference now passes61 HTTP observations over45 real Blazor assets and actual
SIGTERM/exit0 cleanup. The confined FAT reader and generation-bound disk waits
now have focused host coverage. The isolated native integration reserves a
separate disk at PCI0:7, retains its DMA/queue lifetime and records completed
sector bytes; it still requires native review and complete guest qualification.
Continuing guest disk reads and disk-backed HTTP are
not qualified yet. The branch keeps accepted step1556 evidence unchanged.
The isolated read-service checkpoint is recorded in [step1564](done/step1564.md).
[Step1566](done/step1566.md) records the first verified disk EFI publication,
the failed pre-exit guest and its conditional retained-range repair. Three fresh
staged-file guests pass their complete consumers at `5ceeb0c2`; those files are
served from the staged package. A fresh unfiltered full and actual disk-backed
guest qualification remain required. The earlier failed full remains failed.
[Step1567](done/step1567.md) retains a later guest that reaches firmware exit
and the NIC before memory admission rejects its 65 static mappings. The disk
profile's construction inventory now preserves room for those mappings and all
64 mutable regions. Focused checks pass; fresh disk execution remains pending.
[Step1568](done/step1568.md) identifies the next mount rejection: preserved boot
directory entries have all-zero timestamps. The read contract now treats that
exact pair as unspecified metadata; malformed nonzero timestamps still reject.
Fresh guest qualification and the complete disk consumer remain pending.
[Step1569](done/step1569.md) records r07 mounting and attaching the disk, then
failing its first deferred stat because the syscall gate rejected that call's
Blocked result. Six disk calls now have profile-specific blocking admission,
with real gateway/thread integration controls. Disk-specific startup/network/
runner value checks are in development; fresh execution and complete joins remain.
[Step1570](done/step1570.md) retains r08 at `38ba4446`: 59 file calls, 35
disk completions, seven positive read windows and exit42 reach cleanup markers.
Its original outer command still failed pending the complete consumer. New
value inspection joins all 62 post-bootstrap sector copies to that original
image and their owning calls. Original runner/native/packet integration is now
a candidate with 30 focused checks passing unchanged; a fresh source-matched
capture remains required.

SQLite follow-ups [#791](http://192.168.4.35:3000/admin/CLROS/issues/791) and
[#792](http://192.168.4.35:3000/admin/CLROS/issues/792) use direct
`Microsoft.Data.Sqlite`. EF Core's separate NativeAOT constraints are outside
that engine/provider qualification.

## Next implementation

[Step1577](done/step1577.md) adds complete value-only disk/static scheduler,
file and sector replay. All2973 original diagnostic calls,13 reaps,11 disk
operations and12 sectors match. The original018r03 command remains failed;
original complete HTTP/browser/native/packet acceptance and batch gates remain
pending. No issue, write/durability or SQLite boundary advances.

[Step1576](done/step1576.md) records the first five-response disk HTTP diagnostic
at `e5a5c1c0`: NativeAOT server readiness, published host page/nonce from disk,
owned SIGTERM/exit0 and cleanup. The original outer command remains failed
pending complete validation; full framework/browser and batch gates stay open.

[Step1572](done/step1572.md) adds the default-off disk static-server diagnostic:
original indexed disk package, completion-time file observations and a distinct
post-listen read boundary. Full disk HTTP/Blazor validation remains unfinished.

[Step1571](done/step1571.md) records three fresh complete mechanical disk
captures at `c66befd5`, spanning DiskA/DiskB/DiskA. Each passes the owned native,
source, image, file-window, runner and packet joins for 59 calls and 62 demand
sector copies. Cancellation/error controls, full disk HTTP/Blazor and stable
batch acceptance remain unfinished.

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
