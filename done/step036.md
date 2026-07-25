# Step 036 - Hosted .NET 10 Kestrel Dependency Map

## What Changed

- Updated `docs/kestrel.md` for Gitea issue #59.
- Added an ordered dependency map for the mandatory but incomplete .NET 10 Minimal API/Kestrel milestone.
- Clarified that Kestrel support cannot be claimed from host-only builds, dry-run probes, mock-only PAL tests, native-tier HTTP demos, or documentation alone.
- Added future runner markers and failure conditions for `kestrel.smoke`.

## Why It Changed

The hosted workload goal needs a precise prerequisite ladder so future agents do not move Kestrel ahead of boot proof, UEFI encapsulation, PAL ABI work, runtime helpers, scheduler/threading, timers, storage/package loading, manifest configuration, virtio-net, DHCP/DNS/TCP, PAL sockets, and hosted runtime startup.

## Files Changed

- `docs/kestrel.md`
- `done/step036.md`

## Tests Run

```powershell
rg -n "Gitea issue #59|## Dependency Map|Boot proof|UEFI encapsulation|PAL ABI|Runtime helpers|scheduler|virtio-net|DHCP|DNS|TCP|TEST_START kestrel.smoke|HTTP_PROBE_START|dry-run|host-only" .\docs\kestrel.md .\docs\completion-gates.md
git diff --check -- .\docs\kestrel.md .\done\step036.md
git diff --no-index --check -- <empty-file> .\docs\kestrel.md
git diff --no-index --check -- <empty-file> .\done\step036.md
```

## Evidence

- `docs/kestrel.md` states Kestrel is mandatory but incomplete.
- The dependency map lists the exact prerequisite ladder before Kestrel can be claimed.
- The runner section requires real guest serial markers plus HTTP response validation and defines failure conditions.
- `docs/completion-gates.md` still marks `Hosted .NET 10 Minimal API/Kestrel` as `Fail`.
- The no-index whitespace checks are needed while both touched docs are untracked.

## Failures

None for this documentation slice.

## Deferred Work

- Implement the prerequisite boot, UEFI, runtime, PAL, storage, networking, hosted runtime, and runner HTTP verification work.
- Add implementation issues from the dependency map.
- Keep Gitea issue #59 open for main-thread verification and closure.

## Evidence Boundary

This step is documentation only. It does not implement hosted .NET 10, CoreCLR PAL integration, sockets, TCP, ASP.NET Minimal API launch, Kestrel binding, real HTTP response verification, TLS, HTTPS, or any CloudReady hosted workload support.
