# Step 759 - TCP Close IPv4 Formatter Shared ASCII Helpers

## Summary

Migrated the TCP active-close IPv4 frame-plan diagnostic formatter from
formatter-local decimal counting/writing and byte-copy loops to the shared
`OS.Std.Text` no-runtime ASCII helpers.

## Why

`TCP_CLOSE_IPV4_FRAME` is a kernel-facing diagnostic marker. Its byte-count
accounting and caller-owned write path should share the same no-runtime ASCII
formatter surface as other diagnostics so local decimal loops do not proliferate
in networking code.

## Files changed

- `src/OS.Kernel/Net/TcpConnectionCloseIpv4FramePlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/TcpConnectionCloseIpv4FramePlannerTests.cs`
- `docs/networking.md`
- `done/step759.md`
- `artifacts/logs/issue717-host-build-20260525.log`
- `artifacts/logs/issue717-hosttests-20260525.log`
- `artifacts/logs/issue717-git-diff-check-20260525.log`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue717-tcp-close-shared-text\bin\Debug\net10.0`
- `dotnet .\artifacts\issue717-tcp-close-shared-text\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence

- `artifacts/logs/issue717-host-build-20260525.log` shows `Build succeeded.`
  with `0 Warning(s)` and `0 Error(s)`.
- `artifacts/logs/issue717-hosttests-20260525.log` shows:
  - `TEST_PASS net.tcp.close.ipv4 writes active fin checksum`
  - `TEST_PASS net.tcp.close.ipv4 rejects identity and short output`
  - `TEST_PASS net.tcp.close.ipv4 default result is not success`
  - `TEST_PASS net.tcp.close.ipv4.formatter formats active close marker`
  - `TEST_PASS net.tcp.close.ipv4.formatter reports short destination`
  - `TEST_PASS net.tcp.close.ipv4.formatter rejects default result`
  - `TEST_PASS net.tcp.close.ipv4.formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- `artifacts/logs/issue717-git-diff-check-20260525.log` was captured from
  `git diff --check`, which exited with code `0`.

## Failures

- No build or host-test failures.
- `git diff --check` emitted existing LF-to-CRLF working-copy warnings, but no
  whitespace errors and exited with code `0`.

## Deferred work

- This is host-tested diagnostic-formatting evidence only. It does not validate
  inbound close ACK/FIN/RST checksums, wrap passive close frames, publish
  virtio-net descriptors, own TCP control blocks, export sockets/PAL handles,
  retransmit, run timers, or prove hosted .NET, ASP.NET Minimal API, Kestrel,
  or live network behavior.

