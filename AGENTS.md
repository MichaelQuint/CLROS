# CLROS AI Agent Guide

## Start here

Read [README.md](README.md), [documentation map](docs/documentation-map.md),
[workload profiles](docs/workload-profiles.md) and
[completion gates](docs/completion-gates.md) for current status and exact scope.
Keep this guide an operating contract; put detailed evidence in `done/` and
subsystem docs instead of duplicating the milestone history here.

As of October 23, 2026, LAN master includes PR790 at `82656076`.
[Step1556](done/step1556.md) qualifies finite quota termination and async churn:
6,258 unfiltered full-host checks across 847 fixtures (753 Fast, 94 Native),
unchanged inputs, zero reuse, and 20 ancestor profiles. Original Smoke remains
at `32c14ccd`; quota256/512 and positive V2 remain at `20e665ad`; full-host and
the R11 ancestors remain at `28a79996`. Preserve all original and failed receipts.
The 36 MiB pool is shared, not independent 16 MiB GC/20 MiB native partitions.

Live readback confirms #748, #225 and the finite #737 integration closed, with
the fourteen step1481 predecessor scopes also closed. Hello and bounded runtime,
socket and Kestrel service profiles already execute in CLROS. #226 -> #227 and
complete local Kestrel/Gate14 remain open. Only numbered Gate 2 passes;
arbitrary workloads, multi-app isolation, cloud and production remain unaccepted.

Refresh LAN master and live ownership before work. #780 tracker conflict
protection and #781 -> #782 host FAT repairs remain open; the FAT repairs precede
#769 disk fixtures. Preserve any active development lane. Do not claim new work
under the closed #737 integration or make host FAT repairs a first-web runtime
prerequisite. Use each managed issue's exact dependencies, not numeric order.

The primary checkout is `C:/Projects/CLROS`. Inspect its branch, dirty files and
active worktrees before editing; use a separate worktree when another agent owns
overlapping work. [Step1478](done/step1478.md) archived the fifteen old linked
checkouts and build caches under
`C:/Projects/CLROS-archive/2026-10-19-workspace-cleanup`; its `cleanup-state.json`
maps historical evidence paths. That cleanup does not prohibit new worktrees.
The earlier paused-primary and frozen October 20 validation instructions are
historical. Build current tooling before new validation in a fresh checkout.

Pick one cohesive dependency-compatible slice. Run focused checks during work;
reserve expensive full-host/native/guest qualification for a stable batch.
Do not expand general kernel EH without a demonstrated requirement.

Keep one main public type per new file where practical. Document public and
unsafe APIs with purpose, ownership/lifetime, execution context, failure
behavior and unsupported cases. Avoid boilerplate comments.

The shared host project is `src/OS.Std.NoRuntime.Shared`. Its audited source
remains linked from `std/no-runtime/shared`; do not duplicate types or casually
move EFI inputs. Use pinned .NET 11 and C# 14 as specified below and in
[host requirements](docs/host-requirements.md).

## Current approved batch (2026-10-23)

The durable goal covers NativeAOT MinimalApi, an actual Standalone Blazor client
served from FAT32, then persistent logs/uploads and SQLite. The first batch is
#780, #781 -> #782 and #768-#774. Use exactly one storage subagent; the primary
agent owns shared integration, documentation, tracker and serialized builds.
[Steps1558](done/step1558.md)/[1559](done/step1559.md) record current development.
The isolated read-service integration adds a combined M16-025 mechanical disk
profile with M16-023/024 parents and source4 thread waits. Preserve its separate
PCI0:7 disk and PCI0:6 NIC ownership. Serialize native publication and guest
qualification with the staged M10-017 lane; compiling EFI C# is not native or
guest acceptance. M10-018 remains denied until its complete consumer is ready.
The new server is `apps/MinimalApiStaticFiles`; preserve exact original smoke
app and `NativeAotPayload`/`NativeAotWebPayload` bytes. Publish through the fixed
static-file wrapper and immutable original socket recipe. #224/#225/#737 are
closed within step1556's finite scope; #226/#227 and all new disk/write claims
remain open. After each5-10 issue batch audit README, all first-party docs and
guides, update affected Gitea issues/comments and canonical records, retain
immutable evidence, commit/push/review/merge verified work and continue.

## Project Identity

The project name is `CLROS`.

Do not call this project `OS` or `SharpOS`. `OS` remains the stable assembly,
namespace, source-path, and hosted-ABI prefix. SharpOS is a reference project under
`references/`, not the name or architecture of this repository.

`CLROS` targets a C#-first, NativeAOT-based CloudReady operating system. The
intended deployment model is a minimal single-workload cloud appliance:

- Freestanding C# kernel and boot path.
- NativeAOT-based first-party boot/runtime boundary.
- Reproducible UEFI boot image and cloud/virtualization image tooling.
- No default multi-user shell, SSH, or remote administration surface.
- One configured workload by default.
- .NET 11 applications as first-class workloads.
- ASP.NET Minimal API/Kestrel support as a mandatory milestone.

CLROS owns its architecture and security policy. Linux compatibility is a
bounded application adapter, not a goal of reproducing a general Linux system.
Keep memory ownership, scheduling/waits, storage, networking, quotas and cleanup
in reusable CLROS services suitable for both Linux and future custom PE apps.
Keep syscall numbers, errno, fd tables, Linux structures/signals and ELF startup
in the adapter; future PE imports/startup translate to the same core services.
Do not route PE apps through Linux syscalls or require exported `OSHost_*` calls
for ordinary internal C# calls. Prefer concrete shared operations over speculative
abstraction frameworks. Existing mixed code needs a scoped, qualified extraction
when extended for reuse, not a wholesale rewrite during an active batch.
Require a supported .NET workload need before expanding compatibility. Normal
app deployment must not add kernel code or require manual disassembly. Production
images expose no interactive shell, terminal, SSH or command-execution admin
endpoint; host tooling and bounded diagnostics remain separate. See the
[core/adapter contract](docs/architecture.md#core-services-and-application-adapters).

Ordinary `VerifiedExit` is accepted in [step1094](done/step1094.md) and ends
after owned post-firmware checks. Diagnostic/application profiles remain
explicit and default off. The pinned Linux NativeAOT route qualifies bounded
Hello/runtime execution (step1307), TCP (step1332), sockets/epoll (step1351),
Kestrel health/UTC (step1419), repeated service (steps1479/1481), and quota/churn
(step1556). These profiles do not turn ordinary boot into a general app launcher.

General kernel EH, arbitrary Linux compatibility, continuing disk-backed static
files, complete local Gate14, multi-app isolation and provider readiness remain
unfinished. Historical profile limits and current gate criteria live in the
canonical docs and immutable steps, not in an inferred claim from a test count.

Never claim Kestrel, hosted .NET, real `ExitBootServices`, live PCI/storage/
networking, cloud-provider support, cloud readiness, or production readiness
unless `docs/completion-gates.md` has matching evidence.

The #124/#125/#126/#627 bounded GPT batch is accepted in `done/step1150.md`:
primary header/array validation, real backup fallback and EFI partition location
pass full-host/native review and intact/damaged-primary guest captures. Each
sector uses a fresh reset/cleanup lifetime; manifest/package loading and
continuing storage remain incomplete.

## Required First Reads

Before meaningful changes, read:

- `README.md`
- `docs/documentation-map.md`
- `docs/host-requirements.md`
- `docs/repo-audit.md`
- `docs/architecture.md`
- `docs/boot.md`
- `docs/boot-order.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/efi-build.md`
- `docs/completion-gates.md`
- Latest `done/stepNN.md`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Diagnostics/Probes.cs`

For subsystem work, also read the matching doc:

- Boot/runtime: `docs/boot.md`, `docs/efi-build.md`, `docs/diagnostics.md`
- PAL: `docs/pal.md`, `work/PAL/pal-design.md`
- Drivers/platform: `docs/drivers.md`
- Storage: `docs/cstorage.md`, `docs/image-builder.md`
- Networking: `docs/networking.md`
- Application model: `docs/app-model.md`, `docs/manifest.md`
- QEMU/libvirt/cloud: `docs/qemu.md`, `docs/libvirt.md`,
  `docs/cloud-import.md`, `docs/cloud-provider-requirements.md`
- Security: `docs/security.md`, `docs/license-audit.md`

Directory-local `AGENTS.override.md` files apply only where they add rules that
differ from this root guide.

## Repository Layout

- `src/OS`: explicit no-stdlib NativeAOT EFI shell project included in
  `CLROS.slnx` under stricter directory-local build rules.
- `src/OS.Kernel`: host-testable kernel, boot, diagnostics, platform, PAL,
  storage, networking, and app-model contracts.
- `std/no-runtime/shared`: no-runtime-compatible shared utility/BCL-like
  surface. Add shared utilities here before kernel/app usage.
- `apps`: app fixtures and placeholders.
- `tools`: host-side tools.
- `tests/OS.HostTests`: host test harness with machine-readable markers.
- `tools/OS.Tooling`: C# build, verification, smoke, evidence, and repository commands.
- `docs`: canonical architecture, operation, gate, and subsystem docs.
- `done`: immutable step evidence history. Do not delete step files.
- `work`: design work in progress, including PAL design.
- `references`: reference repositories only. Do not treat them as first-party
  source or drop-in architecture.
- `artifacts`: generated output. Do not commit generated artifacts unless a
  task explicitly requires it.

## Non-Negotiables

- First-party kernel/std/app source committed to this repo must be C#.
- Do not commit `.c`, `.cpp`, `.h`, `.asm`, or `.s` files.
- Use C# 14.0 and .NET 11 for normal managed projects.
- Keep `src/OS` explicit, minimal, no-stdlib, and NativeAOT-aware.
- `src/OS.Kernel/Boot/BootSequence.cs` is the source of truth for boot order.
  Add phases there; do not bypass it.
- `src/OS.Kernel/Diagnostics/Probes.cs` owns compile-time probe toggles.
  Dangerous probes must default to `false`.
- Add shared string, numeric, collection, memory, and BCL-like behavior first
  under `std/no-runtime/shared`.
- Avoid exceptions, hidden allocations, reflection, and dynamic invocation in
  kernel/boot/runtime paths unless the current issue implements and tests the
  required runtime support.
- Use status structs/results for kernel-facing failure paths.
- The NativeAOT-first decision in `docs/workload-profiles.md` authorizes a
  restricted Linux x64 application syscall adapter in C#. Keep Linux errno/fd
  semantics separate from the stable `OSHost_*` contracts.
- Pinned upstream runtime/libc dependencies may be built externally and
  packaged with apps; do not import reference projects or native source into
  first-party kernel/std/apps.
- Future PAL/CoreCLR work crosses a flat `OSHost_*` ABI: POD structs, explicit
  lengths, opaque handles, status codes, and no managed references, exceptions,
  C++ objects, WinAPI handles, or POSIX descriptors across the boundary.
- Before real `ExitBootServices`, firmware data needed later must be copied or
  represented behind platform interfaces, and owned serial diagnostics must be
  ready.
- Security posture remains no users, no SSH, no remote admin surface by default.
  Do not print or commit secrets.

## Build And Test Commands

Run from `C:\Projects\CLROS`.

For final qualification, commit changes before building and freeze HEAD. Rebuild
the complete HostTests reference graph with `--no-incremental`; verify producer
and copied DLL hashes/revision stamps agree before starting the full lane.
Native fixtures rebuild some dependencies, so a pre-commit binary baseline can
drift even while source stays frozen. Keep final exact-byte input guards enabled.
See [host baseline](docs/host-requirements.md#full-lane-build-baseline).

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --lane full
dotnet run --project .\tools\OS.Tooling -- repo check
dotnet run --project .\tools\OS.Tooling -- contracts check
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
dotnet run --project tools/OS.Tooling -- efi build
git diff --check
```

Real QEMU smoke on the current Windows host:

```powershell
dotnet run --project tools/OS.Tooling -- qemu smoke -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 15
```

Dry-run QEMU output is not boot proof. Boot proof requires a generated image,
OVMF/QEMU execution, captured serial logs, and expected markers.

## Evidence Contract

All tests and smoke paths must use stable markers:

- `TEST_START <name>`
- `TEST_PASS <name>`
- `TEST_FAIL <name> <reason>`
- `OS_BOOT_OK`
- `OS_SMOKE_PASS`
- `OS_SMOKE_FAIL`

Boot flow markers should be stable and parseable. Avoid decorative logs in
machine-read paths. Serial output is the primary kernel evidence channel.

## Documentation Rules

Repo docs are canonical for build/run commands, ABI contracts, completion gates,
subsystem status, and evidence. The Gitea wiki may contain reader-friendly
summaries only; it must link back to repo docs and issues for source-of-truth
details.

Use `docs/documentation-map.md` before adding or moving docs. Update the
relevant existing doc instead of creating duplicates. Keep subsystem docs honest
about current status, implemented behavior, unsupported behavior, tests,
limitations, and next steps.

Every meaningful milestone needs a new or updated `done/stepNN.md` with:

- What changed.
- Why it changed.
- Files changed.
- Tests run.
- Logs/evidence.
- Failures.
- Deferred work.
- Evidence boundary.

Run `dotnet run --project tools/OS.Tooling -- done next` or
`dotnet run --project tools/OS.Tooling -- done reserve` before creating a new
step file. Reservations use atomic file creation.

Host tests use explicit C# discovery, never module initializers. The default
command runs the fast lane and emits `TEST_PASS host.tests.fast`; only an
unfiltered `--lane full` run can emit `TEST_PASS host.tests`. Use `--list` and
`--filter <fixture-or-case>` for focused work; do not generate subset projects.
`tools/OS.Tooling/EfiContracts.cs` owns shared reviewed test/tool expectations.
Derive counts from inventories, normalize source text for semantic checks, and
retain exact raw bytes for build provenance. Do not freeze test-runner source
hashes in other tests. Native orchestration and transcript checks use compiled
C# APIs; fast success is not native, guest, or full acceptance evidence.

## Gitea Issue Workflow

Use the Gitea API when instructed or when doing backlog maintenance.

- Repository: `http://192.168.4.35:3000/admin/CLROS`
- API base: `http://192.168.4.35:3000/api/v1/repos/admin/CLROS` (repository ID 9)

Verify the server and repository identity before tracker writes. This machine's
`localhost` Gitea is a different server. The compiled backlog tool verifies the
LAN repository and supports `--issuekeys` for scoped reconciliation; preview
before `--apply` and read back afterward. Legacy loopback-only helpers must not
be used from this host. See [issue workflow](docs/issue-labels.md) and
[Windows handoff](docs/windows-handoff.md).

Rules:

- Use only credentials supplied by the current task context or secure
  environment. Do not write credentials into files, docs, issues, prompts, or
  logs.
- Review open issues before creating a new one.
- Prefer updating a matching issue over creating a duplicate.
- Pick the first unfinished dependency-compatible `agent:ready` issue in
  `docs/workload-profiles.md`; preserve live ownership and collision lanes.
- Keep issues small, actionable, and detailed enough for another agent to
  implement.
- Include exact files, commands, expected markers, docs to read, and acceptance
  criteria.
- Add labels for agent status, component, phase, and type.
- Close issues only after posting evidence.
- If blocked, state the exact blocker and evidence.

## Implementation Order

Current order: `docs/workload-profiles.md` and the M16/M10/M14 dependency graph.
Qualify a pinned Linux NativeAOT payload first, then continuing post-exit
execution, ELF/user boundary, scheduler and upstream runtime, live TCP/sockets,
and real Kestrel. Two isolated local apps follow before provider qualification.
CoreCLR/framework-dependent hosting and Roslyn/PowerShell are later targets.
Do not make broad custom kernel EH or optional platform work a first-web
prerequisite without a demonstrated kernel requirement.

Kestrel is mandatory, but do not jump to it before boot, runtime, PAL,
scheduler/threading, storage, and networking prerequisites are represented by
issues and evidence.

## Git Rules

The worktree may contain user or other-agent changes.

- Do not revert changes you did not make.
- Do not use `git reset --hard`.
- Do not use `git checkout --` to discard files unless explicitly asked.
- Inspect overlapping files before editing.
- Keep changes scoped to the task.
- Do not commit generated artifacts unless explicitly requested.
- Use `git status --short` before final reporting.

## Final Checklist

Before ending a task:

1. Run the smallest relevant build/test commands.
2. Run `git diff --check`.
3. Update affected docs when behavior, status, commands, or evidence changed.
4. Update or create `done/stepNN.md` for meaningful milestones.
5. Update Gitea issues with evidence when issue work was part of the task and
   credentials are available.
6. Confirm no forbidden first-party source files or secrets were added.
7. Report changed files, tests, evidence, failures, uncommitted files, and the
   next concrete subsystem.

Prefer precise, evidence-backed reporting over optimistic language.
