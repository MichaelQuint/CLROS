# CLROS AI Agent Guide

The #141–#144/#635 bounded modern RX/header/ARP batch is qualified in
`done/step1164.md`: 4,441 full-host checks, linked native review, a 194-record
ARP guest capture, independent request/reply libpcap and thirteen ancestor
regressions pass. The default-off profile receives one matching reply and checks
supplied-tick neighbor cache controls. Continuing storage/networking, application
execution, hosted .NET/Kestrel and later completion gates remain incomplete.

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
- Do not add POSIX/Linux compatibility unless a documented PAL/CoreCLR
  integration step explicitly requires it.
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

- Repository: `http://localhost:3000/admin/CLROS`
- API base: `http://localhost:3000/api/v1/repos/admin/CLROS`

These loopback addresses identify Gitea on the current source computer. On
another machine, verify the intended server and `admin/CLROS` repository
against the final handoff before any tracker operation. The destination
machine's own `localhost` Gitea is a different server. The default handoff
continues to use the source server at `192.168.4.35`; verify that address
on arrival. Existing loopback-only helpers and backlog synchronization run
on the source host unless endpoint support is explicitly reviewed. See
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

Default order unless issue state proves a dependency is done:

1. Audit and baseline.
2. Diagnostics first.
3. `std/no-runtime` base.
4. Runtime correctness.
5. PAL and UEFI encapsulation.
6. Platform/drivers.
7. Storage.
8. Networking.
9. Native-tier app model.
10. Cloud image tooling.
11. Security/hardening.
12. Hosted .NET 10, Minimal API, Kestrel.
13. Roslyn/PowerShell hosted-tier expansion.

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
