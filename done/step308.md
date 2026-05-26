# Step 308 - Guarded Framebuffer Snapshot Boundary

## What changed

- Added `IPlatformFramebuffer` and `IPlatformFramebufferSource` as the firmware-guarded boundary for framebuffer metadata snapshots.
- Added `GuardedPlatformFramebuffer`, which denies metadata capture after UEFI-gone simulation or after `ExitBootServices`.
- Added POD-style framebuffer metadata/result contracts for base address, byte length, width, height, stride in pixels, pixel format, firmware-services mode, nested source status, required byte length, and firmware status.
- Added status-coded validation for zero base address, zero byte length, zero dimensions, invalid stride, unsupported pixel format, byte-length/stride mismatch, required-size arithmetic overflow, source unavailable, and firmware source failure.

## Why it changed

The OS needs a platform boundary for the UEFI GOP framebuffer snapshot before it can safely prepare an owned post-firmware display path. This step adds only the metadata contract and firmware-services guard. It intentionally does not call GOP, map framebuffer memory, draw text, allocate framebuffer storage, or claim output after `ExitBootServices`.

## Files changed

- `src/OS.Kernel/Platform/IPlatformFramebuffer.cs`
- `src/OS.Kernel/Platform/GuardedPlatformFramebuffer.cs`
- `tests/OS.HostTests/PlatformFramebufferGuardTests.cs`
- `docs/boot.md`
- `docs/drivers.md`
- `docs/completion-gates.md`
- `done/step308.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Ran multiple times in a shared worktree.
  - The #308 framebuffer markers passed in the full host run.
  - A later rerun failed before reaching `PlatformFramebufferGuardTests` because an unrelated nested `tools/OS.QemuRunner` compile in `CiSmokeScriptTests` could not resolve `RunnerOptions.MarkerPresets`.
- `.\scripts\check-naming.ps1`
  - Passed with `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check`
  - Passed. Git printed only LF-to-CRLF working-copy warnings.

## Evidence markers

```text
TEST_PASS platform.framebuffer snapshot allowed before UEFI gone
TEST_PASS platform.framebuffer snapshot denied in UEFI-gone simulation
TEST_PASS platform.framebuffer snapshot denied after ExitBootServices
TEST_PASS platform.framebuffer snapshot rejects zero base or length
TEST_PASS platform.framebuffer snapshot rejects zero dimensions
TEST_PASS platform.framebuffer snapshot rejects invalid stride
TEST_PASS platform.framebuffer snapshot rejects unsupported pixel format
TEST_PASS platform.framebuffer snapshot rejects size mismatch and overflow
TEST_PASS platform.framebuffer snapshot reports source failures
TEST_PASS platform.framebuffer result contains no managed references
```

## Failures

- One full host-test attempt did not finish green because of an unrelated QEMU HTTP probe temp-file sharing failure:
  - `TEST_FAIL qemu.runner.http_probe times out deterministically`
  - The failure output showed `Add-Content` could not access the fake serial log because it was being used by another process.
- Follow-up Gitea issue #314 tracks that QEMU HTTP probe serial-log sharing race.
- A later final-verification host-test attempt failed earlier in unrelated CI smoke coverage:
  - `TEST_FAIL ci.smoke.qemu.qcow2.dryrun exit_code=1`
  - Nested `tools/OS.QemuRunner` compilation reported missing `RunnerOptions.MarkerPresets` and a static-local-function capture error.
  - Gitea issue #313 was updated with the exact compiler output because the failure aligns with the marker-preset tooling surface.
- No #308 framebuffer marker failed.

## Deferred work

- Implement the real UEFI GOP adapter that populates this metadata boundary.
- Copy framebuffer metadata into the typed boot handoff and live `EfiMain` marker path.
- Map framebuffer memory after page ownership rules exist.
- Add framebuffer text output and panic-safe display rendering only after it is independent from firmware services.
- Add QEMU evidence for live framebuffer metadata once the no-stdlib EFI path is wired to GOP.
