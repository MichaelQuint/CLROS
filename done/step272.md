# Step 272 - MinimalApiSmoke Host Route And Config Contract

## What Changed

- Extended `tests/OS.HostTests/KestrelSmokePackageContractTests.cs` to host-test the real `apps/MinimalApiSmoke` fixture.
- The test now validates the project/source/appsettings contract, publishes the real app, launches the published `MinimalApiSmoke.dll` on a dynamic loopback port, and probes `GET /`, `GET /healthz`, and `GET /config`.
- Added an override case proving `OS_SMOKE_EXPECTED_BODY` controls the response body through configuration/environment rather than a fixed host-only constant.
- Updated `docs/kestrel.md` with the new host-only route/config evidence markers and the claim boundary.

## Why It Changed

Gitea issue #272 required concrete host evidence that the Minimal API smoke fixture is aligned with `kestrel.smoke.package.v0` before any guest Kestrel work can rely on it. This keeps the Kestrel milestone anchored to a deterministic route/config contract without claiming in-guest Kestrel support.

## Files Changed

- `tests/OS.HostTests/KestrelSmokePackageContractTests.cs`
- `docs/kestrel.md`
- `done/step272.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The host harness exited `0` and emitted:

```text
TEST_PASS kestrel.smoke_app host fixture publishes
TEST_PASS kestrel.smoke_app host healthz returns configured body
TEST_PASS kestrel.smoke_app host config exposes contract metadata
TEST_PASS kestrel.smoke_app host env override controls body
TEST_PASS host.tests
```

The route/config checks also require the default body hash for `OS_MINIMAL_API_OK`:

```text
87b557229560bb3b737957331c4690b801c50d452a306097fafa8f4f0f4dfe3d
```

## Evidence Boundary

This is host fixture evidence only. It does not prove guest package loading, hosted .NET 10 startup in OS, PAL config propagation, exported PAL file/config/socket functions, guest TCP, socket binding, or Kestrel inside OS. It must not be reported as `TEST_PASS kestrel.smoke`.

## Failures And Deferred Work

- The first rebuild attempt hit a transient file lock from another `.NET Host` process in the shared dirty tree. A rerun after the lock cleared passed.
- In-guest Minimal API/Kestrel proof remains blocked on hosted runtime, PAL, package loading, scheduler/threading, timers, TCP/socket, and QEMU/libvirt external HTTP evidence.
