# Step 024 - PAL Socket Timer Thread Wait POD Declarations

## What Changed

- Extended `src/OS.Kernel/Pal/OSHostAbi.cs` with declaration-only POD structs for the next Kestrel-critical PAL data contracts:
  - `OSHostSocketAddress`
  - `OSHostSocketCreateOptions`
  - `OSHostSocketIoRequest`
  - `OSHostSocketPollEntry`
  - `OSHostTimeout`
  - `OSHostTimerOptions`
  - `OSHostThreadStart`
  - `OSHostThreadCreateOptions`
  - `OSHostWaitOneRequest`
  - `OSHostWaitManyRequest`
  - `OSHostWaitResult`
- Extended `tests/OS.HostTests/PalAbiLayoutTests.cs` to cover the new structs for sequential layout, public readonly fields, unmanaged/no-reference shape, stable sizes, and selected offsets.
- Updated `docs/pal.md` and `work/PAL/pal-design.md` to state that these are declaration/layout contracts only.

## Why It Changed

The hosted .NET 10 and Kestrel path needs stable unmanaged socket, timer, thread, and wait data contracts before exported `OSHost_*` functions or runtime adapters can safely target the PAL boundary.

## Files Changed

- `src/OS.Kernel/Pal/OSHostAbi.cs`
- `tests/OS.HostTests/PalAbiLayoutTests.cs`
- `docs/pal.md`
- `work/PAL/pal-design.md`
- `done/step024.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
dotnet run --project $env:TEMP\os-palabi-check-<guid>\PalAbiCheck.csproj --nologo
git diff --check -- .\src\OS.Kernel\Pal\OSHostAbi.cs .\tests\OS.HostTests\PalAbiLayoutTests.cs .\docs\pal.md .\work\PAL\pal-design.md .\done\step024.md
```

## Logs And Evidence

- `dotnet build .\src\OS.Kernel\OS.Kernel.csproj --nologo` passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo` passed with 0 warnings and 0 errors.
- A temporary focused PAL ABI verifier under `%TEMP%` referenced `src/OS.Kernel` and passed the same layout checks for the new structs.
- PAL ABI host-test markers passed:
  - `TEST_PASS pal abi structs use sequential layout`
  - `TEST_PASS pal abi fields are public readonly`
  - `TEST_PASS pal abi structs contain no managed references`
  - `TEST_PASS pal abi struct sizes are stable`
  - `TEST_PASS pal abi field offsets are stable`
- Scoped `git diff --check` exited 0 with only existing LF/CRLF normalization warnings.

## Failures

- Full `dotnet build .\OS.slnx --nologo -m:1` is currently blocked by unrelated `tools/OS.ImageBuilder/Program.cs` errors outside this issue's ownership, beginning with `CS1501: No overload for method 'Create' takes 3 arguments` and missing `RawEspImageBuilder.FatLayout` members.
- Full `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build` is currently blocked before PAL ABI tests complete because `ImageBuilderTests` invokes the same broken `OS.ImageBuilder` project.

## Deferred Work

- Add named constants for socket domains/types/protocols, poll event flags, timeout kinds, timer flags, and wait result kinds.
- Add exported `OSHost_*` functions only when linkability or behavior work requires them.
- Implement package/config reads, timers, thread/sync primitives, socket listen/accept/send/receive, and guest-side Kestrel evidence in later issues.

## Evidence Boundary

This step proves declaration and layout only. It does not export PAL functions, implement timers, implement threads, implement waits, implement sockets, run hosted .NET 10, or run Kestrel inside the guest.
