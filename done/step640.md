# Step 640 - TCP established stream acknowledgement hardening

## Summary

Added a host-tested established TCP stream parser-state guard that rejects
payload-bearing ACK/PSH segments whose acknowledgement number does not match the
stream's current local next sequence number.

## Why

Issue #6 needs incremental networking hardening before live packet I/O exists.
`TcpEstablishedStream` already checked ports, flags, receive sequence order,
duplicate segments, and receive-buffer capacity, but it did not reject payload
segments that acknowledged the wrong local sequence. The new guard keeps the
caller-owned stream state unchanged on that malformed input.

## Files changed

- `src/OS.Kernel/Net/TcpEstablishedStream.cs`
  - Added `InvalidAcknowledgementNumber`.
  - Rejects mismatched inbound acknowledgement numbers before payload bytes are
    copied or remote sequence metadata is advanced.
- `tests/OS.HostTests/TcpEstablishedStreamTests.cs`
  - Added `TEST_PASS net.tcp stream rejects wrong acknowledgement`.
  - Verifies wrong-ack payload rejection preserves unread bytes and local/remote
    sequence metadata, then verifies a correct segment still succeeds.
- `docs/networking.md`
  - Documented the new established-stream acknowledgement guard and marker.
- `docs/completion-gates.md`
  - Updated Gate 8 host-tested networking evidence without changing the gate
    status.
- `done/step640.md`
  - Records this step and evidence boundary.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`

## Evidence

- Host-test project build passed with 0 warnings and 0 errors.
- Full host-test harness passed with `TEST_PASS host.tests`.
- The run emitted:
  - `TEST_PASS net.tcp stream receives in-order payload`
  - `TEST_PASS net.tcp stream rejects out-of-order payload`
  - `TEST_PASS net.tcp stream rejects wrong acknowledgement`
  - `TEST_PASS net.tcp stream receive default result is not success`

## Failures

None observed in the focused build or full host-test harness for this slice.

## Deferred work

- This is host-tested parser-state evidence only.
- It does not implement retransmission, receive windows, timers, socket
  delivery, live virtio-net packet I/O, FetchApp, hosted .NET 10, ASP.NET
  Minimal API, or Kestrel behavior.
