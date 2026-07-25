# Step 764 - HTTP TCP GET formatter shared ASCII helpers

## Summary
Migrated `HttpTcpClientGetPlanDiagnosticFormatter` to the shared
`OS.Std.Text.AsciiDecimalFormatter` and `OS.Std.Text.AsciiTextBuilder`
helpers. The `HTTP_TCP_GET_PLAN` marker shape remains stable while removing the
formatter-local decimal byte-count helpers, direct byte-copy append helper, and
direct decimal digit write loops.

## Why
The HTTP TCP GET planner is Phase F networking evidence for caller-owned TCP
and HTTP diagnostic metadata. Its formatter should use the shared no-runtime
ASCII helper surface so integer formatting behavior stays centralized and
source-guarded before later networking diagnostics reuse the same pattern.

## Files changed
- `src/OS.Kernel/Net/HttpTcpClientGetPlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/HttpTcpClientGetPlannerTests.cs`
- `docs/networking.md`
- `done/step764.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue721-http-tcp-get-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue721-http-tcp-get-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`

## Evidence
- Build log: `artifacts/logs/issue721-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue721-hosttests-20260525.log`
  - `TEST_PASS net.http.tcp.get completes request response`
  - `TEST_PASS net.http.tcp.get writes request checksum`
  - `TEST_PASS net.http.tcp.get rejects bad response checksum`
  - `TEST_PASS net.http.tcp.get formatter formats success marker`
  - `TEST_PASS net.http.tcp.get formatter names duplicate content length`
  - `TEST_PASS net.http.tcp.get formatter reports short destination`
  - `TEST_PASS net.http.tcp.get formatter default result is not success`
  - `TEST_PASS net.http.tcp.get formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Diff-check log: `artifacts/logs/issue721-git-diff-check-20260525.log`
  - `git diff --check` exited 0.
  - The log contains existing Git LF-to-CRLF working-copy warnings from the
    dirty tree and no whitespace-error failure.

## Failures
- None in the focused build, host-test run, or diff check.
- No QEMU smoke was run because this changes only host-tested diagnostic
  formatting over already-produced HTTP TCP GET planner metadata.

## Deferred work
- Wire HTTP TCP GET planning into future live TCP/socket I/O only after
  connection lifecycle, retransmission, timers, stream ownership, and live
  virtio-net packet I/O have evidence.
- Continue migrating remaining networking diagnostic formatters that still
  carry local integer formatting loops.

## Evidence boundary
This step proves caller-owned byte-buffer formatting for already-produced HTTP
TCP GET planner results and source-guards use of the shared ASCII text builder
and decimal formatter. It does not perform live HTTP GET, open sockets, export
PAL socket calls, publish virtio-net descriptors, retransmit packets, run
timers, execute FetchApp, start hosted .NET, start Kestrel, or provide
cloud-provider evidence.

