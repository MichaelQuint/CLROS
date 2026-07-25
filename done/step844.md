# Step 844 - M00-020 Machine-Readable Evidence Manifest

## Summary

Added a deterministic evidence-manifest writer and CLI that independently
binds Git state, tools, commands, artifacts, serial bytes, markers, and time.

## Why

Loose logs and copied hashes cannot prove that one source state produced one
accepted run.

## Files changed

- `tools/OS.QemuRunner/EvidenceManifestWriter.cs`
- `tools/OS.QemuRunner/EvidenceManifestCommand.cs`
- `tools/OS.QemuRunner/Program.cs`
- `tests/OS.HostTests/EvidenceManifestWriterTests.cs`
- `docs/qemu.md`
- `docs/completion-gates.md`

## Tests run

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

## Evidence

The writer derives Git state twice, hashes bytes under the read handle, parses
serial marker claims, requires config/EFI/image/serial roles, rejects overlap
and stale or forged input, and writes deterministic UTF-8 without BOM through
atomic replacement. The production manifest is recorded in Step 846.

## Failures

Negative tests cover dirty/commit drift, malformed values, missing roles,
empty/unreadable input, hash mismatch, marker forgery, conflicting terminals,
invalid UTF-8, and output/input overlap.

## Deferred work

Later gates must produce their own manifests; this record cannot be reused as
proof of a different source state.

## Evidence boundary

This proves evidence serialization and validation, not the hardware behavior
described by a future manifest.
