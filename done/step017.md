# Step 017 - Manifest Runtime Kind and Reserved Kestrel Contract

## What Changed

- Extended `OS.ManifestTool` manifest v0 validation with an optional `runtime` object.
- Preserved existing manifests with no `runtime` as the v0 native-compatible default.
- Accepted explicit `runtime.kind: "native-aot"` for native-tier manifests.
- Reserved and validated `runtime.kind: "dotnet10"` with a safe relative `runtime.assembly` path and `runtime.kestrel.listen` endpoint array.
- Rejected unknown runtime kinds, unsafe hosted assembly paths, malformed hosted shapes, non-HTTP Kestrel schemes, invalid ports, duplicate listen endpoints, and non-object listen entries.
- Updated manifest documentation to state that `dotnet10`/Kestrel validation is host-side contract validation only.

## Why It Changed

The CloudReady goal requires .NET 10 applications, with ASP.NET Minimal API/Kestrel as the primary hosted workload target. This step reserves the manifest contract early while avoiding any claim that the OS can load hosted .NET 10 or run Kestrel today.

## Files Changed

- `tools/OS.ManifestTool/Program.cs`
- `tests/OS.HostTests/ManifestToolTests.cs`
- `docs/manifest.md`
- `done/step017.md`

## Tests Run

```powershell
dotnet build .\tools\OS.ManifestTool\OS.ManifestTool.csproj --nologo
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check -- tools/OS.ManifestTool/Program.cs tests/OS.HostTests/ManifestToolTests.cs docs/manifest.md done/step017.md
```

## Logs and Evidence

- Manifest tool build passed with 0 warnings and 0 errors.
- Full solution build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- New manifest markers passed:
  - `TEST_PASS manifesttool validates native-aot runtime`
  - `TEST_PASS manifesttool validates reserved dotnet10 kestrel contract`
  - `TEST_PASS manifesttool rejects unknown runtime kind`
  - `TEST_PASS manifesttool rejects unsafe dotnet10 assembly paths`
  - `TEST_PASS manifesttool rejects malformed dotnet10 runtime shapes`
  - `TEST_PASS manifesttool rejects invalid kestrel listen endpoints`
- Scoped `git diff --check` exited 0. It printed line-ending normalization warnings only.

## Failures

No failures remain for this slice.

An initial host-test project build attempt was blocked by an already-running `OS.HostTests.exe` process. The lock cleared, and the subsequent full solution build and host-test run passed.

## Deferred Work

- Kernel-side manifest parser and loader.
- Image-builder manifest generation.
- Package filesystem integration.
- Hosted .NET 10 runtime and `OSHost_*` PAL integration.
- Socket, timer, scheduler, async, config, and logging support needed by Kestrel.
- In-guest ASP.NET Minimal API/Kestrel request-response smoke evidence.

## Evidence Boundary

This step proves host-side schema validation for native and reserved hosted runtime manifests. It does not prove OS package loading, hosted .NET 10 execution, Kestrel binding, TCP networking, TLS, or any in-guest HTTP request/response behavior.
