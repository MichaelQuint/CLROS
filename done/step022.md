# Step 022 - Kestrel Socket Compatibility Test Plan

## What Changed

- Added a layered Kestrel socket compatibility test plan to `docs/networking.md`.
- Separated packet encode/decode tests, raw TCP state-machine tests, PAL socket tests, and app-level Kestrel HTTP tests.
- Mapped Kestrel-visible behaviors to lower-level packet/TCP, PAL, runtime/app, and runner evidence requirements.
- Added required serial markers and expected failure diagnostics for packet, TCP, PAL socket, and Kestrel socket compatibility tests.
- Cross-linked the networking-owned checklist from `docs/kestrel.md` without changing the PAL ABI contracts.
- Explicitly kept TLS, HTTPS, ALPN, HTTP/2, HTTP/3, QUIC, and WebSockets out of scope until separate implementation and tests exist.

## Why It Changed

Gitea issue #36 requires the project to define Kestrel socket compatibility tests before implementing TCP. The plan gives independent agents a stable order for packet primitives, TCP state-machine work, PAL socket behavior, and final ASP.NET Minimal API/Kestrel smoke evidence without claiming Kestrel support early.

## Files Changed

- `docs/networking.md`
- `docs/kestrel.md`
- `done/step022.md`

## Tests Run

```powershell
rg -n "Kestrel Socket Compatibility Test Plan|Layer 1 - Packet Encode/Decode|Layer 2 - Raw TCP State Machine|Layer 3 - PAL Socket Tests|Layer 4 - App-Level Kestrel HTTP Tests|TEST_START kestrel.socket.compat|TLS, HTTPS" .\docs\networking.md .\docs\kestrel.md
git diff --check -- .\docs\networking.md .\docs\kestrel.md .\done\step022.md
```

## Logs And Evidence

- The documentation now contains the four ordered layers: packet encode/decode, raw TCP state machine, PAL socket tests, and app-level Kestrel HTTP tests.
- The checklist covers bind/listen/accept, concurrent clients, partial reads/writes, connection close, timeouts, cancellation, backpressure, large response body, split request headers, and malformed request handling.
- The required serial markers include `TEST_START kestrel.socket.compat`, `TEST_PASS kestrel.socket.compat`, and `TEST_FAIL kestrel.socket.compat reason=<code>`.
- Scoped `git diff --check` completed successfully for the owned files.

## Failures

No failures remain for this documentation slice.

## Deferred Work

- Implement ARP and TCP packet encode/decode tests.
- Implement a raw TCP state-machine test harness.
- Implement PAL socket mock tests against the documented `OSHost_*` socket contracts.
- Implement runner HTTP response verification.
- Implement in-guest hosted .NET 10 and Kestrel smoke tests after the network, PAL, scheduler, timer, package, and hosted runtime prerequisites exist.

## Evidence Boundary

This step is docs and test-plan work only. It does not implement TCP, sockets, PAL exports, runner HTTP verification, hosted .NET 10, Kestrel, TLS, or in-guest HTTP request/response behavior.
