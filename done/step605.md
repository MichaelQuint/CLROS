# Step 605 - HTTP Request Parser Host Slice

## Summary

Added a host-tested HTTP/1.x request parser for caller-owned byte spans. The
parser records method, origin-form target, protocol version, host,
content-length, connection-close, and fixed-length body metadata while rejecting
malformed requests and default/zero result values.

## Why

Issue #6 needs server-side HTTP prerequisites before any future Kestrel socket
compatibility work can parse inbound `GET /healthz` bytes. This slice fills a
parser gap without adding sockets, TCP control blocks, live virtio-net I/O, PAL
exports, hosted .NET, or Kestrel behavior.

## Files changed

- `src/OS.Kernel/Net/HttpRequestParser.cs`
  - Added request parse statuses, method/result structs, request-line parsing,
    selected header parsing, fixed-length body metadata, and default-result
    rejection.
- `tests/OS.HostTests/HttpRequestParserTests.cs`
  - Added stable `net.http.request` host markers.
- `docs/networking.md`
  - Documented the request parser, focused markers, and non-claim boundary.
- `docs/completion-gates.md`
  - Recorded host-only request parser evidence without changing gate status.
- `done/step605.md`
  - Recorded this evidence boundary.

## Tests run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false
dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll | Select-String -Pattern 'TEST_FAIL|Unhandled|Exception|net\.http\.request|host\.tests'
.\scripts\check-naming.ps1
git diff --check
```

## Evidence

- The normal host-test build compiled dependencies but failed to overwrite the
  locked `OS.HostTests.exe`; the lock was held by an existing `OS.HostTests`
  process from other shared-worktree activity.
- Re-running the host-test build with `-p:UseAppHost=false` passed with 0
  warnings and 0 errors.
- After the lock cleared, the normal host-test build passed with 0 warnings and
  0 errors.
- The filtered full host-test run emitted the new markers and
  `TEST_PASS host.tests`:
  - `TEST_PASS net.http.request parses get healthz`
  - `TEST_PASS net.http.request preserves body metadata`
  - `TEST_PASS net.http.request rejects malformed request line`
  - `TEST_PASS net.http.request rejects invalid headers and body`
  - `TEST_PASS net.http.request default result is not success`
- `scripts/check-naming.ps1` emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0 with line-ending normalization warnings only.

## Failures

- The normal build failed only at the apphost copy step because
  `tests/OS.HostTests/bin/Debug/net10.0/OS.HostTests.exe` was locked by another
  process. The slice was verified with `UseAppHost=false` to avoid touching the
  locked executable, then the normal build passed after the lock cleared.

## Deferred work

- The parser does not implement chunked request bodies, absolute-form targets,
  header folding, full HTTP compliance, middleware semantics, sockets, TCP
  control blocks, retransmission, timers, route selection, PAL exports, live
  virtio-net I/O, hosted .NET, ASP.NET Minimal API, or Kestrel.

