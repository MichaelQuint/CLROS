# Step 819 - GPT Partition Selector

## Summary

Added `GptPartitionSelector`, a host-tested selector over already-parsed
caller-owned `GptPartitionDescriptor` spans. It can select the first descriptor
by partition type GUID or unique partition GUID and preserves selected range,
name metadata, match count, and duplicate count without reparsing GPT bytes or
reading a block device.

## Why

Future storage/cloud paths need reusable partition selection over the
caller-buffer GPT parser output before live virtio-blk exists. The existing
legacy ESP selector reads from an `IReadOnlyBlockDevice`; this slice adds the
smaller no-allocation metadata selector needed by later package/app loading
work.

## Files changed

- `src/OS.Kernel/Storage/GptPartitionSelector.cs`
- `tests/OS.HostTests/GptPartitionSelectorTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step819.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo *> .\artifacts\logs\issue779-host-build.log`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll *> .\artifacts\logs\issue779-host-tests.log`

## Evidence

- `artifacts/logs/issue779-host-build.log` ended with `Build succeeded.`,
  `0 Warning(s)`, `0 Error(s)`, and `issue779-host-build-exit=0`.
- `artifacts/logs/issue779-host-tests.log` ended with `TEST_PASS host.tests`
  and `issue779-host-tests-exit=0`.
- `artifacts/logs/issue779-host-tests.log` emitted:
  - `TEST_PASS storage.gpt selector selects type guid`
  - `TEST_PASS storage.gpt selector selects unique guid`
  - `TEST_PASS storage.gpt selector reports missing partition`
  - `TEST_PASS storage.gpt selector reports duplicate type metadata`
  - `TEST_PASS storage.gpt selector rejects failed default parse result`
  - `TEST_PASS storage.gpt selector rejects invalid descriptor metadata`
  - `TEST_PASS storage.gpt selector result layouts contain no managed references`
- `artifacts/logs/issue779-diff-check.log` ended with
  `git-diff-check-exit=0`.
- `artifacts/logs/issue779-trailing-whitespace.log` emitted
  `TEST_PASS issue779.trailing_whitespace`.

## Failures

None in the saved #779 build or host-test logs.

## Deferred work

- This remains host-tested GPT metadata selection only. It does not prove live
  virtio-blk, real disk reads, boot-time manifest/package loading, app
  execution, cloud-provider storage behavior, or production readiness.
