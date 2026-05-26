# Step 300 - Guarded Platform Random Boundary

## What Changed

- Added `IPlatformRandom` and `IPlatformRandomSource` as the minimal platform RNG boundary.
- Added `GuardedPlatformRandom`, which fills caller-owned byte buffers only while UEFI boot services are available.
- Added status-coded `PlatformRandomResult` and `PlatformRandomSourceResult` values for success, UEFI-gone denial, empty destinations, source failures, and short reads.
- Added host tests proving the adapter denies firmware RNG calls in UEFI-gone simulation and after marked `ExitBootServices`.
- Updated boot, security, and completion-gate documentation to state that this is a firmware RNG guard only.

## Why

The OS needs platform interfaces that can be safely rejected after the firmware boundary before real `ExitBootServices` is enabled. Randomness has to be represented early because UEFI RNG may be useful for seeding future runtime entropy, but no code should accidentally keep using firmware services after the UEFI-gone boundary.

## Files Changed

- `src/OS.Kernel/Platform/IPlatformRandom.cs`
- `src/OS.Kernel/Platform/GuardedPlatformRandom.cs`
- `tests/OS.HostTests/PlatformRandomGuardTests.cs`
- `docs/boot.md`
- `docs/security.md`
- `docs/completion-gates.md`
- `done/step300.md`

## Tests Run

```powershell
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

- Managed build passed with 0 warnings and 0 errors.
- Host tests emitted `TEST_PASS host.tests`.
- Host tests emitted `TEST_PASS platform.random fill allowed before UEFI gone`.
- Host tests emitted `TEST_PASS platform.random fill denied in UEFI-gone simulation`.
- Host tests emitted `TEST_PASS platform.random fill denied after ExitBootServices`.
- Host tests emitted `TEST_PASS platform.random fill rejects empty destination`.
- Host tests emitted `TEST_PASS platform.random fill reports source failure`.
- Host tests emitted `TEST_PASS platform.random fill reports short read`.
- Host tests emitted `TEST_PASS platform.random result contains no managed references`.

## Failures

- None in the #300 implementation.

## Deferred Work

- Real UEFI RNG protocol binding.
- Entropy-pool design and seeding before `ExitBootServices`.
- Post-firmware RNG source.
- TLS and cryptographic APIs.
- Hosted .NET/Kestrel randomness requirements.

## Evidence Boundary

This step proves a host-tested firmware RNG guard over an injected source. It does not prove real UEFI RNG protocol calls, entropy quality, post-`ExitBootServices` randomness, cryptographic APIs, TLS, hosted .NET, ASP.NET Minimal API, or Kestrel support.
