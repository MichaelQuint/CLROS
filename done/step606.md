# Step 606 - Caller-Owned Boot Event Record Ring

## Summary

Added a host-tested caller-owned boot-event record ring for future panic-safe
boot diagnostics.

## Why

The existing `BootEventRingBuffer` stores managed strings and snapshots by
allocating a new string array. That is useful for current host-tested boot logs,
but it is not suitable for a future panic path. This step adds a compact record
ring that writes only caller-provided storage and keeps record/result layouts
free of managed references.

## Files changed

- `src/OS.Kernel/Diagnostics/BootEventRecordRingBuffer.cs`
- `tests/OS.HostTests/BootEventRecordRingBufferTests.cs`
- `docs/diagnostics.md`
- `docs/completion-gates.md`
- `tools/OS.QemuRunner/OS.QemuRunner.csproj`
- `done/step606.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`

Full host-test execution was captured to
`artifacts/logs/host-tests-step606.log` and emitted:

```text
TEST_PASS diagnostics.boot_event_record records caller owned events
TEST_PASS diagnostics.boot_event_record overwrites oldest deterministically
TEST_PASS diagnostics.boot_event_record reports snapshot capacity
TEST_PASS diagnostics.boot_event_record rejects missing storage
TEST_PASS diagnostics.boot_event_record default results are not success
TEST_PASS diagnostics.boot_event_record layouts contain no managed references
```

## Evidence

- `BootEventRecordRingBuffer` records compact `BootEventRecord` values into a
  caller-owned `Span<BootEventRecord>`.
- Snapshot order is oldest-to-newest when capacity is sufficient.
- Undersized snapshots copy the newest records that fit and return
  `DestinationTooSmall` instead of reporting success.
- Missing storage and default result/record values are not successful evidence.
- `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` tests verify the record
  and result layouts contain no managed references.
- The focused host-test build passed with 0 warnings and 0 errors.

## Failures

- A concurrent stale `OS.HostTests.exe` process locked the host-test apphost
  during the first build retry window; the process was inspected and
  terminated after it failed to clear.
- The next build exposed stale generated C# compile items under
  `tools/OS.QemuRunner/artifacts/obj/...`; `tools/OS.QemuRunner` now excludes
  `artifacts/**/*.cs` from compilation instead of deleting another agent's
  artifact directory.
- A concurrent worker host-test process briefly held the apphost while checking
  another slice; the later `host-tests-step606.log` run completed and emitted
  `TEST_PASS host.tests` with no matching `TEST_FAIL`, `Unhandled`, or
  `Exception` lines.

## Deferred work

- Wire the caller-owned record ring into `BootSequence` or the live panic path
  only after owned serial and descriptor-table installation paths are ready.
- Add QEMU fault-probe evidence for null dereference and invalid opcode after
  real IDT/GDT/TSS/IST setup exists.
- Keep the existing managed string `BootEventRingBuffer` for normal host-tested
  boot log mirroring until a no-allocation marker path is wired through
  boot-order phases.

