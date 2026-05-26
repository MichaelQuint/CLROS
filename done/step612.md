# Step 612 - Boot Event Record Formatter

## Summary

Added a host-tested caller-owned ASCII formatter for `BootEventRecord`
snapshots. The formatter writes deterministic boot-event record lines through
`PanicTextBuffer` or caller-owned byte storage for future panic dumps.

## Why

Issue #2 needs diagnostics/boot panic-dump prerequisites that do not depend on
live IDT, QEMU fault probes, or the existing `BootEventRecordRingBuffer` storage
slice. This step keeps the ring storage separate and adds the next boundary:
turning compact records into panic-safe ASCII text without allocation in the
formatter path.

## Files changed

- `src/OS.Kernel/Diagnostics/BootEventRecordFormatter.cs`
  - Added formatter status/result values and deterministic record/snapshot
    formatting into `PanicTextBuffer` or caller-owned byte spans.
- `tests/OS.HostTests/BootEventRecordFormatterTests.cs`
  - Added focused host tests with stable `diagnostics.boot_event_formatter`
    markers.
- `docs/diagnostics.md`
  - Documented the formatter contract, tests, and host-only boundary.
- `docs/completion-gates.md`
  - Recorded formatter evidence without changing fault-diagnostics gate status.
- `done/step612.md`
  - Recorded this step.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|diagnostics\.boot_event_formatter|host\.tests'
```

## Evidence

- Focused host-test build completed with 0 errors.
- Filtered host-test output emitted:
  - `TEST_PASS diagnostics.boot_event_formatter formats snapshot records`
  - `TEST_PASS diagnostics.boot_event_formatter reports destination capacity`
  - `TEST_PASS diagnostics.boot_event_formatter rejects invalid records`
  - `TEST_PASS diagnostics.boot_event_formatter writes through panic buffer`
  - `TEST_PASS diagnostics.boot_event_formatter default result is not success`
  - `TEST_PASS diagnostics.boot_event_formatter result contains no managed references`

## Failures

- The focused host-test build completed after one MSBuild apphost copy retry
  warning caused by a concurrent process holding
  `tests/OS.HostTests/obj/Debug/net10.0/apphost.exe`.
- The filtered full host harness later exited with existing unrelated
  `ci.smoke` and `libvirt.smoke.generate_xml` failures, including
  `TEST_FAIL ci.smoke failures=4` and `Unhandled exception.
  System.InvalidOperationException: CI smoke script tests failed: 1`. The new
  formatter markers passed before that unrelated failure.

## Deferred work

- The formatter is not wired into `BootSequence`, the live EFI path, a real
  panic dump, or an owned serial panic writer.
- No live IDT/GDT/TSS/IST setup, CPU register capture, `ExitBootServices`,
  QEMU fault probe, post-firmware serial output, or production panic path is
  proven by this slice.

