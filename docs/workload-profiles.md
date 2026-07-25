# Workload Profiles ADR

## Status

Accepted for planning. This document defines workload profile names and gate
semantics only. It does not add runtime, process, network, storage, hosted
.NET 10, CoreCLR, ASP.NET Minimal API, or Kestrel evidence.

## Context

OS has two related but different workload targets:

- A near-term cloud appliance target that runs one configured ASP.NET Minimal
  API workload.
- A later provider platform target that can run multiple isolated ASP.NET
  Minimal API workloads on one guest.

The current implementation is still host-tested readiness contracts plus local
EFI/QEMU boot markers. No hosted .NET 10 runtime, CoreCLR path, Kestrel path,
live storage, live network, process table, per-process address space, scheduler,
or app supervisor exists today.

## Decision

Define two workload profiles:

- `SingleMinimalApi`: the near-term default CloudReady workload profile. It
  targets one configured ASP.NET Minimal API/Kestrel workload and intentionally
  excludes multi-process requirements such as a process table, per-process
  address spaces, per-app isolation, and a multi-app supervisor. It still
  requires the Kestrel prerequisites: hosted runtime boundary, package/config
  access, sockets, timers, threading/synchronization, and live network I/O.
- `MultiMinimalApi`: the later multi-app platform profile. It requires real
  process, isolation, scheduler, and supervisor semantics before it can be
  claimed. It is not a microkernel claim, and it is not implemented today.

`SingleMinimalApi` is the default implementation path until a specific issue
selects `MultiMinimalApi`.

## Build Selector

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
.\scripts\run-qemu-smoke.ps1 -DryRun
```

Expected evidence includes:

```text
WORKLOAD_PROFILE SingleMinimalApi
TEST_PASS workload.profile.tooling default single
```

Explicit multi-profile dry run:

```powershell
.\scripts\run-qemu-smoke.ps1 -DryRun -WorkloadProfile MultiMinimalApi
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

- This ADR does not introduce a POSIX/Linux compatibility layer.
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
