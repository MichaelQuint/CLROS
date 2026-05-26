# Step 014 - Hosted .NET 10 Minimal API Kestrel Ledger

## What Changed

- Added `docs/kestrel.md` as the documentation-only prerequisite ledger for hosted .NET 10, ASP.NET Minimal API, and Kestrel support.
- Defined the first acceptable in-guest Kestrel proof.
- Added required `kestrel.smoke` serial markers.
- Separated host-build fixture evidence from real in-guest Kestrel evidence.
- Documented that `TinyHttpServer` is not a Kestrel substitute.
- Added a short cross-reference from `docs/completion-gates.md` to the Kestrel ledger.

## Why It Changed

Kestrel is mandatory for the CloudReady product goal, but it depends on hosted runtime, PAL, networking, scheduler, async, storage, configuration, and logging work that is not implemented yet. This ledger prevents host-only fixtures or native-tier HTTP demos from being mistaken for Kestrel evidence.

## Files Changed

- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step014.md`

## Tests Run

```powershell
rg -n "TEST_START kestrel.smoke|TinyHttpServer is not a Kestrel substitute|Host-build fixture evidence|In-guest Kestrel evidence" .\docs\kestrel.md
git diff --check -- .\docs\kestrel.md .\docs\completion-gates.md .\done\step014.md
```

## Logs And Evidence

- Documentation ledger includes `TEST_START kestrel.smoke`, `TEST_PASS kestrel.smoke`, and `TEST_FAIL kestrel.smoke reason=<code>`.
- Documentation ledger includes the first acceptable proof: boot OS, launch packaged Minimal API app, bind Kestrel endpoint, serve deterministic HTTP response, and capture serial/runner evidence.
- Documentation ledger explicitly separates host-build fixture evidence from in-guest evidence.
- Documentation ledger states `TinyHttpServer` is not a Kestrel substitute.

## Failures

No implementation failures are associated with this documentation-only slice.

No build was run because the task was documentation-only and ownership excluded source, tool, app, and test files.

## Deferred Work

- Hosted .NET 10 runtime implementation.
- CoreCLR/PAL boundary implementation.
- Sockets, TCP, timers, scheduler/threading, async/task/cancellation, file/config reads, environment variables, and logging support.
- Minimal API fixture project and in-guest `kestrel.smoke` runner verification.

## Evidence Boundary

This step proves the Kestrel prerequisite ledger exists and defines the evidence contract. It does not prove hosted .NET 10 runtime execution, ASP.NET Minimal API launch, Kestrel socket binding, TCP networking, app package loading, or in-guest HTTP request/response behavior.
