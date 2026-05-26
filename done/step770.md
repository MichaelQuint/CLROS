# Step 770 - Virtio-blk package staging formatter shared ASCII helper guard

## Summary
Updated the virtio-blk package-file staging formatter evidence slice so the
host source-guard marker uses the requested stable name and the storage doc
explicitly records that decimal byte-count accounting comes from the shared
ASCII decimal formatter.

## Why
Issue #727 is a Phase F storage cleanup: the
`APP_PACKAGE_FILE_STAGING_PREFLIGHT` diagnostic formatter must stay on the
shared no-runtime ASCII helper surface instead of growing formatter-local
decimal count or append loops.

## Files changed
- `src/OS.Kernel/Storage/VirtioBlockPackageFileStagingPreflightDiagnosticFormatter.cs`
- `tests/OS.HostTests/StorageVirtioBlockPackageReadPreflightTests.cs`
- `docs/cstorage.md`
- `done/step770.md`
- `artifacts/logs/issue727-host-build-20260525.log`
- `artifacts/logs/issue727-hosttests-20260525.log`
- `artifacts/logs/issue727-git-diff-check-20260525.log`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue727-storage-package-staging-formatter\bin\Debug\net10.0`
- `dotnet .\artifacts\issue727-storage-package-staging-formatter\bin\Debug\net10.0\OS.HostTests.dll`
- `git diff --check`
- Clean integrated rerun:
  `dotnet .\artifacts\issue730-cpu-fault-panic-formatter\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-hosttests-20260525.log`
- Clean integrated source check:
  `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue730-git-diff-check-20260525.log`

## Evidence
- Build log: `artifacts/logs/issue727-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Formatter source evidence:
  - `VirtioBlockPackageFileStagingPreflightDiagnosticFormatter` uses
    `AsciiDecimalFormatter.CountUInt64Digits(...)` for decimal byte-count
    calculation.
  - The caller-owned write path uses
    `AsciiTextBuilder.AppendBytes(...)` and
    `AsciiTextBuilder.AppendUInt64Decimal(...)`.
  - No formatter-local `GetUInt64DecimalByteCount`, direct decimal digit
    writes, `value /= 10;`, or `value.CopyTo(destination[offset..])` path is
    present in the formatter source.
- Host source-guard marker added in source:
  - `TEST_PASS storage virtio block package file staging formatter uses shared ascii text builder`
- Clean integrated host-test rerun:
  `artifacts/logs/issue730-hosttests-20260525.log`
  - `TEST_PASS storage virtio block package file staging formatter uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Clean integrated source check:
  `artifacts/logs/issue730-git-diff-check-20260525.log`
  exited 0 with only existing LF-to-CRLF working-copy warnings.
- Storage doc evidence:
  - `docs/cstorage.md` describes this as host-tested formatter evidence only
    and does not claim live virtio-blk, QEMU storage, real disk read, hosted
    .NET, or Kestrel behavior.

## Failures
- None in the clean integrated rerun.
- The original requested host-test command did not complete successfully. It
  failed before reaching the storage formatter test group:
  - `TEST_FAIL ci.smoke returns nonzero on failed command TimeoutException: run-ci-smoke.ps1 did not exit within 120 seconds.`
  - `Unhandled exception. System.InvalidOperationException: CI smoke script tests failed: 1`
- The clean integrated rerun passed the earlier CI smoke script section,
  emitted the storage formatter marker, and ended with `TEST_PASS host.tests`,
  so the original timeout is recorded as transient/concurrent harness behavior
  rather than #727 evidence.
- The requested full-tree `git diff --check` command returned nonzero in this
  dirty shared tree. Its log contains Git LF-to-CRLF working-copy warnings and
  no `path:line` whitespace diagnostics found by follow-up scan. A scoped
  diff-check over the issue-owned files returned 0 with only the same
  line-ending warnings. The clean integrated source check exited 0 with the
  same class of warnings and no issue-specific whitespace error.

## Deferred work
- Wire the package-file staging diagnostics into live virtio-blk package reads
  only after the live device, DMA/virtqueue, MMIO notification, and boot-time
  storage path have separate evidence.

## Evidence boundary
This step verifies host-tested caller-owned formatting for the virtio-blk
package-file staging diagnostic formatter: shared ASCII byte-count helpers,
shared ASCII text-builder appends, exact source-guard marker text, and
documentation of host-tested formatter evidence. It does not prove live
virtio-blk hardware I/O, QEMU storage reads, DMA, MMIO, device notification,
boot-time package loading from disk, executable mapping, hosted .NET startup,
Kestrel startup, cloud-provider behavior, or production readiness.

