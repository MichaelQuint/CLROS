# CLROS Claude Agent Guide

[Step1643](done/step1643.md) retains the failed source044a unfiltered run: 6,158 passed checks, one churn watchdog case failure across809fixture results, zero reuse; the native lane was blocked. The expert-guided test-only change holds the final watchdog continuation while the independent snapshot reader remains active. Seven focused checks pass; application bytes and the1ms watchdog budget are unchanged. This does not fix the separate early-timer exception-classification edge. Timing native review accounts for2076methods, explicit data rebases and a conservative1848-byte own-parent charge; Full/M16 emitted leaves remain identical. All1033 native source inputs per profile still match after the test-only delta. A fresh unfiltered lane and guarded short original remain required; no scheduler change, overall speed gain or Full/browser acceptance follows. Prior e942 finite evidence and every failed original remain separate. #770/#771/#773/#774 and PR812 remain open; writes/SQLite/Gate14 remain unqualified. Finish the read phase, merge/readback and recoverable cleanup, then pause.

[Step1635](done/step1635.md) preserves the first fresh Full018 DiskA run at `f27d42e2` as FAILED at the unchanged1700-second HTTP matrix budget:119 complete responses/8130864B, with the concurrent CoreLib response incomplete at819200/2009369B. The owned runner joined after cancellation; browser and normal guest cleanup were not reached. The same-source7359-check full-host pass and conditional native review do not override that failure.

Historical Step1635 diagnostic checkpoint: a generated-driver child span partitioned the existing dispatch window without changing work, polling, evidence or budgets. At that checkpoint, 28 focused Root host checks passed while its native and original v7 qualification were pending. Later source-matched results are recorded above; the diagnostic does not establish a speed gain or pure guest-CPU measurement.

[Step1624](done/step1624.md) integrates bounded port/filesystem and descriptor/transport range batches, copied syscall identity and fail-closed host repairs. Runtime code `28b6b454` passes the unfiltered Root full r02: 7,319 checks across 918 fixtures (808 Fast, 110 Native), 6,033 inventory cases, unchanged inputs and zero failures/reuse. The exact frozen closing metadata and Host42E/Tooling9B producers are retained; later documentation/backlog changes are a separate metadata delta requiring focused checks. All fourteen selected original recipes meet their own complete consumers and guards. Mechanical30 remains its historical `4e48acc1` guest plus current native equivalence, with conditional stack premises and no fresh28b6 Mechanical30 guest claim. Failed full r01, ordinary guests and timing diagnostics remain failed and retained. [PR810](http://192.168.4.35:3000/admin/CLROS/pulls/810) contains this batch and its published reviews; finite #780/#781/#782/#768/#769 have actual closure readbacks. Pending-read concurrency/cache qualification, complete guest-origin Blazor/browser, actual BCL logs/uploads, reboot persistence, SQLite locking/synchronization/transactions/recovery, physical durability and sustained service remain open.

[Step1614](done/step1614.md) Actual v6 timing r14 passes four responses and exact64KiB with joined intentional cancellation:34.575s phase=16.506s poll+13.374s dispatch outside poll+4.695s supervisor outside poll. It does not qualify full Blazor or normal cleanup. LinuxHTTP r03 is independently joined FAILED before app launch; the pinned BusyBox setsid invocation repair passes19 Fast controls and requires freshLinuxr04. The P helper is integrated; its separate failed ordinary allocation experiment remains unimported/open.

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
general runtime or continuing platform services. Named finite Linux NativeAOT,
live TCP/socket and Kestrel scopes are recorded in steps1307 through1556.
Step1571 adds complete mechanical readonly disk captures; step1580 qualifies
the five-response disk/static development consumer. Full Blazor/browser and
sustained-service acceptance remain open.

Step1613 retains a separate phase-complete dispatch diagnostic with34 focused
Fast passes. Its original native timing was pending; step1614 records the later
actual timing result at its own source scope. Those frozen totals do not prove
guest cleanup. Step1624 integrates port/filesystem, descriptor and transport
batching, copied ordinals and fail-closed host repairs. On4e48acc1 its original
Mechanical30/DiskA guest passes all30 calls and complete cleanup, with exact
ordinary CD5D2A42 emitted-byte and conditional stack joins. The source-matched
unfiltered full r04 naturally joins exit0 with7318 checks/918 fixtures, unchanged
inputs and zero reuse. PR810 still requires the affected original guest/native
regressions and closing review/tracker gates.

Step1612's paired writable block/filesystem/TCP controls are host preparation,
including fake media and managed HTTP processes. Complete native writable
carriers, source5 integration, emitted closure/lifetime, guest writes/reboot
and SQLite durability still require their own evidence. General hosting,
exported socket PAL, isolation, cloud and production remain unqualified.

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

The October27 human phase boundary overrides automatic continuation: finish the current first-ten batch (#770/#771/#773/#774 remain), including qualification, documentation/tracker/evidence, commit/push/review/merge, remote/master readback and safe workspace cleanup. Then pause the broader development goal and stop helper work before any next phase. Preserve unique changes, ignored evidence, failed originals and already-started future preparation recoverably; do not delete them to manufacture a clean workspace.

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
