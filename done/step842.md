# Step 842 - M00-018 EFI Artifact Verification

## Summary

Added same-build verification for the emitted EFI PE32+ x64 application.

## Why

A filename or synthetic PE header is insufficient evidence of the linked
`EfiMain` image and its import boundary.

## Files changed

- `scripts/test-efi-artifact.ps1`
- `tests/OS.HostTests/EfiArtifactVerificationScriptTests.cs`
- `docs/efi-build.md`

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-efi-artifact.ps1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The verifier binds the EFI file to its linker response/map/build record, checks
PE32+, AMD64, EFI application subsystem, executable/file-backed `EfiMain`
entrypoint, and absent ordinary, bound, IAT, and delay imports.

## Failures

Malformed, wrong-machine, wrong-subsystem, import-bearing, mismatched-build, and
RET-only synthetic fixtures are rejected.

## Deferred work

Static PE verification does not prove firmware execution.

## Evidence boundary

This proves artifact structure and same-build association only.
