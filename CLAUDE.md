# OS Claude Agent Guide

This file is a compact guide for Claude agents working in this repository. The
root operating contract remains `AGENTS.md`; use this file as a quick handoff,
not as a replacement for canonical repo docs.

## Project Identity

Project name is `OS`.

Do not call this project `SharpOS`. Reference repositories under `references/`
are study material only; do not treat them as first-party architecture or source
to copy.

`OS` is a C#-first NativeAOT operating system targeting a minimal CloudReady
single-workload appliance model. Hosted .NET 10 applications and ASP.NET
Minimal API/Kestrel support are mandatory milestones, but they are not
implemented yet.

Do not claim hosted .NET, Kestrel, Azure/AWS readiness, cloud readiness,
production readiness, live storage/networking, or real `ExitBootServices`
without matching evidence in `docs/completion-gates.md`.

## Required First Reads

Before meaningful work, read:

- `AGENTS.md`
- `README.md`
- `docs/documentation-map.md`
- `docs/host-requirements.md`
- `docs/completion-gates.md`
- latest `done/stepNN.md`
- `src/OS.Kernel/Boot/BootSequence.cs`
- `src/OS.Kernel/Diagnostics/Probes.cs`

Then read the matching subsystem docs:

- Boot/runtime: `docs/boot.md`, `docs/efi-build.md`,
  `docs/nativeaot-nostdlib-limits.md`
- PAL/hosted runtime: `docs/pal.md`, `work/PAL/pal-design.md`,
  `docs/kestrel.md`
- Storage/image: `docs/cstorage.md`, `docs/image-builder.md`
- Networking/Kestrel socket ladder: `docs/networking.md`, `docs/kestrel.md`
- App model/manifest: `docs/app-model.md`, `docs/manifest.md`
- Cloud/QEMU/libvirt: `docs/qemu.md`, `docs/libvirt.md`,
  `docs/cloud-provider-requirements.md`

## Non-Negotiables

- First-party kernel/std/app source committed here must be C#.
- Do not commit `.c`, `.cpp`, `.h`, `.asm`, or `.s` files.
- Use C# 14.0 and .NET 10 for normal managed projects.
- Keep `src/OS.Kernel/Boot/BootSequence.cs` as the source of truth for boot
  ordering and readiness markers.
- Keep dangerous probe toggles in
  `src/OS.Kernel/Diagnostics/Probes.cs` disabled by default.
- Add shared BCL-like utilities under `std/no-runtime/shared` before kernel or
  app usage.
- Avoid exceptions, hidden allocations, reflection, dynamic invocation, and
  managed-reference ABI leakage in kernel/boot/runtime paths unless the issue is
  implementing and testing that support.
- Future hosted-runtime PAL work uses flat `OSHost_*` POD/status-code ABI
  contracts only. No managed references, C++ objects, WinAPI handles, POSIX
  descriptors, exceptions, or host-owned movable buffers cross that boundary.
- Do not add POSIX/Linux compatibility unless a documented PAL/CoreCLR step
  requires it.
- Never write credentials or secrets into files, issues, logs, commits, or
  prompts. Use only credentials supplied by the active task context.

## Current Evidence Boundary

The current repository is early boot/runtime boundary work plus host-tested
contracts and QEMU/OVMF smoke evidence for the firmware-visible marker path.
Storage, PAL, app-model, and networking slices are host-tested parser,
planning, mock-table, or caller-owned-buffer evidence only.

Networking currently includes host-tested UDP/TCP checksum slices through
active/passive handshakes, HTTP GET payload frames, and active close FIN/ACK
frame generation. This is not live virtio-net I/O, a complete TCP stack,
exported PAL sockets, hosted .NET, or Kestrel.

Use `docs/completion-gates.md` and the latest `done/stepNN.md` for exact
claim boundaries.

## Workflow

1. Start in `C:\Projects\OS`.
2. Run `git status --short`.
3. Inspect code and docs before editing.
4. Query live Gitea open issues before choosing work.
5. Pick the smallest unblocked `agent:ready` issue. If none exists, create or
   update small actionable issues with labels.
6. Keep changes narrowly scoped and evidence-backed.
7. Update relevant docs and add/update `done/stepNN.md` for meaningful work.
8. Run focused build/tests, full solution build for code changes, and
   `git diff --check`.
9. Post Gitea evidence before closing issues.

Use `rg`/`rg --files` for search. Use `apply_patch` for manual edits. Do not
revert user or other-agent changes.

## Gitea

Repository:

```text
http://192.168.2.42:3000/michael.quint/OS
```

API base:

```text
http://192.168.2.42:3000/api/v1/repos/michael.quint/OS
```

Use Basic Auth only from the active task context or secure environment. Do not
store credentials in this file or any issue body.

Issue hygiene:

- Review open issues before creating new ones.
- Prefer updating a matching issue over duplicating it.
- Keep issue slices small, actionable, and detailed.
- Include exact files, commands, markers, acceptance criteria, and docs to
  update.
- Add status, component, phase, and type labels.
- Close issues only after posting evidence.

## Evidence Commands

Common commands from `C:\Projects\OS`:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\OS.slnx --nologo -m:1
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun
.\scripts\build-efi-shell.ps1
git diff --check
```

Stable markers:

```text
TEST_START <name>
TEST_PASS <name>
TEST_FAIL <name> <reason>
OS_BOOT_OK
OS_SMOKE_PASS
OS_SMOKE_FAIL
```

Dry-run QEMU output is not boot proof. Boot proof requires a generated image,
OVMF/QEMU execution, captured serial logs, and expected markers.

## Documentation

Repo docs are canonical for build/run commands, ABI contracts, completion gates,
subsystem status, and evidence. The Gitea wiki may contain only
reader-friendly summaries that link back to repo docs and issues.

Use `docs/documentation-map.md` before adding or moving docs. Do not delete
`done/stepNN.md` history.

## Reporting

Every completed slice should report:

- what changed
- why it changed
- files changed
- tests run
- logs/evidence
- failures or unverified areas
- next concrete subsystem

Prefer precise evidence boundaries over optimistic language.
