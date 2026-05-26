# Step 209 - HTTP GET over TCP IPv4 frame planner

## What changed

- Added `OS.Net.HttpTcpClientGetPlanner`, a caller-owned-buffer planner that composes the active TCP IPv4 frame planner, established TCP payload stream, and HTTP GET request/response transaction.
- Added host tests for the SYN/SYN-ACK/ACK sequence, one HTTP GET request frame, one HTTP response frame, TCP frame failures, request failures, response frame failures, response parse failures, and small response destination handling.
- Updated networking documentation to describe the new boundary and its limitations.

## Why

Issue #228 required a deterministic HTTP GET planning layer after the TCP IPv4 active-connect frame wrapper landed. This gives the next networking agents a narrow bridge between TCP handshaking, established payload bytes, and HTTP response parsing without claiming sockets, live NIC I/O, FetchApp, hosted .NET, Minimal API, or Kestrel support.

## Files changed

- `src/OS.Kernel/Net/HttpTcpClientGetPlanner.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `done/step209.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\issue228-hosttests.log`
- `.\scripts\check-naming.ps1`
- `git diff --check -- src/OS.Kernel/Net/HttpTcpClientGetPlanner.cs tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs docs/networking.md done/step209.md`

## Logs/evidence

- Build passed with `0 Warning(s), 0 Error(s)`.
- Host tests passed with `HOST_TEST_EXIT 0` and `TEST_PASS host.tests`.
- Naming/source guard passed with `TEST_PASS naming.guard` and `TEST_PASS source.firewall`.
- `git diff --check` exited `0`; PowerShell reported only the existing LF/CRLF normalization warning for `docs/networking.md`.
- New host markers:
  - `TEST_PASS net.http.tcp.get completes request response`
  - `TEST_PASS net.http.tcp.get preserves tcp handshake failures`
  - `TEST_PASS net.http.tcp.get rejects request failures`
  - `TEST_PASS net.http.tcp.get rejects response frame failures`
  - `TEST_PASS net.http.tcp.get preserves http response failures`
  - `TEST_PASS net.http.tcp.get rejects response buffer too small`

## Failures

- None observed for this slice.

## Deferred work

- No sockets, live virtio-net RX/TX, retransmission, timers, TLS, redirects, chunked transfer, FetchApp, hosted .NET 10, ASP.NET Minimal API, or Kestrel behavior is claimed.
- HTTP request segmentation is intentionally deferred; the planner writes one complete GET request into one caller-owned TCP payload frame.
- TCP pseudo-header checksum generation and validation remain deferred.

## Evidence boundary

This step proves only a host-testable planning boundary once tests pass. It does not prove live packet I/O, a kernel TCP control block, PAL socket behavior, hosted runtime behavior, Minimal API, FetchApp, or Kestrel support.
