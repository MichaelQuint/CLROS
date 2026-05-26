# Step 261 - OSHost Socket ABI Contract for Hosted Kestrel Smoke

## What Changed

- Added the missing `OSHostStatus.Closed` / `OSHOST_STATUS_CLOSED` status code
  for closed socket-handle paths.
- Added POD socket request/output contracts for bind, listen, accept, connect,
  shutdown, option set, and endpoint query operations.
- Added focused host tests with the issue #261 marker names for POD layout,
  status-code naming, and managed-boundary rejection.
- Updated the PAL and Kestrel docs with function-level ownership,
  status-code, timeout, and manifest-policy rules.

## Why It Changed

Issue #261 needs the smallest flat `OSHost_*` socket ABI that a future hosted
.NET 10/Kestrel adapter can target. This step defines the contract and layout
in C# only. It does not export PAL symbols, implement TCP, launch hosted
CoreCLR, or run Kestrel.

## Files Changed

- `src/OS.Kernel/Pal/OSHostAbi.cs`
- `src/OS.Kernel/Pal/OSHostSocketAbi.cs`
- `tests/OS.HostTests/PalAbiLayoutTests.cs`
- `tests/OS.HostTests/PalSocketAbiTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `docs/kestrel.md`
- `done/step261.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check
```

## Logs And Evidence

`dotnet build .\OS.slnx --nologo -m:1` passed with:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

`dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
passed and emitted the focused #261 markers:

```text
TEST_PASS pal.socket.abi validates pod layouts
TEST_PASS pal.socket.abi validates status codes
TEST_PASS pal.socket.abi rejects managed boundary assumptions
```

`git diff --check` exited `0`. It printed only existing line-ending
normalization warnings for files in the working tree.

## Failures

None.

## Deferred Work

- Export `OSHost_*` socket symbols only when a hosted runtime adapter needs
  linkable functions.
- Back those symbols with real TCP, timers, wait, cancellation, and scheduler
  behavior.
- Prove `TEST_PASS pal.socket.listen` in guest before using socket evidence for
  hosted .NET.
- Launch packaged ASP.NET Minimal API/Kestrel in guest and validate external
  HTTP response before claiming Kestrel support.
