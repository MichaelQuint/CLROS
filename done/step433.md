# Step 433 - GPT public result default guard

## Summary

Implemented Gitea issue #418 by making public legacy GPT result structs reject
default/zero values as successful parser or ESP metadata.

## Why

`GptStatus.Success` is zero. `default(GptParseResult)`,
`default(GptPartitionSelectionResult)`, and
`default(GptEfiSystemPartitionLocationResult)` previously reported success
because `Succeeded` only checked the status field. Future package and manifest
readiness callers must not treat zero/default GPT metadata as initialized disk
evidence.

## Files changed

- `src/OS.Kernel/Storage/GptParseResult.cs`
- `src/OS.Kernel/Storage/GptPartitionSelectionResult.cs`
- `src/OS.Kernel/Storage/GptEfiSystemPartitionLocationResult.cs`
- `tests/OS.HostTests/StorageGptParserTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step433.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage gpt result defaults are not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- GPT default-result marker evidence:

```text
TEST_START storage gpt result defaults are not success
TEST_PASS storage gpt result defaults are not success
TEST_PASS host.tests
```

- Full solution build passed with 0 warnings and 0 errors.
- Naming and source firewall guard emitted:

```text
TEST_START naming.guard
TEST_PASS naming.guard
TEST_START source.firewall
TEST_PASS source.firewall
```

## Failures

- Initial marker attempt failed because the test asserted that public GPT result
  structs contain no managed references. Existing GPT partition metadata carries
  a `string` partition name, so the test was corrected to cover the initialized
  success/failure contract without changing existing metadata shape.

## Deferred work

`GptPartitionTableParseResult` and package parser/loader result defaults remain
separate hardening slices. This step is host-tested GPT parser contract evidence
only; it does not implement live disk I/O, package boot, hosted .NET, Kestrel,
cloud readiness, or production readiness.
