# Step 798 - TCP IPv4 frame planner buffer operations

## Summary

Migrated the TCP-over-IPv4 frame planner segment-copy sites for Gitea issue
#755 from direct span `CopyTo` calls to the shared
`OS.Std.Memory.BufferOperations.MemoryCopy` helper.

## Why

Phase F/G networking is moving packet and planner byte-copy paths onto the
shared no-runtime memory helper surface. This keeps TCP frame planner copies on
the same status-coded helper path as the newer packet/header copy migrations
without expanding the networking claim beyond host-tested caller-owned buffers.

## Files changed

- `src/OS.Kernel/Net/TcpIpv4FramePlanner.cs`
  - Uses `OS.Std.Memory`.
  - Routes the active-connect TCP segment frame copy through
    `BufferOperations.MemoryCopy` after existing frame-size validation.
- `src/OS.Kernel/Net/TcpPassiveIpv4FramePlanner.cs`
  - Uses `OS.Std.Memory`.
  - Routes the passive-open SYN-ACK segment frame copy through
    `BufferOperations.MemoryCopy` after existing frame-size validation.
- `src/OS.Kernel/Net/TcpEstablishedStreamIpv4FramePlanner.cs`
  - Uses `OS.Std.Memory`.
  - Routes the established payload segment frame copy through
    `BufferOperations.MemoryCopy` after existing frame-size validation.
- `src/OS.Kernel/Net/TcpConnectionCloseIpv4FramePlanner.cs`
  - Uses `OS.Std.Memory`.
  - Routes the active-close FIN/ACK segment frame copy through
    `BufferOperations.MemoryCopy` after existing frame-size validation.
- `tests/OS.HostTests/TcpIpv4FramePlannerTests.cs`
  - Added the focused source guard marker
    `TEST_PASS net.tcp ipv4 frame planners use shared buffer operations`.
- `docs/networking.md`
  - Documented #755 host-tested TCP IPv4 frame planner copy evidence.
- `done/step798.md`
  - Recorded this evidence step.
- `artifacts/logs/issue755-hosttests-build.log`
- `artifacts/logs/issue755-hosttests-run.log`
- `artifacts/logs/issue755-diff-check.log`
- `artifacts/logs/issue755-trailing-whitespace.log`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- Scoped `git diff --check` over the files changed for #755.
- Scoped trailing whitespace scan over the files changed for #755.

## Evidence

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: passed.
  - Log: `artifacts/logs/issue755-hosttests-build.log`.
  - Evidence: `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Result: passed.
  - Log: `artifacts/logs/issue755-hosttests-run.log`.
  - Relevant markers observed:
    - `TEST_PASS net.tcp ipv4 frame planners use shared buffer operations`
    - `TEST_PASS host.tests`
- Scoped `git diff --check`
  - Result: passed.
  - Log: `artifacts/logs/issue755-diff-check.log`.
- Scoped trailing whitespace scan
  - Result: passed.
  - Log: `artifacts/logs/issue755-trailing-whitespace.log`.

## Failures

- None.

## Deferred work

- Continue migrating remaining network copy sites only when their surrounding
  packet/planner status behavior is covered by focused tests.
- This does not implement live virtio-net packet I/O, TCP control blocks,
  sockets/PAL exports, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.

## Evidence boundary

- Host-tested source guard and caller-owned TCP IPv4 frame planner behavior
  only.
- No live NIC, no QEMU boot proof, no cloud-provider evidence, and no
  production-readiness claim.
