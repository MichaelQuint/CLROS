# Step 122 - PAL Socket Options And Endpoint Queries

## What Changed

- Added POD socket option value contracts for Kestrel-oriented `NoDelay`, `ReuseAddress`, receive timeout, send timeout, and keep-alive settings.
- Added mock `OSHostSocketTable` set/get option support, local endpoint query support, and connected remote endpoint query support.
- Added deterministic `ReuseAddress` bind policy: duplicate binds are allowed only when the new socket and every conflicting bound/listening socket have `ReuseAddress` enabled.
- Updated PAL, networking, Kestrel, and completion-gate docs to record the host-only evidence boundary.

## Why It Changed

Issue #144 needed the mock PAL socket contract to expose the next Kestrel-facing socket option and endpoint-query surface without claiming exported PAL behavior, TCP behavior, hosted .NET, or Kestrel support.

## Files Changed

- `src/OS.Kernel/Pal/OSHostSocketAbi.cs`
- `src/OS.Kernel/Pal/OSHostSocketTable.cs`
- `tests/OS.HostTests/PalSocketAbiTests.cs`
- `tests/OS.HostTests/PalSocketTableTests.cs`
- `docs/pal.md`
- `docs/networking.md`
- `docs/kestrel.md`
- `work/PAL/pal-design.md`
- `docs/completion-gates.md`
- `done/step122.md`

## Tests Run

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:UseAppHost=false /p:OutputPath=C:\Projects\OS\artifacts\pal144-output\
dotnet C:\Projects\OS\artifacts\pal144-output\OS.HostTests.dll
```

## Logs And Evidence

- Alternate-output host test project build passed with 0 warnings and 0 errors.
- Host tests emitted:
  - `TEST_PASS pal.socket options store kestrel flags`
  - `TEST_PASS pal.socket options reject unsupported values`
  - `TEST_PASS pal.socket endpoints report bound local address`
  - `TEST_PASS pal.socket endpoints report accepted peer address`
  - `TEST_PASS pal.socket reuse address policy is deterministic`
  - `TEST_PASS host.tests`

## Failures

- The default host-test build path failed because existing `OS.HostTests` processes were locking `tests/OS.HostTests/bin/Debug/net10.0/OS.HostTests.exe` and `.dll`.
- A first alternate-output build attempt using `BaseIntermediateOutputPath` under the source tree caused duplicate generated assembly attributes; those generated alternate folders were removed and the successful verification used `OutputPath` under `artifacts`.

## Deferred Work

- No exported `OSHost_SocketSetOption`, address-query, bind, listen, accept, send, receive, poll, shutdown, or close symbols were added.
- No TCP stack behavior, scheduler wait integration, hosted .NET 10 runtime, ASP.NET Minimal API launch, or Kestrel socket binding was added.

## Evidence Boundary

This step proves host-only PAL socket option, endpoint-query, and deterministic mock bind-policy contracts. It does not prove exported PAL behavior, guest networking, TCP, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
