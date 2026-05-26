# Step 735 - FetchApp HTTP TCP Context Planner

## Summary

Added a host-tested FetchApp HTTP TCP context planner. `FetchAppHttpTcpContextPlan` composes an already-derived FetchApp request endpoint, an already-successful DNS resolver transaction result, a cached IPv4 neighbor-route result, and caller-provided local TCP identity into `HttpTcpClientGetPlannerInfo` for the existing caller-buffer HTTP GET TCP planner.

## Why

FetchApp previously had manifest endpoint and completion predicates, but no explicit bridge from endpoint/DNS/ARP metadata into the HTTP TCP GET planner. This slice records that handoff boundary without claiming live DNS, ARP, TCP, HTTP, sockets, virtio-net I/O, or FetchApp execution.

## Files changed

- `src/OS.Kernel/Net/FetchAppHttpTcpContextPlan.cs`
- `tests/OS.HostTests/FetchAppHttpTcpContextPlanTests.cs`
- `docs/networking.md`
- `docs/app-model.md`
- `done/step735.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts\logs\issue701-host-build-20260525.log`: host test project build succeeded with 0 warnings and 0 errors.
- `artifacts\logs\issue701-hosttests-20260525.log`: host tests passed with:
  - `TEST_PASS fetch.http tcp context composes planner info`
  - `TEST_PASS fetch.http tcp context rejects endpoint failure`
  - `TEST_PASS fetch.http tcp context rejects dns failure`
  - `TEST_PASS fetch.http tcp context rejects dns host mismatch`
  - `TEST_PASS fetch.http tcp context rejects uncached route`
  - `TEST_PASS fetch.http tcp context rejects invalid local identity`
  - `TEST_PASS fetch.http tcp context default result is not success`
  - `TEST_PASS host.tests`
- `artifacts\logs\issue701-git-diff-check-postdone-20260525.log`: `git diff --check` exited 0; output contains only existing LF-to-CRLF working-copy warnings.

## Failures

No test failure is known for this slice.

## Deferred work

- Feed this context into a future FetchApp runner once DNS, ARP, TCP, HTTP, and socket ownership are represented by live guest paths.
- Preserve caller-owned host/path buffers for the future request write step; this slice only validates host/DNS fingerprint parity and carries endpoint byte counts.
- Add live virtio-net packet I/O, retransmission timers, TCP control-block ownership, and socket/PAL integration before claiming FetchApp network execution.

## Evidence boundary

This is host-tested metadata composition only. It does not resolve DNS, send ARP, publish NIC descriptors, open sockets, perform TCP handshakes, transfer HTTP bytes, execute FetchApp, run hosted .NET, prove Kestrel, boot QEMU, or prove cloud readiness.
