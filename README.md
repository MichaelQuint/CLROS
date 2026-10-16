<p align="center">
  <img src="img/clros_logo.png" alt="CLROS logo" width="500">
</p>

<h1 align="center">CLROS</h1>

<p align="center">
  <strong>A C#-first NativeAOT operating system for minimal cloud workloads.</strong>
</p>

CLROS is an experimental operating system for VM images containing configured
.NET 11 applications and the services they need. The first application milestone
is one real NativeAOT ASP.NET Minimal API serving `GET /healthz` through Kestrel.
Hardware-protected isolation for multiple applications follows.

**The unchanged pinned .NET11 NativeAOT Hello and bounded runtime workloads now
run inside CLROS.** [Step1307](done/step1307.md) qualifies the thread/runtime
batch. Guest Kestrel remains incomplete.
CLROS is not cloud-ready or production-ready.

[Step1225](done/step1225.md) qualifies the pinned .NET 11 OS/tooling migration and
fresh Linux-host application payloads. C# remains 14. Historical .NET 10 results
retain their original identities; only the explicitly rerun profiles have current
compiler and guest evidence.

## Current accepted evidence

The latest qualification is [step1332](done/step1332.md): **5,341 full-host checks
across 700 fixtures (94 Native), three strict TCP captures and 20 fresh affected
ancestors**, with original native/source/producer joins. The default-off finite
network service retains Virtio-net queues through ARP/ICMP and real TCP ownership,
handshake, window, retry, close/reset and 240-second TIME-WAIT cases. A real
cooperative worker advances throughout and exits/reaps before checked cleanup.
Linux sockets, epoll and guest Kestrel remain incomplete.

[Step1307](done/step1307.md) separately qualifies **5,202 full-host checks
across 687 fixtures (94 Native), 44 strict captures across 32 thread/runtime profiles
and 24 fresh affected ancestors**, with original native/ABI/stack review and exact
source/object/EFI/package/transcript joins. Three retained strict GC-stress
repetitions and a separately identified raw-state diagnostic prove the bounded
exit-request/GC interaction. Accepted scope includes mechanical waits/TLS/Linux
threads/signals and the exact unchanged pinned Hello, focused GC/ThreadPool,
original RuntimeSmoke and RuntimeMatrix workloads on single-vCPU QEMU/OVMF.
This does not qualify arbitrary Linux apps, general kernel EH or Kestrel.

[Step1225](done/step1225.md) separately retains the .NET 11 migration acceptance:
4,783 full-host checks and 29 audited strict QEMU captures, including an expected
unhandled negative and three timer reruns. Fresh static Hello, runtime exerciser
and Minimal API payloads pass its separate Linux-host qualifier. Earlier failures
and source checkpoints remain in their original step records.

The foundation records below retain their historical toolchain and profile scope.
Step1225 lists the finite set requalified on .NET 11.

| Foundation | Accepted scope and evidence |
| --- | --- |
| Firmware exit | Ordinary `VerifiedExit` performs owned subsystem checks after real `ExitBootServices`, then halts. [Step1094](done/step1094.md). |
| Upstream payload feasibility | Pinned .NET 10 static Hello, runtime exerciser and real Minimal API qualify on a Linux builder. This is Linux-host evidence. [Step1198](done/step1198.md). |
| Payload loading and startup | Bounded service loop, ELF mappings and Linux initial-stack construction. [Step1200](done/step1200.md). |
| User/syscall boundary | Mechanical CPL3 ELF write/exit, register preservation, controlled faults and restoration. [Step1201](done/step1201.md). |
| Linux memory and startup services | Sparse reservations, bounded backing, time and entropy; the unchanged payload reaches unsupported `ARCH_SET_FS` before Hello. [Step1205](done/step1205.md). |
| Descriptors and pipes | Mechanical package/proc reads, live `openat`, descriptor/pipe errors and checked last-close cleanup. [Step1208](done/step1208.md). |
| Heap ownership | Qualified `brk` and fixed-mapping ownership repair, including a real protected-page fault control. [Step1209](done/step1209.md). |
| Cooperative transfer | Seventeen checked transfers across distinct guarded stacks and independent original-context restoration. [Step1218](done/step1218.md). |
| Hardware timer | Four owned interrupts, safe deferred wake and IDT/IST/APIC/mapping restoration. [Step1224](done/step1224.md). |
| Cooperative lifecycle | Repeated hardware-backed waits, retained joins, deferred cancellation and supervisor-only scrub/reuse. [Step1230](done/step1230.md). |
| Continuing network/TCP | Finite retained queues, independent wire captures, bounded TCP lifetimes and a cooperative progress witness. [Step1332](done/step1332.md). |

Runtime and network qualification remain separate bounded profiles.
Only Gate 2 passes within its documented scope. The
[completion gates](docs/completion-gates.md) define all claim boundaries;
[step records](done/) retain historical results and their exact tested inputs.

## NativeAOT-first architecture

The selected route uses the existing **`linux-musl-x64`** target and a controlled
static publish recipe. CLROS implements the required Linux x64 application
syscalls in C#; pinned upstream NativeAOT runtime, musl and native payload
libraries stay with the application. Application GC and exception handling remain
upstream responsibilities. A Linux RID alone does not make an executable
freestanding or prove CLROS compatibility. Step1225 qualifies the .NET 11 publish
recipe and records native dependencies and observed runtime/ABI requirements;
the required CLROS guest services remain on the implementation roadmap.

First-party kernel, shared library and app source committed here is C#.
`src/OS` retains an explicit no-stdlib NativeAOT EFI build. Upstream native
components may be built externally and packaged with applications; reference
repositories are not imported into the first-party build. The stable `OSHost_*`
ABI remains separate from Linux register, errno and descriptor semantics.

The initial guest target is x86-64 QEMU/OVMF with one vCPU, an immutable application
package and one configured HTTP endpoint. CoreCLR/framework-dependent hosting,
SMP and cloud-provider qualification follow the first working NativeAOT service.
The [workload profiles and roadmap](docs/workload-profiles.md) are canonical;
[architecture](docs/architecture.md) and [PAL](docs/pal.md) explain the boundaries.

## Next implementation

The existing development thread remains paused until explicitly resumed.

The immediate sequence after the qualified thread/runtime and network batches is:

1. Complete [#689 Linux sockets](http://192.168.4.35:3000/admin/CLROS/issues/689)
   and [#690 epoll](http://192.168.4.35:3000/admin/CLROS/issues/690), then
   qualify the actual socket/async engine workloads.
2. Follow the M10 NativeAOT app/publish/configuration ladder and prove guest
   Kestrel `/healthz` with an independent external HTTP request.
3. Qualify two isolated local applications before provider-specific deployment.

Use the [managed issue manifest](.gitea/backlog/clros-issues.json) for exact
acceptance, owned paths and dependencies, and the
[LAN Gitea backlog](http://192.168.4.35:3000/admin/CLROS/issues) for current claims.
Refresh both before starting work. Follow the dependency graph rather than
numeric issue order; optional research is not a first-web prerequisite.

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
