# Step 621 - HTTP Static Response Transaction Host Slice

## Summary

Added `HttpStaticResponseTransaction` as a host-tested server-side HTTP
primitive over caller-owned buffers. It composes the existing HTTP request
parser and response writer for a single static route, returning deterministic
`200 OK`, `404 Not Found`, and `405 Method Not Allowed` responses without
owning sockets or live network state.

## Why

Issue #6 needs HTTP server and Kestrel prerequisites, but the project still
lacks live virtio-net I/O, sockets, scheduler integration, hosted .NET, and
Kestrel. This slice advances the server-side HTTP contract that a future tiny
HTTP server or Kestrel-facing path can reuse while keeping the evidence boundary
host-tested and caller-buffer-only.

## Files changed

- `src/OS.Kernel/Net/HttpStaticResponseTransaction.cs`
- `tests/OS.HostTests/HttpStaticResponseTransactionTests.cs`
- `docs/networking.md`
- `docs/completion-gates.md`
- `done/step621.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe` with output
  saved to `artifacts\logs\host-tests-step621-retry.log`.

## Evidence

- Build passed with 0 warnings and 0 errors.
- The full host suite retry emitted `TEST_PASS host.tests`.
- The Step621 HTTP transaction markers appeared in the retry log:
  - `TEST_PASS net.http.static serves matched get`
  - `TEST_PASS net.http.static writes not found response`
  - `TEST_PASS net.http.static writes method not allowed response`
  - `TEST_PASS net.http.static rejects malformed request and route`
  - `TEST_PASS net.http.static reports short response buffer`
  - `TEST_PASS net.http.static default result is not success`

## Failures

- The first full host run for this slice saw stale QEMU runner dry-run
  failures while parallel workers were editing and rebuilding QEMU-related host
  tooling. Rebuilding the host tests and rerunning the full suite cleared those
  failures and produced the passing retry log listed above.

## Deferred work

- Wire this transaction to a live listener only after socket export, scheduler
  waits, TCP connection ownership, and virtio-net packet I/O exist.
- Add request routing, headers beyond the static route baseline, and streaming
  response support only when a real server surface needs them.
- Do not claim ASP.NET Minimal API or Kestrel behavior from this primitive.

## Evidence boundary

This is host-tested HTTP request/response composition over caller-owned byte
buffers only. It does not open sockets, accept TCP connections, poll a NIC,
perform live HTTP server I/O, run hosted .NET 10, run ASP.NET Minimal API, or
prove Kestrel readiness.
