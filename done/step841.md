# Step 841 - M00-017 EFI Toolchain Preflight

## Summary

Made EFI build preflight report exact linker, Windows SDK, ILCompiler, QEMU,
qemu-img, and OVMF discovery.

## Why

EFI evidence must identify the tools and firmware that produced it and fail
with stable reasons when a prerequisite is missing or ambiguous.

## Files changed

- `scripts/build-efi-shell.ps1`
- `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`
- `docs/efi-build.md`
- `docs/host-requirements.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-efi-shell.ps1 -PreflightOnly
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

Host fixtures cover explicit paths, search roots, numeric toolset selection,
missing tools, undersized OVMF, and exact stable discovery markers. The final
selected versions are bound in Step 846.

## Failures

Preflight previously mixed implicit discovery with build failures.

## Deferred work

Different hosts must supply equivalent supported tools.

## Evidence boundary

This proves deterministic discovery, not the emitted EFI's behavior.
