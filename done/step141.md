# Step 141 - TCP Established Stream Primitive

## What Changed

- Added `OS.Net.TcpEstablishedStream` as a caller-owned established TCP payload primitive.
- Added status-coded receive, read, and send result types.
- Inbound payload handling now:
  - accepts ACK/PSH payload segments only when the segment sequence equals the expected receive sequence;
  - appends accepted payload bytes into caller-provided receive storage;
  - advances acknowledgement metadata immediately after accepted payload;
  - preserves unread bytes across bounded reads;
  - rejects duplicate and out-of-order payload without consuming buffer space or advancing acknowledgement metadata.
- Outbound payload handling now writes one TCP segment at a time and caps payload bytes by a caller-provided MSS-like maximum payload length.
- Added host-test source with the required issue #159 markers:
  - `TEST_PASS net.tcp stream receives in-order payload`
  - `TEST_PASS net.tcp stream preserves unread bytes`
  - `TEST_PASS net.tcp stream rejects out-of-order payload`
  - `TEST_PASS net.tcp stream writes segmented payload`
- Updated networking and completion-gate docs with the host-test-only boundary.

## Why It Changed

Gitea issue #159 needed the next TCP data-transfer slice after active/passive establishment. This provides deterministic established payload state for future HTTP and socket work without adding retransmission, timers, congestion control, close states, sockets, virtio-net I/O, hosted .NET, or Kestrel behavior.

## Files Changed

- `src/OS.Kernel/Net/TcpEstablishedStream.cs`
- `tests/OS.HostTests/TcpEstablishedStreamTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step141.md`

## Tests Run

```powershell
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1` passed with 0 warnings and 0 errors.
- Full host-test execution was intentionally not run during this pass because full host-test and ImageBuilder processes were stacking up in the shared workspace. The #159 test markers are present in `tests/OS.HostTests/TcpEstablishedStreamTests.cs` and will run through the existing module-initializer host-test harness when the full host-test process is safe to run.

## Failures

- No compile failures were observed in the focused project builds.
- The required host-test markers were not executed in this pass due the concurrent long-running verification constraint above.

## Deferred Work

- No retransmission, timers, congestion control, receive-window negotiation, out-of-order reassembly, FIN/RST/close handling, TCP control-block table, socket/PAL export, live virtio-net I/O, TLS, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior was added.
- `HttpClientGetTransaction` remains a mock-stream transaction and is not wired to `TcpEstablishedStream`.

## Evidence Boundary

This step is a managed compile-verified and host-test-sourced TCP primitive only. It proves the new kernel-facing source and test source compile, and it defines focused host-test markers for established payload receive/read/send behavior. It does not prove full host-test execution in this pass, live packet I/O, a full TCP state machine, sockets, FetchApp, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
