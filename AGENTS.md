# CLROS AI Agent Guide

## Start here

1. Read `docs/workload-profiles.md` for the current NativeAOT-first decision.
2. Refresh the LAN tracker and select the first unfinished dependency-compatible
   NativeAOT-first ticket. M16-001/#679 through M16-007/#685 are qualified;
   the next foundation is [M09-001 / #183](http://192.168.4.35:3000/admin/CLROS/issues/183).
   M16-008/#686 still depends on #188/#191/#192; reconcile #183's unrelated
   network acceptance boilerplate before claiming its thread-context slice.
   Use exact dependencies and acceptance from the managed issue, not numeric
   milestone order or a generic "smallest ready" optional ticket.
3. Preserve existing work. Pick one cohesive implementation slice, reuse the
   ticket's exact source references, run focused checks first, then qualify
   the stable guest batch. Do not expand general kernel EH without a need.
4. Keep one main public type per new file where practical. Document public and
   unsafe APIs with purpose, ownership/lifetime, execution context, failure
   behavior and unsupported cases; avoid boilerplate comments.
5. The shared host project is `src/OS.Std.NoRuntime.Shared`. Its audited
   implementation source remains under `std/no-runtime/shared` and appears as
   linked files. Do not duplicate those types or move EFI source paths casually.

The #684/#706 default-off memory/startup batch is qualified in `done/step1205.md`:
4,646 full-host checks across 608 fixtures (94 Native), original scoped native
review, three 150-record high-output guests, two controlled memory negatives and
twelve ancestors pass. Sparse user reservations reach 64 GiB; physical/table
bounds remain below 4 GiB. A mechanical ELF replays the 8,234,266,624-byte
reservation with bounded backing and proves high copy-in/out, live monotonic
time, fresh entropy and owned restoration. The unchanged upstream payload stops
at unsupported ARCH_SET_FS, before Hello. Descriptors/pipes, threads/TLS/futex/
signals, upstream GC/EH/ThreadPool, Kestrel, numbered/cloud/production gates remain
incomplete.

The #685/#707 default-off descriptor/pipe child is qualified in `done/step1206.md`:
4,679 full-host checks across 613 fixtures (94 Native), eight original native
bodies, three 173-record descriptor guests, three 175-record pipe guests and
fifteen ancestors pass. Original package/proc views, exact fd/pipe errors and
last-close ownership are genuine; buffers are checked after release. Blocking
pipes belong to #686; first unchanged Hello to #687. General threads/signals,
upstream runtime, Kestrel and numbered/cloud/production gates remain incomplete.

The #683/#699 default-off CPL3/syscall batch is qualified in `done/step1201.md`:
4,619 full-host checks across 604 groups (94 Native), original scoped native
review, three 121-record write/exit guests, four 112-record controlled-negative
guests and seven ancestors pass. A C#-authored mechanical ELF checks actual
selectors, errno, cross-page write, GPR/flag/FS/GS/SSE preservation and exit 42.
Owned NULL FS/GS selectors are established before the restoration baseline.
General processes, threads/TLS/futex/signals, real NativeAOT Hello (#687), guest
application GC/EH, Kestrel, numbered gates, cloud and production remain incomplete.

The corrected handoff is recorded in `done/step1202.md`. #684 distinguishes
sparse virtual reservations from committed pages: the pinned Hello reserves
8,234,266,624 bytes with `PROT_NONE`, beyond the earlier 4-GiB user range.
#685 qualifies mechanical descriptors/pipes; #686 qualifies mechanical
threads/TLS/futex, blocking pipes and real per-thread masks; #687 adds signal
delivery and runs the first unchanged pinned Hello before focused suspension.
#688 repeats Hello and qualifies the full runtime after GC/ThreadPool checks.
Stock Linux/musl remains primary. Host transport and optional WASI HTTP studies
are separate research, not dependencies of #684 or substitutes for guest Kestrel.

The #662/#663/#664 default-off caller-window batch is qualified in
`done/step1187.md`: 4532 full-host checks across 587 groups (92 Native), original native review,
three byte-identical 95-record guests and twelve ancestors pass. One no-EH frame
and two real callers feed the existing cleanup/typed dispatch planner. Actual
return RIP is retained; exact EH lengths come from the original compiler catalog.
Measured parent/funclet ABI and retained lifetime guards remain. General EH, arbitrary methods, dynamic module lifetimes, collided unwind, #90/Gate4, hosted .NET/Kestrel, cloud and production remain incomplete.

The #654/#655/#656 default-off filtered-catch batch is qualified in
`done/step1181.md`: 4518 full-host checks, original native review, three
88-record filter guests and eleven ancestors pass. Two same-range nonthrowing
compiler filters share the original parent local; only the second catch and
compiled continuation execute. General EH, #90/#91/#84, Gate4 and hosted
.NET/Kestrel remain incomplete.

This is the root operating contract for agents working in this repository. Keep
it concise. Put subsystem detail in the relevant `docs/*.md` file and use
`docs/documentation-map.md` to find the canonical source for each topic.

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
- .NET 10 applications as first-class workloads.
- ASP.NET Minimal API/Kestrel support as a mandatory milestone.

Ordinary `VerifiedExit` is accepted in `done/step1094.md`: three frozen-source
79-record QEMU/OVMF captures leave boot services and finish original owned
subsystem checks before terminal success. Explicit `Diagnostic` retains the
legacy profiles; dangerous probe defaults remain false. This does not add
firmware-page reclamation or a continuing workload.

Current implementation is bounded boot/runtime work with host-tested contracts
and accepted QEMU/OVMF profiles for real firmware exit, owned diagnostics and
subsystem use, allocation/collection, constructors, dispatch and unwind metadata.
One-frame restoration and EH-clause decoding retain their accepted host-only
contracts. The separate bounded throw/catch profile has final acceptance in
`done/step1075.md`: one real throw, typed catch and compiled continuation, with
local-input admission rejection. `done/step1088.md` accepts a separate bounded
compiler-finally profile: one cleanup before typed catch and compiled continuation,
with the original exception and checked parent-stack side effect. The bounded
rethrow and post-firmware UC/WC/WB mapping profiles are accepted in
`done/step1120.md`. Rethrow reaches the outer typed handler and compiled
continuation with the same rooted exception. General EH and arbitrary rethrow
remain incomplete. The #638–#640 default-off typed-search/two-cleanup profile is
qualified in `done/step1167.md`: 4,456 full-host checks, native controls, three
89-record guests and five ancestor regressions pass. An annotation/digest-only
follow-up passes six focused checks and three fresh byte-identical guests.
#90, constructor cached failures and Gate4 remain incomplete. See
`docs/completion-gates.md` for each scope.
The #650-#652 default-off constructor-failure batch is qualified in
`done/step1176.md`: 4,502 full-host checks, native review, three 92-record guests
and ten ancestors pass. One real constructor throw is wrapped and genuinely
thrown to an outer typed catch and compiled continuation. Only one pinned Read
body branch is interpreted locally; general unwinding is unchanged. Repeated
compiler access/cached rethrow, #90/#84 and Gate4 remain incomplete.

The #646-#648 bounded physical-frame dispatch batch is qualified in
`done/step1171.md`: 4,488 full-host checks, final native review, three 88-record
frame guests and seven ancestors pass. The separate default-off `M04FrameDispatch`
profile executes one earlier-frame cleanup, outer typed catch and compiled
continuation with distinct caller contexts. #90, general EH and Gate4 remain open.
The #642–#644 default-off frame-search/type-predicate/unhandled batch is qualified
in `done/step1169.md`: 4,473 full-host checks, native review, three 73-record
expected-terminal guests and six ancestor captures pass. It stops after three
frames and one nonmatching catch without cleanup or handler transfer. General
multi-frame dispatch, general EH, constructor cached failures and Gate4 remain incomplete.
Bounded modern Virtio block binding, feature negotiation, owned DMA layout and
queue setup are accepted in `done/step1140.md`. One default-off sector read with
checked completion and reset/allocator cleanup is accepted in `done/step1143.md`.
A one-attempt live block-contract adapter and protective LBA0 validation are
accepted in `done/step1147.md`. General workload manifest/package loading, continuing storage
and Gate 7 remain incomplete.
PAL and app-model progress remains host-tested contract/planning evidence;
networking includes UDP/TCP checksum
slices through active/passive handshakes, HTTP GET payload frames, and active
close FIN/ACK frame generation. It is not a functional kernel, not cloud-ready,
and not production-ready.

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
- Use C# 14.0 and .NET 10 for normal managed projects.
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

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --lane full
dotnet run --project .\tools\OS.Tooling -- repo check
dotnet run --project .\tools\OS.Tooling -- contracts check
dotnet run --project tools/OS.Tooling -- repo check
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
- Pick the smallest `agent:ready` issue that is not blocked.
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
