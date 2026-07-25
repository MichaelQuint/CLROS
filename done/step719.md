# Step 719 - Parser shared-memory helper migrations

## Summary

Closed four Phase C/G parser cleanup slices by moving additional byte-prefix,
byte-equality, and magic-signature checks onto the shared no-runtime memory
helper surface. The changed paths keep their existing status-code behavior while
removing local loop helpers that duplicated `OS.Std.MemoryExtensions`.

## Why

CloudReady kernel, app-model, and storage paths should consume shared
`std/no-runtime/shared` utility contracts instead of accumulating ad hoc
per-parser byte loops. These migrations reduce duplicated parser logic before
the same manifest/package/app payload contracts are wired into live boot and
storage paths.

## Files changed

- `src/OS.Kernel/Manifest/NativeManifestParser.cs`
  - Uses `OS.Std.MemoryExtensions.StartsWith` for `env.` manifest keys.
  - Uses `OS.Std.MemoryExtensions.SequenceEqual` for duplicate environment-key
    checks.
  - Removed private `HasPrefix` and `ByteSequenceEqual` loops.
- `tests/OS.HostTests/NativeManifestParserTests.cs`
  - Added source guard marker:
    `TEST_PASS manifest kernel uses shared memory env helpers`.
- `src/OS.Kernel/Runtime/NativeAppExitStatus.cs`
  - Uses `OS.Std.MemoryExtensions.SequenceEqual` for machine-readable
    app-exit status token parsing.
  - Removed private `EqualsBytes`.
- `tests/OS.HostTests/NativeAppExitStatusTests.cs`
  - Added address-status alias coverage.
  - Added source guard marker:
    `TEST_PASS appmodel.exit status parser uses shared memory helpers`.
- `src/OS.Kernel/Storage/OsPackageParser.cs`
  - Uses `OS.Std.MemoryExtensions.SequenceEqual` for package magic, file lookup,
    and duplicate-path comparisons.
  - Removed private path equality loop.
- `tests/OS.HostTests/StoragePackageParserTests.cs`
  - Added source guard marker:
    `TEST_PASS storage.package parser uses shared memory equality helper`.
- `src/OS.Kernel/Manifest/NativeAppPayloadHeader.cs`
  - Uses `OS.Std.MemoryExtensions.SequenceEqual` for the `OSAPPv0\0` native
    payload magic check.
- `tests/OS.HostTests/NativeAppPayloadHeaderTests.cs`
  - Added source guard marker:
    `TEST_PASS appmodel payload header uses shared memory equality helper`.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `artifacts/logs/issues659-662-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
  - `EXIT_CODE 0`
- `artifacts/logs/issues659-662-hosttests-20260525.log`
  - `TEST_PASS manifest kernel uses shared memory env helpers`
  - `TEST_PASS appmodel.exit status parser uses shared memory helpers`
  - `TEST_PASS storage.package parser uses shared memory equality helper`
  - `TEST_PASS appmodel payload header uses shared memory equality helper`
  - `TEST_PASS host.tests`
  - `EXIT_CODE 0`
- `artifacts/logs/issues659-662-git-diff-check-20260525.log`
  - `EXIT_CODE 0`
  - Git reported only LF/CRLF working-copy conversion warnings.

## Failures

- Worker-local default-output builds briefly hit stale host-test/tool output
  locks while #660 and #662 were being implemented in parallel. Isolated worker
  validation passed, and the final combined validation passed on the normal
  output path without requiring process termination.
- No QEMU or libvirt boot was run for this slice; the changed behavior is
  host-tested parser/helper migration only.

## Deferred work

- Continue migrating remaining ad hoc byte loops in storage, ACPI/GPT, and
  app-model parsers onto shared no-runtime helpers where the BCL-compatible
  helper contract already exists.
- Live virtio-blk package reads, native app invocation, and cloud-provider boot
  proof remain separate completion-gate work.
- These changes do not claim cloud readiness, hosted .NET, Kestrel execution in
  OS, real `ExitBootServices`, or live device support.

