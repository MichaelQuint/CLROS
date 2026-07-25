# Step 115 - PAL Mock Socket Accept Queue

## What Changed

- Added a mock accept-queue contract to `OSHostSocketTable`.
- Added `OSHostSocketAcceptResult` and a `Connected` mock socket lifecycle state.
- Extended table snapshots with remote endpoint metadata and pending accept counts.
- Added host-test coverage for queued accepts, non-listener rejection, empty queues, backlog enforcement, out-of-slot behavior, and deterministic close behavior.
- Updated PAL, Kestrel, and completion-gate docs with the issue #133 evidence boundary.

## Why It Changed

Issue #133 moves the PAL socket model one step beyond create/bind/listen toward the future `OSHost_SocketAccept` contract required by hosted .NET 10 and Kestrel. This remains an in-process mock model so the contract can be tested before exported PAL symbols, real TCP sockets, or a hosted runtime exist.

## Files Changed

- `src/OS.Kernel/Pal/OSHostSocketTable.cs`
- `tests/OS.HostTests/PalSocketTableTests.cs`
- `docs/pal.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step115.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build > .\artifacts\logs\host-tests-current.log
.\scripts\check-naming.ps1
rg -n "pal\.socket accept queues connection|pal\.socket accept rejects non-listener|pal\.socket accept reports empty queue|accept-queue|OSHostSocketAcceptResult" .\src\OS.Kernel\Pal\OSHostSocketTable.cs .\tests\OS.HostTests\PalSocketTableTests.cs .\docs\pal.md .\docs\kestrel.md .\docs\completion-gates.md
git diff --check -- .\src\OS.Kernel\Pal\OSHostSocketTable.cs .\tests\OS.HostTests\PalSocketTableTests.cs .\docs\pal.md .\docs\kestrel.md .\docs\completion-gates.md .\done\step115.md
```

## Logs And Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- Full host tests emitted:
  - `TEST_PASS pal.socket accept queues connection`
  - `TEST_PASS pal.socket accept rejects non-listener`
  - `TEST_PASS pal.socket accept reports empty queue`
  - `TEST_PASS host.tests`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` passed for the issue #133 files with existing LF-to-CRLF warnings on docs.

## Failures

- None in the parent-owned verification run.

## Deferred Work

- No exported `OSHost_SocketAccept` symbol.
- No send, receive, poll, shutdown, or socket options.
- No real TCP listener, virtio-net driver, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel smoke.

## Evidence Boundary

This step is a host-compiled mock PAL socket table contract only. It does not prove guest sockets, exported PAL behavior, TCP packet I/O, CoreCLR, ASP.NET Minimal API, or Kestrel.
