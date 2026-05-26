# Step 260 - Host-Publishable Kestrel Smoke App Template

## Summary

Updated the existing `apps/MinimalApiSmoke` ASP.NET Core Minimal API fixture so
it can be published on the host into the `kestrel.smoke.package.v0` package
layout with deterministic host validation markers.

## Why

Gitea issue #260 needed a small host-publishable .NET 10 Kestrel smoke app
template before any guest hosted .NET, PAL, storage, socket, TCP, or Kestrel
execution path exists.

## Files changed

- `apps/MinimalApiSmoke/Program.cs`
- `apps/MinimalApiSmoke/MinimalApiSmoke.csproj`
- `apps/MinimalApiSmoke/appsettings.json`
- `apps/MinimalApiSmoke/publish-kestrel-smoke-package.ps1`
- `docs/kestrel.md`
- `docs/app-model.md`
- `done/step260.md`

## Tests run

- `dotnet build .\apps\MinimalApiSmoke\MinimalApiSmoke.csproj --nologo -m:1`
- `.\apps\MinimalApiSmoke\publish-kestrel-smoke-package.ps1`
- Host loopback endpoint probe against `http://127.0.0.1:18080/healthz` and
  `http://127.0.0.1:18080/config`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `MinimalApiSmoke` project build passed with `Build succeeded.`, `0
  Warning(s)`, and `0 Error(s)`.
- The host publish script emitted:
  - `TEST_PASS kestrel.smoke_app publishes package`
  - `TEST_PASS kestrel.smoke_app validates package contract`
  - `TEST_PASS kestrel.smoke_app rejects missing expected body`
  - `TEST_PASS kestrel.smoke_app`
- The publish script staged:
  - `artifacts/kestrel-smoke/package.json`
  - `artifacts/kestrel-smoke/package-root/apps/minimal-api/MinimalApiSmoke.dll`
  - `artifacts/kestrel-smoke/package-root/apps/minimal-api/MinimalApiSmoke.deps.json`
  - `artifacts/kestrel-smoke/package-root/apps/minimal-api/MinimalApiSmoke.runtimeconfig.json`
  - `artifacts/kestrel-smoke/package-root/apps/minimal-api/appsettings.json`
- The host loopback endpoint probe emitted
  `TEST_PASS kestrel.smoke_app host endpoints`.
- The full solution build passed on retry with `Build succeeded.`, `0
  Warning(s)`, and `0 Error(s)`.
- The full host-test run emitted `TEST_PASS host.tests`.
- `git diff --check` exited `0`.

## Failures

- The first full solution build attempt failed because another running
  `OS.HostTests` process, PID `8032`, held
  `tests/OS.HostTests/bin/Debug/net10.0/OS.Kernel.dll` and
  `OS.Std.NoRuntime.Shared.dll`. The process exited without intervention, and
  the build retry passed.

## Deferred work

- This is host publish and host package-contract validation only.
- It does not prove hosted .NET 10 execution inside OS, CoreCLR/PAL
  integration, guest package loading, manifest-to-hosted-runtime configuration
  propagation, socket binding, TCP listener behavior, QEMU/libvirt HTTP
  response evidence, TLS, or cloud-provider boot.
