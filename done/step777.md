# Step 777 - IPv4 neighbor route formatter shared ASCII builder

## Summary
Migrated `Ipv4NeighborRoutePlanDiagnosticFormatter` marker appends and integer
writes to the shared no-runtime `OS.Std.Text.AsciiTextBuilder` path while
preserving the stable `IPV4_NEIGHBOR_ROUTE ...` marker text and query behavior.

## Why
The IPv4 neighbor route formatter is part of the Phase F networking diagnostic
surface for ARP/cache route planning. Reusing shared ASCII text builder helpers
keeps networking marker formatting aligned with the no-runtime text surface
before live virtio-net, ARP retries, or TCP/HTTP smoke wiring consume these
markers.

## Files changed
- `src/OS.Kernel/Net/Ipv4NeighborRoutePlanDiagnosticFormatter.cs`
- `tests/OS.HostTests/Ipv4NeighborRoutePlannerTests.cs`
- `docs/networking.md`
- `done/step777.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue736-ipv4-neighbor-route-formatter\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue736-host-build-20260525.log`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue736-ipv4-neighbor-route-formatter-r2\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue736-host-build-r2-20260525.log`
- `dotnet .\artifacts\issue736-ipv4-neighbor-route-formatter-r2\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue736-hosttests-r2-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue736-git-diff-check-20260525.log`
- `git diff --check -- .\src\OS.Kernel\Net\Ipv4NeighborRoutePlanDiagnosticFormatter.cs .\tests\OS.HostTests\Ipv4NeighborRoutePlannerTests.cs .\docs\networking.md .\done\step777.md *>&1 | Tee-Object -FilePath .\artifacts\logs\issue736-git-diff-check-scoped-20260525.log`
- `rg -n "[ \t]$" .\src\OS.Kernel\Net\Ipv4NeighborRoutePlanDiagnosticFormatter.cs .\tests\OS.HostTests\Ipv4NeighborRoutePlannerTests.cs .\docs\networking.md .\done\step777.md`

## Evidence
- `artifacts/logs/issue736-host-build-r2-20260525.log` shows the focused
  host-test build succeeded with `0 Warning(s)` and `0 Error(s)`.
- `artifacts/logs/issue736-hosttests-r2-20260525.log` ends with
  `TEST_PASS host.tests` and includes
  `TEST_PASS net.ipv4.neighbor.formatter uses shared ascii text builder`.
- `artifacts/logs/issue736-git-diff-check-20260525.log` and
  `artifacts/logs/issue736-git-diff-check-scoped-final-20260525.log` exited 0
  while reporting only the known LF-to-CRLF normalization warnings in the
  current multi-agent worktree.
- The scoped trailing-whitespace source scan returned
  `TEST_PASS issue736.scoped_whitespace_source_final`.

## Failures
- The first build attempt in `artifacts/logs/issue736-host-build-20260525.log`
  failed because an older issue-specific `dotnet` host-test process held
  `artifacts/issue736-ipv4-neighbor-route-formatter/bin/Debug/net10.0/OS.Std.NoRuntime.Shared.dll`
  open. The retry used a fresh `issue736-ipv4-neighbor-route-formatter-r2`
  output directory and passed.

## Deferred work
- Live virtio-net RX/TX descriptors, ARP retries, timer-driven cache expiry,
  IPv4 packet routing over a NIC, FetchApp execution, hosted .NET, ASP.NET
  Minimal API, and Kestrel remain outside this formatter migration.

## Evidence boundary
This step is intended to prove host-tested caller-owned formatting of already
produced IPv4 neighbor route plan metadata through shared no-runtime ASCII text
builder helpers. It does not prove live NIC I/O, DHCP, DNS, TCP, HTTP, FetchApp,
hosted .NET, Kestrel, or cloud-provider behavior.

