# Step 796 - TCP header BufferOperations copy guard

## Summary

Issue #754 migrates `TcpHeader.TryWrite` option and payload byte copies from
direct span `CopyTo` calls to the shared no-runtime
`OS.Std.Memory.BufferOperations.MemoryCopy` helper.

## Why

TCP header writes are still packet primitives over caller-owned buffers only.
Routing the validated option and payload copies through the shared buffer
operation keeps the TCP primitive aligned with the Phase F/G networking cleanup
without expanding the networking evidence boundary.

## Files changed

- `src/OS.Kernel/Net/TcpHeader.cs`
- `tests/OS.HostTests/TcpHeaderTests.cs`
- `docs/networking.md`
- `done/step796.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
  - Log: `artifacts/logs/issue754-host-build.log`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Result: pass
  - Log: `artifacts/logs/issue754-host-tests.log`
- `git diff --check -- src/OS.Kernel/Net/TcpHeader.cs tests/OS.HostTests/TcpHeaderTests.cs docs/networking.md done/step796.md`
  - Result: pass
  - Log: `artifacts/logs/issue754-diff-check.log`
- Scoped trailing whitespace scan over the files above
  - Result: pass
  - Log: `artifacts/logs/issue754-trailing-whitespace.log`

## Evidence

- `TEST_PASS done.step.allocate`
- `TEST_PASS net.tcp header payloads use shared buffer operations`
- `TEST_PASS host.tests`
- `TcpHeader.TryWrite` now calls
  `BufferOperations.MemoryCopy(options, segment.Slice(MinHeaderLength, options.Length))`
  after the existing option alignment and segment length validation.
- `TcpHeader.TryWrite` now calls
  `BufferOperations.MemoryCopy(payload, segment.Slice(headerLength, payload.Length))`
  after the existing segment length validation.
- Unexpected helper copy failures after validation map to
  `PacketParseStatus.BufferTooSmall`, preserving the existing non-success
  behavior for write failures.

## Failures

None.

## Deferred work

This is host-tested source and packet primitive evidence only. It does not
prove live virtio-net, TCP state, sockets, hosted .NET, ASP.NET Minimal API, or
Kestrel behavior.

