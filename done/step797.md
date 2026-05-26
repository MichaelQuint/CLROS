# Step 797 - GPT partition header copy BufferOperations guard

## Summary

Issue #756 migrates the GPT partition table parser's validated header CRC stack
copy from direct span `CopyTo` onto the shared no-runtime
`OS.Std.Memory.BufferOperations.MemoryCopy` helper.

## Why

The GPT parser is a caller-buffer storage contract. Keeping the header CRC
scratch copy on the shared buffer operation path reduces parser-local BCL-like
copy behavior while preserving existing CRC validation and parser status
boundaries.

## Files changed

- `src/OS.Kernel/Storage/GptPartitionTableParser.cs`
- `tests/OS.HostTests/GptPartitionTableParserTests.cs`
- `docs/cstorage.md`
- `done/step797.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Result: pass
  - Log: `artifacts/logs/issue756-host-build.log`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Result: pass
  - Log: `artifacts/logs/issue756-host-tests.log`
- `git diff --check -- src/OS.Kernel/Storage/GptPartitionTableParser.cs tests/OS.HostTests/GptPartitionTableParserTests.cs docs/cstorage.md done/step797.md`
  - Result: pass
  - Log: `artifacts/logs/issue756-diff-check.log`
- Scoped trailing whitespace scan over the files above
  - Result: pass
  - Log: `artifacts/logs/issue756-trailing-whitespace.log`

## Evidence

- `TEST_PASS done.step.allocate`
- `TEST_PASS storage.gpt.partition parser header copy uses shared buffer operations`
- `TEST_PASS storage.gpt.parser.crc validates good buffers`
- `TEST_PASS host.tests`
- `GptPartitionTableParser` now imports `OS.Std.Memory` and copies the
  validated header-size slice into the bounded stack buffer through
  `BufferOperations.MemoryCopy`.
- Unexpected shared-copy failure after validation maps to the existing
  `GptPartitionTableStatus.HeaderSectorTooSmall` non-success status.

## Failures

None.

## Deferred work

This is host-tested caller-buffer GPT parser evidence only. It does not prove
live virtio-blk, post-firmware disk reads, boot-time manifest/package loading,
hosted .NET, ASP.NET Minimal API, or Kestrel behavior.
