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

**No unchanged .NET NativeAOT Hello or Kestrel application has run inside CLROS
yet.** Current acceptance covers bounded boot, runtime and device diagnostics,
mechanical user execution, cooperative context transfer and hardware timer wakes.
CLROS is not cloud-ready or production-ready.

The current migration moves the OS, host tooling and guest application target to
.NET 11 with the updated native linker. Its qualification is pending. The accepted
.NET 10 results below remain historical evidence and do not transfer automatically
to a new compiler, runtime, linker or payload.

## Current accepted evidence

The accepted baseline is `master` commit
[`a651496`](http://192.168.4.35:3000/admin/CLROS/commit/a651496), recorded in
[step1224](done/step1224.md). Its default-off timer profile receives four real
local-APIC interrupts on one BSP after firmware exit. It defers redirection
through allocation, initialization and publication phases, then restores the
original context and releases the borrowed resources.

Qualification includes **4,776 full-host checks across 622 fixtures (94 Native),
six original native bodies reviewed, three fresh timer guests and ten ancestor
regressions**. Each timer guest has 115 application records. This is bounded
single-BSP CPL0 mechanical execution; continuing thread lifecycle remains open.

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

These diagnostics are separate profiles, not one continuing application kernel.
Only Gate 2 passes within its documented scope. The
[completion gates](docs/completion-gates.md) define all claim boundaries;
[step records](done/) retain historical results and their exact tested inputs.

## NativeAOT-first architecture

The selected route uses the existing **`linux-musl-x64`** target and a controlled
static publish recipe. CLROS implements the required Linux x64 application
syscalls in C#; pinned upstream NativeAOT runtime, musl and native payload
libraries stay with the application. Application GC and exception handling remain
upstream responsibilities. A Linux RID alone does not make an executable
freestanding or prove CLROS compatibility. The .NET 11 migration must requalify
the publish recipe, native dependencies and observed runtime/ABI requirements.

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

Complete [#727: .NET 11 qualification](http://192.168.4.35:3000/admin/CLROS/issues/727)
before beginning #188 boot/runtime edits. The migration currently owns the shared
integration lane.

The next scheduler slice is [#188: thread lifecycle](http://192.168.4.35:3000/admin/CLROS/issues/188):
bounded single-BSP CPL0 cooperative create/start, yield, park/wake, sleep, join,
exit and cancellation, with actual hardware timer wakes. It must join the
qualified contexts and queues under one lifetime owner, handle all workers
being parked, preserve timer progress across repeated wakes and reclaim a
thread's stack only after execution and outstanding references have left it.
This scope does not add arbitrary interrupt-preempted managed execution.

The immediate sequence is:

1. Qualify #188 lifecycle, then
   [#191 wait-on-address](http://192.168.4.35:3000/admin/CLROS/issues/191) and
   [#192 FS/TLS and kernel GS preservation](http://192.168.4.35:3000/admin/CLROS/issues/192).
2. Implement [#686 Linux threads/TLS/futex](http://192.168.4.35:3000/admin/CLROS/issues/686),
   blocking pipes and real per-thread signal masks.
3. Add [#687 signal delivery and first unchanged pinned Hello](http://192.168.4.35:3000/admin/CLROS/issues/687),
   then [#688 upstream runtime qualification](http://192.168.4.35:3000/admin/CLROS/issues/688)
   after its GC/ThreadPool prerequisites.
4. Complete the live networking, Linux sockets and epoll dependencies, then
   prove guest Kestrel `/healthz` with an independent external HTTP request.
5. Qualify two isolated local applications before provider-specific deployment.

Use the [managed issue manifest](.gitea/backlog/clros-issues.json) for exact
acceptance, owned paths and dependencies, and the
[LAN Gitea backlog](http://192.168.4.35:3000/admin/CLROS/issues) for current claims.
Refresh both before starting work. Follow the dependency graph rather than
numeric issue order; optional research is not a first-web prerequisite.

## Build and test

Start with [host requirements](docs/host-requirements.md) for the selected .NET 11
SDK, native linker and migration status following the Visual Studio update.
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
