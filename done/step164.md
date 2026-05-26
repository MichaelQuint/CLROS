# Step 164 - ImageBuilder Host Test Tool Invocation Boundary

## What Changed

- Updated `tests/OS.HostTests` to build `tools/OS.ImageBuilder` as an outer project dependency without referencing its output assembly.
- Updated `ImageBuilderTests.RunImageBuilder` to execute the already-built `OS.ImageBuilder.dll` with `dotnet <dll>` instead of nesting `dotnet run --project`.
- Updated ImageBuilder and completion-gate docs with the new test-tooling boundary and evidence.

## Why

Gitea issue #183 tracks a host-test scalability and determinism problem: ImageBuilder tests were invoking nested `dotnet run`, so a `--no-build` host-test run could still trigger repeated tool builds inside the test process. That made full integrated runs slower and increased the chance of file-lock contention when other workers were also building or testing.

The new path keeps compilation in the outer `dotnet build .\OS.slnx --nologo -m:1` step and keeps the host-test process focused on executing deterministic tool commands.

## Files Changed

- `tests/OS.HostTests/OS.HostTests.csproj`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `docs/image-builder.md`
- `docs/completion-gates.md`
- `done/step164.md`

## Implemented Behavior

- `tests/OS.HostTests/OS.HostTests.csproj` declares `tools/OS.ImageBuilder` as a project dependency with `ReferenceOutputAssembly="false"` and `PrivateAssets="all"`.
- `ImageBuilderTests` derives the current build configuration and target framework from `AppContext.BaseDirectory`.
- `ImageBuilderTests` resolves `tools/OS.ImageBuilder/bin/<configuration>/net10.0/OS.ImageBuilder.dll`.
- ImageBuilder test commands execute `dotnet <resolved OS.ImageBuilder.dll> ...`.
- A missing built tool emits a deterministic `image builder tool not found: <path>` failure through the existing `ToolResult` path.

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-integrated-177-183.log
.\scripts\check-naming.ps1
.\scripts\run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers
git diff --check
```

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` passed with 0 warnings and 0 errors.
- `artifacts/logs/host-tests-integrated-177-183.log` emitted `TEST_PASS host.tests`.
- The same integrated run emitted `TEST_PASS imagebuilder.package validation rejects missing app file`, proving the ImageBuilder command test path still exercises negative validation.
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `scripts/run-qemu-smoke.ps1 -DryRun -RequireNativeAppMarkers` emitted `TEST_PASS qemu.runner.dryrun`.
- `git diff --check` exited 0; it reported only line-ending normalization warnings.

## Failures

- No functional failures remained after the #179 virtio PCI parser bounds fix and the #183 host-test command-path change.
- `git diff --check` reported line-ending normalization warnings only; no whitespace errors were reported.

## Deferred Work

- Apply the same built-DLL execution pattern to other host tools that still use nested `dotnet run` from host tests.
- Add a lightweight host-test performance budget only after the test suite stabilizes further.

## Evidence Boundary

This step is host-test tooling evidence only. It does not prove ImageBuilder guest boot behavior, qcow2 boot, libvirt boot, cloud import, native app execution, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
