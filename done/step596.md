# Step 596 - Libvirt Serial Marker Preflight

## Summary

Added a host-side `OS.LibvirtRunner check-serial` mode that validates an
already-captured serial log against required and forbidden marker contracts
without connecting to libvirt or starting a domain. The check reuses the
`OS.QemuRunner` `SerialMarkerSummary` parser source so QEMU and libvirt smoke
paths interpret marker lines, `APP_EXIT` records, missing markers, and forbidden
markers consistently.

## Why

Libvirt real lifecycle support is still pending, but issue #7 needs a concrete
preflight boundary before live domain execution. This slice makes the planned
serial-marker polling semantics executable and host-tested while preserving the
current rule that libvirt smoke evidence must not claim boot proof.

## Files changed

- `tools/OS.LibvirtRunner/OS.LibvirtRunner.csproj`
- `tools/OS.LibvirtRunner/Program.cs`
- `scripts/run-libvirt-smoke.ps1`
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
- `docs/libvirt.md`
- `docs/cloud-import.md`
- `done/step596.md`

## Tests run

- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo`
- `dotnet run --project .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj -- check-serial --serial-log <temp>\serial.log --summary-log <temp>\summary.log --require-marker OS_SMOKE_PASS --require-marker BOOT_HANDOFF_READY --forbid-marker OS_SMOKE_FAIL`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- `OS.LibvirtRunner` built with 0 warnings and 0 errors.
- The focused `check-serial` run exited 0 and emitted `TEST_PASS
  libvirt.serial.summary`.
- The focused `check-serial` run emitted `LIBVIRT_SERIAL_SUMMARY_STATUS pass`,
  `OBSERVED_MARKER OS_SMOKE_PASS`, `OBSERVED_MARKER BOOT_HANDOFF_READY`,
  `APP_EXIT_RESULT code=0 status=OSHOST_STATUS_SUCCESS`, `MISSING_MARKERS
  none`, `FORBIDDEN_MARKER_HIT none`, and `FAIL_REASON none`.
- The host-test project built with 0 warnings and 0 errors.
- The host-test run emitted `TEST_PASS libvirt check-serial passes and writes
  summary`, `TEST_PASS libvirt check-serial rejects missing marker`,
  `TEST_PASS libvirt check-serial rejects forbidden marker`, and `TEST_PASS
  host.tests`.
- The new CLI mode emits `TEST_START libvirt.serial.summary`,
  `LIBVIRT_SERIAL_SUMMARY_STATUS pass|fail`, `OBSERVED_MARKER`,
  `APP_EXIT_RESULT`, `MISSING_MARKERS`, `FORBIDDEN_MARKER_HIT`,
  `FAIL_REASON`, and `TEST_PASS`/`TEST_FAIL` markers.
- Host tests were added for passing serial logs, missing required markers,
  forbidden marker hits, summary artifact writing, and the script-level
  `-CheckSerialOnly` contract.

## Failures

- A first `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  attempt was blocked by stale/concurrent `OS.HostTests.exe` processes holding
  output DLL locks. The processes were stopped explicitly by PID before
  continuing. This was a host-output lock, not a compile error in the libvirt
  runner.

## Deferred work

- Run the full solution build after the parallel worker test process contention
  is clear.
- Wire real libvirt lifecycle execution to call the now-host-tested serial
  marker summary contract after transient domain startup.
- Add timeout, destroy, undefine, and cleanup evidence once live libvirt
  execution is implemented on a host with libvirt.
