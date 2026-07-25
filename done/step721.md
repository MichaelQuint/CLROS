# Step 721 - QEMU NativeAOT Heap Preset And Shared Helper Migrations

## Summary

Closed four small implementation slices:

- Added the `nativeaot-heap` QEMU marker preset and CI dry-run lane for the future `NATIVEAOT_HEAP_BOOTSTRAP_READY` guest marker.
- Migrated FAT32 short-name equality to `OS.Std.MemoryExtensions.SequenceEqual`.
- Migrated HTTP request and response protocol-prefix checks to `OS.Std.MemoryExtensions.StartsWith`.
- Added host/source-guard coverage for each slice.

## Why

The CloudReady backlog needs small, evidence-backed progress that moves runtime, storage, and networking code toward the shared no-runtime utility surface while keeping smoke tooling ready for future boot images. The new marker preset is contract tooling only; it does not claim a real NativeAOT heap bootstrap happened in QEMU.

## Files changed

- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `scripts/run-qemu-smoke.ps1`
- `scripts/run-ci-smoke.ps1`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `src/OS.Kernel/Storage/Fat32Parser.cs`
- `tests/OS.HostTests/StorageFat32ParserTests.cs`
- `src/OS.Kernel/Net/HttpRequestParser.cs`
- `tests/OS.HostTests/HttpRequestParserTests.cs`
- `src/OS.Kernel/Net/HttpResponseParser.cs`
- `tests/OS.HostTests/HttpResponseParserTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset nativeaot-heap`
- `.\scripts\run-ci-smoke.ps1 -DryRun -LogRoot artifacts\logs\issue667-ci-dryrun`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- `artifacts\logs\issues667-670-host-build-20260525.log` ended with `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, and `EXIT_CODE 0`.
- `artifacts\logs\issue667-qemu-nativeaot-heap-dryrun-20260525.log` emitted `QEMU_MARKER_PRESETS nativeaot-heap`, `MARKER_PRESETS nativeaot-heap`, `NATIVEAOT_HEAP_BOOTSTRAP_READY`, `TEST_PASS qemu.runner.dryrun`, and `EXIT_CODE 0`.
- `artifacts\logs\issue667-ci-dryrun-20260525.log` emitted `TEST_PASS ci.smoke.dryrun includes nativeaot-heap marker preset`, `TEST_PASS ci.smoke.dryrun`, and `EXIT_CODE 0`.
- `artifacts\logs\issues667-670-hosttests-20260525.log` emitted `TEST_PASS host.tests`, `TEST_PASS qemu.smoke.preset nativeaot heap adds heap bootstrap marker`, `TEST_PASS qemu.smoke.preset nativeaot heap aliases normalize`, `TEST_PASS qemu.smoke.preset script dry-run includes nativeaot heap preset`, `TEST_PASS ci.smoke.dryrun includes nativeaot-heap marker preset`, `TEST_PASS storage fat32 uses shared memory short-name helper`, `TEST_PASS net.http.request uses shared memory protocol prefix helper`, and `TEST_PASS net.http.response uses shared memory protocol prefix helper`, with `EXIT_CODE 0`.

## Failures

- An early integrated host build attempt wrote `artifacts\logs\issue667-host-build-after-ci-patch-20260525.log` and failed because concurrent `OS.HostTests.exe` processes held the host-test apphost. After the worker processes completed, the integrated build passed.

## Deferred work

- The `nativeaot-heap` preset remains marker-contract tooling only. Real evidence still requires a guest image that emits `NATIVEAOT_HEAP_BOOTSTRAP_READY` from the actual boot path.
- The HTTP and FAT32 changes are host-tested shared-helper migrations only. They do not add live storage or networking device evidence.
- Continue decomposing the open CloudReady epics into small implementation slices and close issues only with command/log evidence.

## Evidence boundary

This step provides host build, host-test, QEMU dry-run, and CI dry-run evidence only. It is not real QEMU boot proof for NativeAOT heap bootstrap, live FAT32-on-virtio, live HTTP parsing over virtio-net, hosted .NET, Minimal API, Kestrel, cloud readiness, or production readiness.
