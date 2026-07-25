# Step 279 - PAL Socket Facade Host Contract

## What Changed

- Added `OSHostSocketFacade` as a host-tested `OSHost_*`-shaped adapter over
  `OSHostSocketTable`.
- Covered create, bind, listen, accept, send, receive, poll, shutdown, socket
  option, address-query, and close status translation.
- Added host tests for facade lifecycle, endpoint output, partial I/O,
  close/reset poll states, invalid-handle translation, and close invalidation.
- Updated PAL, networking, Kestrel, and completion-gate docs.

## Why It Changed

The existing socket table models Kestrel-relevant socket behavior, but future
hosted .NET work also needs a flat ABI-shaped layer. This step proves that
socket table behavior can be reduced to handle/POD/status-style calls before
any unmanaged `OSHost_Socket*` symbols are exported.

## Files Changed

- `src/OS.Kernel/Pal/OSHostSocketFacade.cs`
- `tests/OS.HostTests/PalSocketFacadeTests.cs`
- `docs/pal.md`
- `docs/networking.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `done/step279.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll
```

## Logs And Evidence

The host-test project build passed:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

The first focused host run emitted all new facade markers, then an unrelated
`qemu.runner.http_probe rejects body mismatch` timeout occurred. A rerun of the
full host test harness passed the HTTP probe suite and `TEST_PASS host.tests`.

New facade markers:

```text
TEST_PASS pal.socket facade creates binds and listens
TEST_PASS pal.socket facade accepts with endpoint output
TEST_PASS pal.socket facade preserves partial io
TEST_PASS pal.socket facade polls close and reset states
TEST_PASS pal.socket facade translates invalid handles
TEST_PASS pal.socket facade close invalidates handle
```

## Failures

One transient host-test failure occurred in the QEMU runner HTTP probe test,
outside this slice. The same test suite passed on rerun.

## Evidence Boundary

This is host-only PAL facade evidence. It does not export unmanaged
`OSHost_Socket*` symbols, implement TCP FIN/RST packet behavior, integrate
CoreCLR, run hosted .NET 10, or prove ASP.NET Minimal API/Kestrel behavior.

## Deferred Work

- Export actual unmanaged `OSHost_Socket*` functions when the hosted runtime
  integration reaches the ABI boundary.
- Back the facade with real TCP/listener behavior after virtio-net, timers, and
  scheduler prerequisites are in place.
