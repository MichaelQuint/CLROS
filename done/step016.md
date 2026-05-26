# Step 016 - Minimal API Smoke Fixture

## What Changed

- Added `apps/MinimalApiSmoke` as a host-buildable ASP.NET Core .NET 10 Minimal API fixture.
- Added deterministic `GET /` and `GET /healthz` endpoints that return `OS_MINIMAL_API_OK`.
- Added the project to `OS.slnx`.
- Updated `docs/app-model.md` to record that the fixture is host-only and not in-guest Kestrel evidence.

## Why It Changed

Gitea issue #35 needs a small Minimal API/Kestrel workload shape ready for future package and hosted-runtime work without claiming current OS execution. This gives future image packaging and Kestrel smoke work a deterministic endpoint and response body.

## Files Changed

- `apps/MinimalApiSmoke/MinimalApiSmoke.csproj`
- `apps/MinimalApiSmoke/Program.cs`
- `apps/MinimalApiSmoke/appsettings.json`
- `apps/MinimalApiSmoke/appsettings.Development.json`
- `OS.slnx`
- `docs/app-model.md`
- `done/step016.md`

## Tests Run

```powershell
dotnet build .\apps\MinimalApiSmoke\MinimalApiSmoke.csproj --nologo
dotnet run --project .\apps\MinimalApiSmoke\MinimalApiSmoke.csproj --no-build --urls http://127.0.0.1:51235
dotnet build .\OS.slnx --nologo -m:1
```

## Logs and Evidence

- `MinimalApiSmoke` project build passed with 0 warnings and 0 errors.
- Host endpoint probe returned `TEST_PASS minimalapi.host.healthz`.
- The `/healthz` response body was exactly `OS_MINIMAL_API_OK`.
- Full solution build passed with 0 warnings and 0 errors after the transient host-test executable lock cleared.

## Failures

No failures remain for this slice.

## Deferred Work

- Package the fixture into an OS image.
- Launch the fixture through the future hosted .NET 10 runtime boundary.
- Bind Kestrel inside the OS network stack.
- Add QEMU/libvirt request-response evidence for the in-guest Kestrel smoke marker.

## Evidence Boundary

This step proves a host-buildable ASP.NET Core Minimal API fixture and deterministic host response. It does not prove OS image packaging, hosted .NET 10 execution, PAL/socket compatibility, in-guest Kestrel binding, TCP networking, or CloudReady workload support.
