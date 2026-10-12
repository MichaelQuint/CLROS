# Workload Profiles ADR

## Status

Accepted for planning. This document defines workload profile names and gate
semantics only. It does not add runtime, process, network, storage, hosted
.NET 10, CoreCLR, ASP.NET Minimal API, or Kestrel evidence.

## Context

CLROS has two related but different workload targets:

- A near-term cloud appliance target that runs one configured ASP.NET Minimal
  API workload.
- A later provider platform target that can run multiple isolated ASP.NET
  Minimal API workloads on one guest.

The current implementation includes host-tested contracts and accepted bounded
EFI/QEMU firmware-exit, storage and network diagnostics. Continuing services,
an upstream .NET application runtime, Kestrel, isolated application processes,
scheduling and an app supervisor remain unqualified. See completion-gates.md
for the accepted boundaries; bounded diagnostics do not establish app hosting.

[Step1201](../done/step1201.md) accepts one default-off mechanical ELF CPL3
write/exit excursion and controlled faults with full owned-state restoration.
It does not qualify the upstream NativeAOT Hello milestone, thread/TLS/futex
services, application GC/EH, a continuing workload or CLROS Kestrel.

## Decision

**Superseded ordering (2026-10-12):** the NativeAOT-first decision below is
authoritative. Older hosted-runtime descriptions preserve historical contracts,
not a requirement to finish CoreCLR before Kestrel or cloud providers before
local process isolation.

Define two workload profiles:

- `SingleMinimalApi`: the near-term default CloudReady workload profile. It
  targets one configured ASP.NET Minimal API/Kestrel workload and intentionally
  excludes multi-process requirements such as a process table, per-process
  address spaces, per-app isolation, and a multi-app supervisor. It still
  requires the Kestrel prerequisites: NativeAOT runtime boundary, package/config
  access, sockets, timers, threading/synchronization, and live network I/O.
- `MultiMinimalApi`: the later multi-app platform profile. It requires real
  process, isolation, scheduler, and supervisor semantics before it can be
  claimed. It is not a microkernel claim, and it is not implemented today.

`SingleMinimalApi` is the default implementation path until a specific issue
selects `MultiMinimalApi`.

## NativeAOT-first execution roadmap (2026-10-12)

Accepted architecture direction only: no upstream guest application/runtime
acceptance. M16-001/#679 through M16-005/#683 are qualified within the bounded
evidence below. The next dependency-compatible slice is
[M16-006 / #684](http://192.168.4.35:3000/admin/CLROS/issues/684), measured Linux
memory/startup services. Refresh the tracker before implementation.
The [foundation epic #678](http://192.168.4.35:3000/admin/CLROS/issues/678)
links #679-#690; [#691](http://192.168.4.35:3000/admin/CLROS/issues/691)
owns the continuing NIC service. Tracker migration and cleanup evidence are in
[step1196](../done/step1196.md).

Run one pinned upstream .NET 10 NativeAOT Minimal API through a restricted
Linux x64 syscall ABI implemented in the C# kernel, then two separately
packaged applications in hardware-protected processes. Preserve the upstream
runtime, GC, EH and musl inside each application payload. Kernel and application
heaps, roots, metadata and exception machinery remain separate.

Use the existing `linux-musl-x64` target and controlled publish recipe before
considering a custom RID/runtime fork. First-party kernel/std/apps remain
C#14/.NET10; attributed upstream native dependencies may be built externally
and packaged with apps. Reference snapshots remain read-only, outside build
inputs. Preserve `OSHost_*` and OSHostStatus; Linux register/errno/fd semantics
are a distinct adapter over common kernel services, not changes to that ABI.

| Stage | Managed keys / existing issues | Required result |
| --- | --- | --- |
| Publish first | M16-001 | Linux builder; static Hello, runtime exerciser and real Minimal API; ELF/native dependency and syscall/source review. |
| Load and continue | M16-002 through M16-007 | Immutable staging, continuing post-exit loop, mechanical ELF64 startup/user boundary, genuine memory and descriptor services; no upstream runtime acceptance. |
| Threads and runtime | #183-#194, M16-008 through M16-010 | Scheduler/TLS/futex, signal ABI and first real pinned Hello in M16-009/#687; focused suspension and comprehensive app GC/EH/ThreadPool/timer qualification follow. |
| Continuing network | M07-026, #148-#153 | Persistent Virtio RX/TX ownership, ARP and passive TCP with timing/retransmission/partial I/O/close. |
| Socket integration | M16-011, M16-012 | Linux sockets and actual epoll readiness used by System.Net.Sockets. |
| First web acceptance | #214-#227 | Real guest Kestrel /healthz with independent external HTTP evidence. |
| Two applications | #286-#301 | Separate page tables/private resources/quotas; one app fails while the other keeps serving. |
| Later targets | M12/M13, CoreCLR-specific #204-#212 | Provider devices/qualification and framework-dependent workloads; Roslyn/PowerShell remain later. |

Numeric milestone order is not execution order. Scheduling no longer waits
for HTTP/custom application EH; local multi-process work no longer waits for
AWS/Azure. The durable dependency source is
`.gitea/backlog/clros-issues.json`. Accepted bounded diagnostics remain valid;
their existence does not prove continuing services.

M16-007/#685 qualifies descriptor and immutable-file behavior with mechanical
fixtures. It must not require real NativeAOT Hello before M16-008/#686 threads
and M16-009/#687 signals exist: the pinned Hello trace starts finalizer and
signal-handler threads before writing its greeting. #686 uses mechanical
clone/TLS/futex fixtures and preserves each thread's signal mask; it does not
require a .NET thread fixture. After implementing signal startup/delivery,
#687 owns the unchanged pinned Hello's first guest execution before focused
suspension qualification. M16-010/#688 repeats Hello as a regression and adds
the comprehensive runtime exerciser. Mechanical ELF entry/write/exit remains
separate evidence; #689's real System.Net.Sockets fixture follows #688.
Basic pipe descriptors/nonblocking behavior belong to #685; blocking wait/wake
belongs to #686 and signal interruption/restart to #687. Futex work follows the
M09-006 wait core; general event/mutex tickets M09-007/M09-008 are not prerequisites.

Initial profile: x86-64 QEMU/OVMF, one vCPU, static non-PIE Linux executable,
invariant globalization, workstation GC, HTTP/1.1 IPv4, explicit static IP,
port8080 and immutable package/config. Start one user-mode context with
protected kernel mappings; cross-process isolation is the mandatory next
milestone. General writable storage, DHCP/DNS/outbound fetch, TLS, SMP,
ASLR and provider drivers are not first-web prerequisites.

M16-001 is a feasibility gate: publish/inspect ET_EXEC, PT_LOAD/PT_TLS,
startup/auxv and native dependencies; prove no PT_INTERP or DT_NEEDED.
Exercise startup, forced GC, threads, requests and shutdown under tracing,
then inspect upstream error paths because tracing is not an exhaustive syscall
specification. If the static closure cannot be built, preserve the exact
blocker and review a changed strategy before speculative compatibility work.
Linux-host success is not CLROS guest evidence. Never implement successful
no-op threads, mappings, protection or entropy.

[Step1198](../done/step1198.md) qualifies M16-001's pinned static Hello, runtime
exerciser and real Minimal API on Alpine3.24.2, SDK10.0.401/runtime10.0.12.
All three are x64 ET_EXEC with four disjoint load ranges, TLS, no interpreter
or dynamic section; upstream application GC/EH remains separate from the kernel.
Fifty-four observed syscall names across22 thread/process trace files, 675 source
and258 native input hashes, headers/symbols/compiler responses and actual auxv
are retained. External Windows HTTP returns exact `OS_MINIMAL_API_OK`, followed
by clean app exit and a failed post-shutdown probe. This qualifies the Linux
host feasibility gate only. [Step1199](../done/step1199.md) implements #680's
versioned immutable package/staging profile; three final-source guests pass
data retention and digest checks after firmware exit, together with two rejection
controls, two boot regressions and 4,565 full-host checks. ELF entry and guest application/runtime/
Kestrel gates remain open. M16-003 owns the bounded service loop; M16-004 owns
ELF mappings, TLS and Linux initial stack construction.

[Step1200](../done/step1200.md) qualifies both children with 4,603 full-host checks,
native review, three combined package guests, standalone children, five affected
ancestors and missing-RNG rollback.
The loader separates kernel and user
leaves, omits the physical RW image alias from the user root, zeroes BSS/padding,
and constructs argv/envp/auxv from the versioned manifest, actual PHDR and borrowed
firmware entropy. Its current live backing cap is 4096 pages, preserving the
existing kernel identity-map limits. TLS template geometry is admitted; the later
step1201 profile qualifies mechanical application entry and write/exit syscalls.
Thread TLS and upstream application startup remain incomplete. No guest runtime
or Kestrel evidence follows from loading, read-back or mechanical entry alone.

### Pinned payload requirements and trace limits

The tested CPU is QEMU `qemu64`, one CPU, with SSE/SSE2/SSE3, FXSAVE, NX and
long mode; no AVX is advertised. Preserve this measured target and upstream
CPUID dispatch before broadening CPU claims. GNU notes record a 1.5 MiB stack;
they are not an exhaustive CPU specification. The runtime/ILC/package inventories
in step1198 are the exact build input evidence.

| Contract family | Observed and source-backed requirement |
| --- | --- |
| ELF/startup | Actual auxv supplies PHDR0x400040, PHENT56, PHNUM11, ENTRY0x4014EE, PAGESZ4096, identity/security, RANDOM, platform/exec-name and vDSO values. musl1.2.6 reads argv/envp/auxv, TLS and stack-canary entropy before main; missing TLS/thread-pointer initialization is fatal. Mechanical startup is qualified; upstream NativeAOT guest startup is not. |
| Memory/order | mmap/mprotect/munmap/mremap/madvise/brk and resource queries appear. GC queries/registers expedited membarrier. Its source has a protection/dirty-page barrier fallback; success no-ops cannot satisfy it. Trace failures include real ENOMEM/EFAULT/EINVAL and permitted discovery ENOENT. |
| Threads/TLS/signals | clone, arch_prctl, set_tid_address, futex and realtime signal masks/handlers/return appear. musl thread setup owns stack/TLS and translates clone failure to EAGAIN with cleanup. NativeAOT thread attachment/key failures may fail fast; GC suspension must preserve the full signal context and errno. |
| Files/descriptors | Immutable config plus proc/sys CPU/memory discovery use open/read/stat/readlink/getdents/lseek/pread, pipe/fcntl/ioctl and close. Missing optional discovery paths and ENOTTY are observed; descriptor identities, bounds and errno semantics remain necessary. |
| Sockets/readiness | socket/bind/listen/accept4, sendto/recvfrom, setsockopt/shutdown and epoll create/control/wait appear. System.Native uses edge-triggered readiness, retries EINTR, maps HUP to read/write progress and returns actual socket errors. A successful trace covers neither every error nor a CLROS TCP implementation. |
| Time/termination | pthread timed waits need monotonic deadlines, spurious-wakeup handling and genuine timeout behavior; syscall tracing misses uncontended user-space operations and vDSO time reads. exit/exit_group and SIGTERM shutdown are observed. |

Source/error review is a requirement inventory, not execution of those failure
branches. Optional TLS/crypto/dynamic loading, wider APIs/CPU shapes and hostile
long-running load remain outside this fixed HTTP/1.1 qualification. Unknown
syscalls must fail honestly; tracing never becomes a complete successful-no-op ABI.

M16-006 must separate reserved virtual address space from committed physical
pages and their quotas. The pinned Hello's step1198 trace reserves
8,234,266,624 bytes with `PROT_NONE`, beyond the current loader's low-4-GiB
user range. Sparse reservation must not allocate one backing page per reserved
page. Preserve the qualified payload and model its measured reservation,
commit/protect/decommit and failure behavior, or explicitly change the bounded
runtime configuration and repeat publish/trace/source qualification. The trace
alone does not prove which smaller-reservation fallback the runtime can use;
silent payload/configuration changes or successful no-op mappings are invalid.

The in-progress backend in [step1203](../done/step1203.md) admits a bounded
64-GiB user VA layout while retaining low-4-GiB physical/root constraints.
Focused host checks replay the exact large reservation with no new tables or
backing, then commit distant islands, trim/release ranges and replace committed
pages with anonymous `PROT_NONE`. The pinned payload/configuration is unchanged.
The default-off EFI adapter and genuine bounded startup services are implemented
in [step1204](../done/step1204.md). A 34-call mechanical guest passes the large
reservation, partial commit/protection/replacement/release, high copy, startup
queries, live monotonic time, fresh entropy and real single-context barriers.
Repeated and negative captures, original native review, full-host/ancestor
qualification and the actual unchanged upstream entry attempt remain pending.
This development capture does not establish NativeAOT Hello or issue acceptance.

### Transport decision experiments

Stock `linux-musl-x64` with real Kestrel remains the primary route. The host-only
[custom transport comparison #702](http://192.168.4.35:3000/admin/CLROS/issues/702)
may retain Kestrel's HTTP engine while replacing its socket transport
through `IConnectionListenerFactory`; compare the pinned application's behavior,
dependency inventory and required CLROS services before adopting it. This can
change networking integration, but does not remove upstream GC/EH, memory,
threading or timer requirements. The experiment is not guest acceptance and
does not block #684. See [the Kestrel ledger](kestrel.md#transport-decision-experiments).

[WASI HTTP research #703](http://192.168.4.35:3000/admin/CLROS/issues/703)
is a separate optional route using a component host and a custom ASP.NET
`IServer`. It must not be reported as Kestrel or satisfy the
existing first-web gate. Publishing a .NET 10 WASI component and invoking it on
a host do not prove a freestanding CLROS engine, runtime or network boundary.
Both tickets are supplemental research with no milestone and no critical-path
dependency; their results inform a later explicit architecture decision.

### Guest acceptance

First web requires three fresh post-firmware-exit boots, external HTTP200
with exact body `OS_MINIMAL_API_OK`, repeated/concurrent connections,
async/cancellation and clean shutdown. Preserve source/compiler/kernel/app/
package/image/serial/packet/HTTP hashes. After terminating the guest the probe
must fail. Require `TEST_START kestrel.smoke`, `KESTREL_APP_START`,
`KESTREL_BIND_READY`, `HTTP_PROBE_PASS`, `TEST_PASS kestrel.smoke`;
missing/failure markers invalidate the run. Kernel-authored HTTP is not Kestrel.

The next gate requires distinct address spaces and private descriptors/config,
budgets, endpoint authorization and supervisor lifecycle. Fault, terminate and
exhaust one app while probing the other; verify cross-process pointer/handle
rejection, cleanup and bounded restart. Two servers sharing one process do not
qualify. No shell/SSH reduces exposed functionality, but is not proof of
resistance to application exploits or superiority over hardened Linux/Windows.

## Build Selector

The build selector itself remains unchanged. See the current roadmap above
for the implementation and evidence required before either profile is usable.

Issue #249 adds a build-time selector named `OSWorkloadProfile`. It is a
compile-time contract only. It selects constants and host-testable capability
metadata; it does not start Kestrel, create a process table, enable a supervisor,
or change runtime behavior.

Default build:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
```

The default is `SingleMinimalApi` and emits the compile symbol
`OS_PROFILE_SINGLE_MINIMAL_API`.

Explicit multi-profile build:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1 /p:OSWorkloadProfile=MultiMinimalApi
```

This emits `OS_PROFILE_MULTI_MINIMAL_API`, sets the kernel-facing profile
metadata to require process-table and multi-app manifest support, and still does
not implement those features.

Invalid values fail during build with a readable MSBuild error:

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1 /p:OSWorkloadProfile=BadProfile
```

The kernel-facing metadata is exposed through
`OS.Kernel.Build.WorkloadProfile`:

- `SelectedName`
- `SelectedKind`
- `IsSingleMinimalApi`
- `IsMultiMinimalApi`
- `RequiresProcessTable`
- `RequiresMultiAppManifest`
- `SupportsMultipleMinimalApiApps`
- `ExactlyOneProfileSelected`

`tools/OS.ManifestTool` also accepts a matching profile selector:

```powershell
dotnet .\tools\OS.ManifestTool\bin\Debug\net10.0\OS.ManifestTool.dll validate --manifest .\manifest.json --profile SingleMinimalApi
```

When `--profile` is omitted, the tool uses the build-selected
`OSWorkloadProfile`. `SingleMinimalApi` rejects future multi-app manifest
fields such as `apps`, `restartPolicy`, `resources`, `isolation`, and
`supervisor`. `MultiMinimalApi` returns `multi_minimal_api_unsupported` until
process, address-space isolation, and supervisor semantics exist.

## Tooling Propagation

Issue #252 propagates the profile selector into evidence-producing host
tooling. This is still a host evidence contract only; it does not implement
processes, Kestrel, hosted .NET, networking, storage, or supervisor behavior.

QEMU smoke wrapper default:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
```

Expected evidence includes:

```text
WORKLOAD_PROFILE SingleMinimalApi
TEST_PASS workload.profile.tooling default single
```

Explicit multi-profile dry run:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun -WorkloadProfile MultiMinimalApi
```

Expected evidence includes:

```text
WORKLOAD_PROFILE MultiMinimalApi
TEST_PASS workload.profile.tooling propagates multi
```

CI and baseline evidence scripts accept the same `-WorkloadProfile` argument,
record `workloadProfile` in their JSON summaries, and pass
`/p:OSWorkloadProfile=<value>` to MSBuild-backed commands.

`tools/OS.ImageBuilder` accepts `--workload-profile` and emits
`WORKLOAD_PROFILE <value>`. The raw image dry-run plan includes
`"workloadProfile":"<value>"` in the `IMAGE_PLAN` JSON payload.

`tools/OS.QemuRunner` accepts `--workload-profile` and emits
`WORKLOAD_PROFILE <value>` in dry-run, real-run console output, and real-run
summary logs.

Invalid values fail early with a nonzero exit and a machine-readable failure:

```text
TEST_FAIL workload.profile.tooling rejects invalid profile=BadProfile valid=SingleMinimalApi,MultiMinimalApi
```

## Feature Matrix

| Feature | `SingleMinimalApi` | `MultiMinimalApi` |
| --- | --- | --- |
| Default workload count | Exactly one configured ASP.NET Minimal API workload. | Two or more configured ASP.NET Minimal API workloads. Acceptance starts with at least two apps. |
| Manifest shape | One workload entry selected by the OS manifest. Current v0 manifest contracts may reserve the profile name later, but existing validators do not enforce it yet. | A bounded app list with per-app name, package or assembly path, args, environment/config, listen endpoint, log settings, resource limits, and restart policy. Not implemented. |
| Process table | Explicitly not required for the near-term profile. No `pid`, `spawn`, `wait`, `kill`, or process enumeration requirement is added by this profile. | Required. Needs process IDs, lifecycle state, exit status, terminate/kill, supervisor ownership, and resource accounting. |
| Per-app address space | Explicitly not required. One workload may run in the only hosted workload context once a hosted boundary exists. This does not prove containment from other apps because there are no other apps. | Required. Each app needs its own virtual address space, handle table, memory limits, guard pages, and copy-in/copy-out validation at kernel or PAL boundaries. |
| Supervisor/restart policy | Boot selects and starts one workload. Restart policy can stay absent or be a single-workload policy after app exit capture exists. No multi-app dependency ordering or port arbitration is required. | Required. The supervisor must start, stop, monitor, restart, and log multiple apps, detect port conflicts, preserve per-app exit codes, and avoid one app crash taking down the others. |
| Scheduler/threading | Requires enough thread, timer, and synchronization support for the selected hosted .NET 10/CoreCLR or Native AOT Minimal API/Kestrel path. It does not require process scheduling. | Requires preemptive scheduling across processes and threads, thread creation/join/exit, sleep/yield, timer waits, futex-style wait/wake, and fair handling of multiple active Kestrel apps. |
| Hosted .NET 10/CoreCLR boundary | Requires a documented hosted boundary such as flat `OSHost_*` PAL calls for file/config/socket/time/thread/memory behavior, or an explicitly documented Native AOT app target. One runtime/workload instance is enough for this profile. | Requires the hosted boundary to be process-aware: per-app handles, per-app config/environment, per-app socket ownership, isolated runtime state, and safe cleanup after app crash or restart. |
| Kestrel network/runtime prerequisites | Required before claiming the profile: app package/config is available in guest, PAL or ABI calls are exported, TCP listener works over live network, Kestrel binds the manifest endpoint, and an external `GET /healthz` reaches the app response. | Requires all single-app Kestrel prerequisites plus distinct ports for multiple apps, concurrent accepts, app-to-app isolation, and crash isolation while another app continues responding. |
| Storage/config packaging | One selected app package plus one config/environment set. The current package, manifest, and `kestrel.smoke.package.v0` docs are contracts only until live guest reads exist. | Multiple app packages or one package with multiple app entries, per-app config/environment, per-app log paths, package enumeration, and update semantics if added later. |
| Serial/panic diagnostics | Required boot, hosted-runtime, Kestrel bind, health-probe, app exit, and panic markers. Serial remains the required early evidence channel. | Required boot plus per-app lifecycle/crash markers. One app crash must be diagnosable without corrupting another app or losing supervisor state. |
| Cloud artifacts | One bootable image with one configured workload. Local QEMU proof is not Azure/AWS proof; provider artifacts still require provider-specific validation. | Cloud image contains multiple workloads and supervisor config. Provider validation must prove the same multi-app behavior on the selected substrate, not only local QEMU. |
| Security posture | No users, no SSH, no shell, no remote admin by default. The only network service is the configured workload or explicit manifest endpoint. No per-app containment claim is made. | Adds per-app containment requirements: address-space isolation, resource limits, bind permissions, metadata mediation, filesystem/config permissions, W^X/NX enforcement, and crash isolation. |

## Profile Claim Rules

`SingleMinimalApi` can be claimed only when evidence shows:

1. A guest image boots to stable serial markers.
2. The selected app package/config is available to the guest.
3. The hosted runtime or app target starts in the guest.
4. Kestrel binds the configured HTTP endpoint.
5. An external runner receives the expected response from `GET /healthz`.
6. Serial logs include workload startup, bind, health-probe, exit/failure, and
   panic markers where applicable.

Provider scope is part of the claim; the canonical provider-scoped Kestrel
marker and evidence lists are in
[`docs/completion-gates.md`](completion-gates.md#gate-14-provider-split) and
[`docs/kestrel.md`](kestrel.md#provider-scope). Local warning:
a `SingleMinimalApi` QEMU/libvirt Kestrel pass proves only local virtualization
behavior, not AWS Nitro or Azure VM support. AWS still needs
Nitro/ENA/NVMe/DHCP/TCP/EC2 serial evidence, and Azure still needs
Hyper-V/VMBus/NetVSC/StorVSC/DHCP/TCP/Azure serial or boot diagnostic evidence.
AWS IMDSv2 and Azure WireServer/goal-state are supporting integrations, not
substitutes for provider Kestrel traffic.

Summary: QEMU/libvirt Kestrel does not prove AWS Nitro support and does not prove Azure VM support.

The following are not required for `SingleMinimalApi`:

- Process table.
- Process IDs.
- Per-process address spaces.
- Multi-app supervisor.
- Per-app resource limits.
- Per-app metadata mediation.
- Running two apps at once.

`MultiMinimalApi` can be claimed only when evidence shows:

1. At least two ASP.NET Minimal API workloads start from manifests.
2. Each app has a distinct lifecycle identity, listen endpoint, config, and log
   stream.
3. The OS or hosted boundary enforces per-app address spaces or an equivalent
   documented isolation model.
4. One app can crash or be terminated without corrupting the OS or another app.
5. The supervisor observes exit status and applies the configured restart
   policy.
6. External HTTP probes can reach both apps after startup, and can still reach
   the surviving app after the other app fails.

## Non-Goals

- This ADR does not require general POSIX/Linux compatibility beyond the
  measured restricted Linux application ABI described above.
- This ADR does not require `fork()`.
- This ADR does not claim microkernel behavior.
- This ADR does not claim Azure, AWS, or provider boot support.
- This ADR does not turn host-side Kestrel, manifest, PAL mock, package, or
  parser tests into in-guest workload evidence.

## Current Evidence Boundary

Today the repository has host-tested manifest, package, PAL mock socket/config
and readiness contracts plus local EFI/QEMU boot markers. That evidence supports
planning for both profiles, but it does not satisfy either profile.

The full hosted-workload evidence boundary remains in
[`docs/completion-gates.md`](completion-gates.md#gate-14-profile-split) and
[`docs/kestrel.md`](kestrel.md#in-guest-kestrel-evidence).

The near-term backlog should continue to implement `SingleMinimalApi` first:
live storage/package reads, hosted runtime or app-target boundary, socket/timer/
thread prerequisites, live TCP listener behavior, Kestrel bind, external HTTP
probe, and serial evidence. `MultiMinimalApi` should remain blocked until
process, address-space isolation, scheduler, supervisor, and per-app security
issues are represented and tested.
