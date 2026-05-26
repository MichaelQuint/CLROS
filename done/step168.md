# Step 168 - Built-DLL Host Tool Test Boundary

## What Changed

- Added `HostToolProcess` as a shared host-test helper for repository-root lookup and built host-tool DLL resolution.
- Updated ManifestTool, QEMU runner, and libvirt runner host tests to run `dotnet <built-tool.dll>` instead of nested `dotnet run --project` calls for those tool CLIs.
- Added non-runtime host-test project dependencies for ManifestTool and LibvirtRunner.
- Updated QEMU, libvirt, manifest, and completion-gate docs.

## Why

Gitea issue #184 followed the ImageBuilder test cleanup. Host tests should not re-enter build behavior while running with `--no-build`; the outer solution build should produce the host tools, and tests should execute those built artifacts as processes.

## Files Changed

- `tests/OS.HostTests/HostToolProcess.cs`
- `tests/OS.HostTests/ManifestToolTests.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
- `tests/OS.HostTests/OS.HostTests.csproj`
- `docs/qemu.md`
- `docs/libvirt.md`
- `docs/manifest.md`
- `docs/completion-gates.md`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-issue184.log` emitted `TEST_PASS host.tests`.
- The host-test log emitted `TEST_PASS manifesttool validates schema v0`, `TEST_PASS qemu dry-run preserves command construction`, and `TEST_PASS libvirt generate-xml emits deterministic network-none XML`.
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `scripts/run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0 with CRLF normalization warnings only.

## Evidence Boundary

This is host-test tooling evidence only. It does not prove QEMU boot, libvirt boot, cloud import, guest storage, guest networking, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
