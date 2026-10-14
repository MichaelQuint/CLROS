# CLROS Claude Agent Guide

This file is a compact guide for Claude agents working in this repository. The
root operating contract remains `AGENTS.md`; use this file as a quick handoff,
not as a replacement for canonical repo docs.

## Project Identity

Project name is `CLROS`.

Do not call this project `OS` or `SharpOS`. `OS` remains the stable assembly,
namespace, source-path, and `OSHost_*` ABI prefix. Reference repositories under
`references/` are not drop-in first-party architecture. Selective source
adaptation is allowed only under the provenance, permission, license, and
file-hash rules in `references/AGENTS.override.md` and
`docs/license-audit.md`.

`CLROS` is a C#-first NativeAOT operating-system project targeting a minimal
CloudReady single-workload appliance model. The first workload milestone is a
.NET 11 NativeAOT Minimal API running Kestrel inside CLROS; it remains incomplete.
Framework-dependent/CoreCLR hosting follows later. Read the NativeAOT-first
decision in `docs/workload-profiles.md` before selecting work.

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
- Use C# 14.0 and the exact .NET 11 pins in `global.json` and the host guide.
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
- The NativeAOT-first roadmap authorizes the required Linux x64 application
  syscall adapter in C#. Keep its errno/fd semantics separate from `OSHost_*`.
- Never write credentials or secrets into files, issues, logs, commits, or
  prompts. Use only credentials supplied by the active task context.

## Current Evidence Boundary

The repository has host-tested contracts and accepted QEMU/OVMF profiles for
firmware-visible boot, bounded real firmware exit, owned post-exit diagnostics
and subsystem use, and selected runtime operations. Each accepted profile has
its own scope in `docs/completion-gates.md`; these results do not establish a
general runtime or continuing platform services. Storage, PAL, app-model, and
networking slices remain host-tested parser, planning, mock-table, or
caller-owned-buffer evidence only.

Networking currently includes host-tested UDP/TCP checksum slices through
active/passive handshakes, HTTP GET payload frames, and active close FIN/ACK
frame generation. This is not live virtio-net I/O, a complete TCP stack,
exported PAL sockets, hosted .NET, or Kestrel.

Use `docs/completion-gates.md` and the latest `done/stepNN.md` for exact
claim boundaries.

## Workflow

1. Start in `C:\Projects\CLROS`.
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
http://localhost:3000/admin/CLROS
```

API base:

```text
http://localhost:3000/api/v1/repos/admin/CLROS
```

These loopback addresses identify the source development machine's Gitea.
Follow `docs/windows-handoff.md` on another machine; its localhost may host a
different server. Existing local REST helpers enforce the source endpoint.

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

Common commands from `C:\Projects\CLROS`:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project tools/OS.Tooling -- repo check
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
dotnet run --project tools/OS.Tooling -- efi build
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
