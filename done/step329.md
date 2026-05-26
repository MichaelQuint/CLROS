# Step 329 - UEFI-Gone Simulation Covers Guarded Adapters

## Summary

Expanded `PlatformUefiGoneSimulation` so the host-tested UEFI-gone boundary verifies every current firmware-guarded adapter: console, memory map, configuration tables, timer, random, physical memory, and framebuffer.

## Why

The UEFI-gone simulation previously proved the first three adapters as an integrated marker path while newer guarded adapters had only focused denial tests. This slice keeps the real `ExitBootServices` path blocked, but closes the host-test gap by proving the integrated simulation fails if any current guarded adapter still allows firmware-backed calls after the simulated boundary.

## Files changed

- `src/OS.Kernel/Platform/PlatformUefiGoneSimulation.cs`
- `tests/OS.HostTests/PlatformUefiGoneSimulationTests.cs`
- `docs/boot.md`
- `docs/pal.md`
- `docs/completion-gates.md`
- `done/step329.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `.\scripts\run-qemu-smoke.ps1 -DryRun`
- `git diff --check`
- `rg --files src std apps | Where-Object { $_ -match '\.(c|cpp|h|asm|s)$' }`

## Evidence

- Focused host-test project build passed with 0 warnings and 0 errors.
- Full host harness passed and emitted:

```text
TEST_PASS platform.uefi gone simulation denies all guarded adapters
TEST_PASS platform.uefi gone simulation denies console memorymap and config adapters
TEST_PASS platform.uefi gone simulation reports console allowance
TEST_PASS platform.uefi gone simulation reports timer allowance
TEST_PASS platform.uefi gone simulation rejects already-exited firmware
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming guard and source firewall emitted `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- QEMU dry-run emitted `TEST_PASS qemu.runner.dryrun` with the default `SingleMinimalApi` workload profile and default required marker list.
- `git diff --check` exited 0 with line-ending warnings only.
- Forbidden first-party source scan produced no `.c`, `.cpp`, `.h`, `.asm`, or `.s` files under `src`, `std`, or `apps`.

## Failures

- Gitea issue updates remain blocked by missing write credentials in the current task context; API write attempts return `token is required`.
- No real `ExitBootServices`, post-firmware hardware access, live owned framebuffer, live owned RNG, or live QEMU boot proof is claimed for this slice.

## Deferred work

- Route future firmware-guarded adapters through `PlatformUefiGoneSimulation` as they are added.
- Wire owned post-firmware serial/framebuffer/physical-memory providers into the live boot path before enabling a real firmware shutdown.
- Add QEMU evidence for the existing UEFI-gone probe when the artifact/runtime path is ready for the expanded adapter surface.

