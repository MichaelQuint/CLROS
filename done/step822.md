# Step 822 - GPT selector integration in caller-buffer package loader

## Summary

Integrated the caller-owned GPT partition selector into the caller-buffer
package-loader ESP path. `OsPackageLoader.LoadFromPrimaryGptEspWithCallerBuffers`
now uses `GptPartitionSelector.SelectByPartitionTypeGuid` for parsed primary and
backup GPT descriptor spans instead of carrying a separate manual EFI System
Partition scan.

## Why

The GPT selector from step 819 was host-tested as a standalone storage contract
but was not exercised by the package/manifest readiness flow. This step moves
one real package-loader call path onto the selector while preserving the
existing status-coded storage boundary and primary/backup table-source evidence.

## Files changed

- `src/OS.Kernel/Storage/OsPackageLoader.cs`
- `tests/OS.HostTests/OsPackageLoaderTests.cs`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step822.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet .\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.dll`

## Evidence

- `artifacts/logs/issue780-host-build.log`: host build exited 0.
- `artifacts/logs/issue780-host-tests.log`: package-loader markers passed,
  including `TEST_PASS storage.package loader caller-buffer uses gpt partition
  selector`.
- Later final active-issue verification in
  `artifacts/logs/issue777-repro-host-tests.log` reached
  `TEST_PASS host.tests` after the unrelated TCP connection table blocker was
  resolved.
- Existing caller-buffer package-loader markers still passed for package read,
  primary GPT source, backup fallback, backup GPT source, CRC mismatch, GPT
  scratch capacity, package capacity, default-result rejection, missing package,
  and malformed package.

## Failures

- The original #780 full host run exited nonzero after #780 markers due to the
  active out-of-scope #777 TCP connection table work:
  `TEST_FAIL net.tcp.connection table closes removes and reuses slot`. That
  blocker was resolved later; `artifacts/logs/issue777-repro-host-tests.log`
  now contains `TEST_PASS host.tests`.

## Deferred work

- This step does not add live virtio-blk reads, boot-time package loading,
  manifest loading from a real disk, app execution, or cloud storage proof.

