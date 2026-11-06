# CLROS Claude Agent Guide

Use the [root agent guide](AGENTS.md) and the applicable directory override
for operating rules and ownership. The [documentation index](docs/README.md)
links to current build and qualification instructions. Record individual runs
in the phase contract and immutable evidence steps.

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
.NET 11 NativeAOT Minimal API running Kestrel inside CLROS. The finite
FAT32/Blazor read scope is recorded in [step1672](done/step1672.md).
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

[Step1672](done/step1672.md) records the released read-only FAT32, external
HTTP and standalone Blazor workload. The [PR814 qualification
contract](work/rc3-whpx-qualification.md#candidate-results) records the RC3
candidate and the accepted source of each result. Use the
[completion gates](docs/completion-gates.md) to check broader claims about
writes, reboot persistence, SQLite, sustained service and provider support.

## Workflow

1. Verify the assigned worktree, branch and execution owner before editing.
2. Run `git status --short`.
3. Inspect code and docs before editing.
4. Query live Gitea open issues before choosing work.
5. Within the authorized phase, follow the issue's declared dependencies,
   owner and acceptance criteria.
6. Keep changes narrowly scoped and evidence-backed.
7. Update relevant docs and add/update `done/stepNN.md` for meaningful work.
8. Run the affected checks during a repair. Reserve the full suite and native
   and guest qualification for the stable candidate. Run `git diff --check`.
9. Post Gitea evidence before closing issues.

Use `rg`/`rg --files` for search. Use `apply_patch` for manual edits. Do not
revert user or other-agent changes.

## Gitea

Repository:

```text
http://192.168.4.35:3000/admin/CLROS
```

API base:

```text
http://192.168.4.35:3000/api/v1/repos/admin/CLROS
```

These are the canonical LAN repository9 endpoints. Verify server/repository identity before reconciliation. This host's loopback service is different; use the current compiled backlog tool and [Windows handoff](docs/windows-handoff.md). Preserve historical source-host URLs only as historical references.

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

For PR814, the developer owns qualification and tracker updates. The
documentation owner prepares changes in an isolated checkout and integrates
them after the active source guards are released. Follow the [phase
contract](work/rc3-whpx-qualification.md#completion-and-order) through reviewed
merge, canonical master readback and recoverable cleanup.

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
