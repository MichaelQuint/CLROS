# CLROS AI Agent Guide

## Start here

Read [README](README.md), the [documentation map](docs/documentation-map.md),
[workload profiles](docs/workload-profiles.md) and [completion gates](docs/completion-gates.md)
for current scope. Keep this guide an operating contract; milestone history,
measurements, failures and current run status belong in `done/` and subsystem docs.
A newer reservation or development step is not an accepted milestone.

Before implementation, consult [host requirements](docs/host-requirements.md),
[repo audit](docs/repo-audit.md), [architecture](docs/architecture.md),
[boot](docs/boot.md), [boot order](docs/boot-order.md),
[no-stdlib limits](docs/nativeaot-nostdlib-limits.md), [EFI build](docs/efi-build.md),
the latest evidence step and subsystem doc, plus
`src/OS.Kernel/Boot/BootSequence.cs` and `src/OS.Kernel/Diagnostics/Probes.cs`.
They own boot order and default-off dangerous probes. Directory-local
`AGENTS.override.md` rules apply where they add constraints.

## Product and architecture

The project name is `CLROS`. `OS` remains the stable assembly, namespace, source-path
and ABI prefix; SharpOS and other references are research inputs, not the product
or a drop-in runtime. CLROS targets a minimal single-workload appliance running
supported NativeAOT Minimal API applications continuously until deliberate
shutdown or a documented fault/resource policy. Blazor, logs/uploads and SQLite
need their own complete scenario evidence. Finite diagnostics are test controls,
not the normal service lifetime. No OS user/login system, shell, SSH or remote
administration surface is exposed by default. Cloud/provider readiness requires
separate qualification.

Claims about Kestrel/hosted .NET, real firmware exit, live PCI/storage/networking,
cloud or production readiness require matching evidence in the completion gates.

Keep reusable ownership, memory, scheduling, storage, networking and lifecycle
operations in CLROS core services. Keep Linux syscall/errno/fd/ELF translation in
the adapter. Future PE or flat `OSHost_*` PAL work uses the same core, POD/status
contracts and opaque handles; do not leak managed references, exceptions or
host-owned movable buffers across that boundary. C++ objects, WinAPI handles and
POSIX descriptors must not cross the hosted ABI either.
Ordinary internal C# calls core services directly; do not route PE applications
through Linux syscall frames or force internal calls across the hosted PAL ABI.

## Scope and ownership

PR824 is the finite five-area HTTP performance phase: preserved refactor,
bounded socket copies, readonly DMA, scoped HPET sampling and TCP flight. The
sole implementation owner uses one isolated producer and serializes all builds,
native and guest execution. No implementation subagent or standing helper is
part of this phase. Consult the expert only for one concrete bounded blocker.
The [phase contract](work/http-performance-phase.md) and
[validation map](work/http-performance-validation.md) define its qualification,
retained-source evidence, delivery and stopping boundary. Documentation/metadata
after the frozen source must be identified separately. PR814 and
[Step1790](done/step1790.md) retain their historical accepted baseline.
After verified commit/push/review/merge, canonical master parity and recoverable
preservation of ignored evidence and unfinished work, stop. Keep TCG incomplete
under its unchanged 30-minute limit; begin no PROD/DEV or further performance phase.

Inspect branch, dirty/untracked files, worktrees and live ownership before edits.
Use the explicitly assigned producer checkout; `C:/Projects/CLROS` is not proof
that its current branch is the active qualification source. Preserve unrelated
work and all declared freezes. Do not reset or discard another owner's changes.

Use exactly one storage implementation subagent when parallel work is authorized.
Follow the current path assignments and leases. The development owner handles
shared integration, tracker updates and serialized build and guest runs. Coordinate
documentation commits with that owner so evidence retains its tested source and
producer identities.

Choose one small dependency-compatible slice. Follow exact dependency keys,
owned/forbidden paths and collision lanes from the current canonical issue plus
fresh LAN readback. Explicitly authorized preparation may retain blocked
prerequisites; it does not authorize activation or dependency/issue closure.

## Implementation rules

- Keep first-party kernel/std/app source C#. Do not commit C/C++/headers/assembly.
- Reference adaptation needs pinned provenance, permission, license and file-hash
  review. Build upstream native dependencies externally as guest package inputs.
- Use C# 14 and the exact .NET 11 SDK/runtime/ILC pins. `global.json` and the host
  requirements are authoritative. Restore a missing pin from verified provenance;
  do not silently upgrade, roll forward or weaken the pin to use an installed SDK.
- Prefer the simplest concrete implementation. Add shared BCL-like behavior to
  `std/no-runtime/shared`, source-linked by `src/OS.Std.NoRuntime.Shared`, before
  kernel/app use. Preserve explicit audited EFI compile links.
- Keep `src/OS` freestanding and no-stdlib under its local override. Do not add
  framework references or success-returning runtime stubs to clear a compiler error.
- Before real `ExitBootServices`, copy required firmware data behind owned
  platform interfaces and prepare owned serial diagnostics.
- Use explicit ownership and status results in constrained paths. Kernel GC, EH
  or metadata expansion needs a demonstrated workload requirement and the smallest
  qualified addition; guest GC/EH/BCL behavior belongs in the pinned upstream runtime.
- Avoid exceptions, hidden allocations, reflection and dynamic invocation in
  kernel/boot/runtime paths unless the current issue implements and tests that support.
- Document public/unsafe APIs: purpose, ownership/lifetime, execution context,
  failure and unsupported behavior. Put each type in its own file. Associated
  helpers use Parent.Child.cs; nested helpers retain their identity and visibility
  inside partial containing wrappers. Keep fields/initializers in their original
  order and storage-owning part. Follow root .editorconfig and the host source
  style check; do not add style analyzers to the freestanding compiler.
- Preserve accepted profiles, package bytes, ordinary boot, resource bounds and
  default-off behavior. Changes require their own scoped qualification.

## Build and test

Run only in the assigned checkout with the primary's lane coordination and exact
pinned toolchain. Build current tooling before validation in a fresh checkout.
Use focused checks during implementation; reserve full/native/guest gates for a
stable batch. For final qualification commit/freeze source, rebuild the complete
HostTests graph and verify producer/dependency revisions and
exact input hashes. Keep before/after input guards enabled.

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --lane fast --filter <fixture>
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --lane full
dotnet run --project tools/OS.Tooling -- repo check
dotnet run --project tools/OS.Tooling -- contracts check
dotnet run --project tools/OS.Tooling -- qemu smoke -DryRun
dotnet run --project tools/OS.Tooling -- efi build
git diff --check
```

Use the exact full build baseline and native/QEMU commands in the host/EFI/QEMU
docs. EFI/native/VM/server execution requires the current owner/lease. A Native
lane label may describe expensive synthetic or managed work; report actual scope.

## Evidence and failure handling

Use stable `TEST_START`, `TEST_PASS`, `TEST_FAIL`, `OS_BOOT_OK`, `OS_SMOKE_PASS`
and `OS_SMOKE_FAIL` markers. Serial/raw originals are primary evidence.
Default Fast emits `host.tests.fast`; filtered runs emit `host.tests.focused`;
only a successful unfiltered full may emit `host.tests`. Explicit C# discovery
must have no module-initializer effects; async fixtures are awaited.

Preserve original inputs, receipts, failures and child cleanup. An interrupted,
resumed, reused, copied, replayed, synthetic or Linux-reference run is not a fresh
complete CLROS guest/full result. Report source-known facts, measured observations
and unqualified assumptions separately. Source inspection and host success do not
prove emitted stack/noGC, live DMA, genuine scheduler events, reboot or durability.

For a failure, identify the first invariant and distinguish a kernel defect,
unsupported capability, observer/fixture interference and toolchain/evidence
failure. Make one bounded repair and run the affected check/original under its
owner. Do not relax guards, enlarge budgets, fabricate markers or retry full
qualification without a concrete change or diagnostic hypothesis.

## Documentation, Git and tracker

Use the documentation map and update existing subsystem docs. Preserve every
`done/stepNN.md`; obtain a step number with the tooling's atomic `done reserve`
or `done next` before adding evidence. The primary records changed files,
commands/producer identities, results, originals/failures, limitations and next
dependencies. After each 5–10 issue batch, audit README and all first-party guides
and docs; update every affected statement, canonical record, parent checklist and
Gitea summary/comment. Commit, push, review and merge verified scoped work, read
back remote/master parity, and continue only the authorized scope. PR824 ends
at its explicit clean-stop boundary.

PR812 closed the first-ten read phase at [step1672](done/step1672.md).
PR814 closed at [step1790](done/step1790.md). For PR824, post actual evidence,
reconcile exact dependencies and open broader criteria, review/merge only the
qualified scope, and verify canonical master contains the intended code and docs.
Preserve failures and all ignored evidence before recoverable cleanup, then stop.
The PROD/DEV project split and system stats remain a separate
[milestone](http://192.168.4.35:3000/admin/CLROS/milestone/21).

Stage only intended files. Never use `git reset --hard` or discard unrelated
changes. Preserve needed ignored evidence separately before any approved worktree
archive; a Git snapshot does not retain ignored outputs. Confirm owned children
joined before releasing source, backing or evidence. Report dirty/untracked state.

Canonical Gitea is `http://192.168.4.35:3000/admin/CLROS`, repository9; localhost
is a different service. Verify server/repository identity. Use secure supplied
credentials without printing or storing secrets. Read open issues before adding
one, prefer an existing matching issue, and preserve exact dependencies,
labels, ownership and content versions. Use the current canonical manifest and
compiled backlog tool: scoped preview, versioned apply, independent readback.
Never substitute stale tooling/another manifest or legacy loopback helpers.

Post evidence before closure. Merge/update/close only when the actual required
gates and current authorization are satisfied; read back the resulting branch,
PR/issue state and remote identity. Do not infer broader acceptance from a test
count, successful Linux reference, static source claim or historical parent closure.
When reporting images, attach them in chat so they are viewable remotely.
