# Step 147 - Libvirt Smoke Marker Diagnostics

## What Changed

- Added repeatable `--require-marker <marker>` and `--forbid-marker <marker>` options to `OS.LibvirtRunner`.
- `--diagnostics` now emits:
  - `LIBVIRT_DIAG required.markers=...`
  - `LIBVIRT_DIAG forbidden.markers=...`
- The default required marker remains `OS_SMOKE_PASS` when no marker is supplied.
- Added host tests for default and custom marker diagnostics.
- Updated libvirt and completion-gate docs.

## Why It Changed

Gitea issue #165 needs libvirt dry-run/XML generation to carry the same serial marker contract as QEMU before real libvirt domain execution and serial polling are implemented.

## Files Changed

- `tools/OS.LibvirtRunner/Program.cs`
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
- `docs/libvirt.md`
- `docs/completion-gates.md`
- `done/step147.md`

## Tests Run

```powershell
dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo -m:1
dotnet run --project .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --no-restore -- generate-xml --image <temp>\os.raw --serial-log <temp>\serial.log --require-marker OS_SMOKE_PASS --require-marker APP_INVOCATION_READY --forbid-marker OS_SMOKE_FAIL --diagnostics --validate-paths
git diff --check -- .\tools\OS.LibvirtRunner\Program.cs .\tests\OS.HostTests\LibvirtRunnerTests.cs .\docs\libvirt.md .\docs\completion-gates.md .\done\step147.md
```

`dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` was attempted but blocked by an unrelated active `OS.HostTests.exe` process locking the shared test output.

## Logs And Evidence

- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- Direct LibvirtRunner diagnostics returned exit code 0 and emitted `LIBVIRT_DIAG required.markers=OS_SMOKE_PASS;APP_INVOCATION_READY`.
- The same run emitted `LIBVIRT_DIAG forbidden.markers=OS_SMOKE_FAIL`.
- The same run preserved domain XML on stdout.
- `git diff --check` on the scoped files exited 0 with existing line-ending warnings only.
- Expected host marker once the full suite is free to run: `TEST_PASS libvirt diagnostics emit marker requirements`.

## Failures

- The shared host-test project build was blocked by an unrelated active host-test process locking `tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe`.

## Deferred Work

- No libvirt connection was added.
- No transient domain creation was added.
- No serial marker polling was added.
- No timeout, shutdown, or clean destroy flow was added.

## Evidence Boundary

This step records host-side marker metadata only. It does not prove libvirt is installed, a domain can boot, serial logs can be captured, or `OS_SMOKE_PASS` can be observed from a guest.
