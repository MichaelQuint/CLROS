# Step 436 - Package loader result default guard

## Summary

Implemented Gitea issue #421 by making package-loader and GPT-ESP readiness
results reject default/zero values as successful storage evidence.

## Why

`OsPackageLoadStatus.Success` and `GptEspLocationReadinessStatus.Success` are
zero. `default(OsPackageLoadResult)` and
`default(GptEspLocationReadinessResult)` previously reported success because
`Succeeded` only checked the status field. Higher app-selection and boot paths
must not treat zero/default package-loader metadata as initialized package or
GPT-ESP readiness evidence.

## Files changed

- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step436.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build | Select-String -Pattern 'storage\.package loader default result is not success|storage\.package loader gpt esp readiness default result is not success|TEST_PASS host\.tests'`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`

## Evidence

- Focused host-test build passed with 0 warnings and 0 errors.
- Package-loader default-result marker evidence:

```text
TEST_START storage.package loader default result is not success
TEST_PASS storage.package loader default result is not success
TEST_START storage.package loader gpt esp readiness default result is not success
TEST_PASS storage.package loader gpt esp readiness default result is not success
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

- None.

## Deferred work

Package parser and package filesystem result defaults remain separate
hardening slices. This step is host-tested package-loader contract evidence
only; it does not implement live disk I/O, package boot, hosted .NET, Kestrel,
cloud readiness, or production readiness.
