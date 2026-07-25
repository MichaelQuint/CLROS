# Step 253 - Kestrel Smoke Package Contract Validator

## Summary

Added a host-side `kestrel.smoke.package.v0` package layout validator through
`OS.ManifestTool validate-kestrel-package`.

## Why

Issue #253 needed a deterministic host contract for the first ASP.NET Minimal
API/Kestrel smoke package before guest package reads, hosted .NET 10, PAL
exports, sockets, TCP, or Kestrel execution exist.

## Files changed

- `tools/OS.ManifestTool/Program.cs`
- `tools/OS.ManifestTool/KestrelSmokePackageCommand.cs`
- `tests/OS.HostTests/KestrelSmokePackageContractTests.cs`
- `docs/kestrel.md`
- `done/step253.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Solution build passed with `Build succeeded.`, `0 Warning(s)`, and
  `0 Error(s)`.
- Full host tests emitted `TEST_PASS host.tests`.
- Kestrel package contract tests emitted:
  - `TEST_PASS kestrel.package.contract validates layout`
  - `TEST_PASS kestrel.package.contract rejects missing files`
  - `TEST_PASS kestrel.package.contract requires expected env`
  - `TEST_PASS kestrel.package.contract rejects unsupported profile`
  - `TEST_PASS kestrel.package.contract rejects unsupported runtime`

## Failures

- None in the integrated host run.

## Deferred work

- This is host package validation only.
- It does not prove hosted .NET 10 execution, PAL/CoreCLR integration, guest
  package reads, guest TCP, Kestrel binding, HTTP response evidence, TLS, or
  cloud-provider boot.
