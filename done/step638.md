# Step 638 - Libvirt image-format path validation

## Summary

Added deterministic host-side image validation to `OS.LibvirtRunner`
`--validate-paths`. The libvirt runner now rejects empty image files and rejects
raw/qcow2 command-line combinations that do not match the local image header.
Diagnostics now report the validated image byte count and detected image format.

## Why

Issue #7 needs cloud image tooling to fail before a real libvirt lifecycle is
implemented when a caller wires the wrong disk artifact or disk-format flag.
The previous libvirt path validation checked that an image path existed, but a
qcow2 artifact could still be passed as `raw`, or a raw-compatible artifact
could be passed as `qcow2`, leaving the failure to a future libvirt/QEMU run.

## Files changed

- `tools/OS.LibvirtRunner/Program.cs`
  - Added image byte-count and qcow2-header detection for `--validate-paths`.
  - Added deterministic mismatch errors for `raw` plus qcow2 header and
    `qcow2` plus raw-compatible header.
  - Added `LIBVIRT_DIAG image.bytes=<n>` and
    `LIBVIRT_DIAG image.detected.format=...` diagnostics.
- `tests/OS.HostTests/LibvirtRunnerTests.cs`
  - Added host tests for image-format diagnostics, raw/qcow2 mismatches, and
    empty-image rejection.
  - Updated existing validate-path fixtures to use non-empty raw-compatible
    test images.
- `docs/libvirt.md`
  - Documented the image-format validation behavior, diagnostics, and evidence
    boundary.
- `done/step638.md`
  - Records this step and its verification boundary.

## Tests run

- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi -o artifacts\bin\hosttests-step638`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-dependencies /p:OSWorkloadProfile=SingleMinimalApi -o artifacts\step638\bin\Debug\net10.0`
- `dotnet .\artifacts\step638\bin\Debug\net10.0\OS.HostTests.dll`
- Direct `OS.LibvirtRunner` fixture commands under
  `artifacts\logs\step638-libvirt-validation`.

## Evidence

- `OS.LibvirtRunner` build passed with 0 warnings and 0 errors.
- The isolated host-test run emitted:
  - `TEST_PASS libvirt validate-paths reports image format diagnostics`
  - `TEST_PASS libvirt validate-paths rejects raw qcow2 mismatch`
  - `TEST_PASS libvirt validate-paths rejects qcow2 raw mismatch`
  - `TEST_PASS libvirt validate-paths rejects empty image`
- Direct runner validation wrote
  `artifacts\logs\step638-libvirt-validation\validation.log` and showed:
  - successful raw-compatible validation with `PASS_EXIT 0`,
    `LIBVIRT_DIAG image.bytes=8`, and
    `LIBVIRT_DIAG image.detected.format=raw-compatible`;
  - `RAW_MISMATCH_EXIT 1` with `expected raw but detected qcow2`;
  - `QCOW_MISMATCH_EXIT 1` with
    `expected qcow2 but detected raw-compatible`;
  - `EMPTY_EXIT 1` with `Image file is empty`.

## Failures

- `dotnet build .\tools\OS.LibvirtRunner\OS.LibvirtRunner.csproj --nologo`
  without `OSWorkloadProfile` failed at the repository workload-profile guard:
  `Invalid OSWorkloadProfile ''. Valid values: SingleMinimalApi, MultiMinimalApi.`
- A normal host-test project build with `OSWorkloadProfile=SingleMinimalApi`
  failed because another `OS.HostTests` process held
  `tests\OS.HostTests\bin\Debug\net10.0\OS.Kernel.dll`.
- A full isolated host-test dependency build later failed in unrelated PAL
  status-contract source with `CS0133` constant-expression errors in
  `src/OS.Kernel/Pal/OSHostStatusContract.cs`.
- The isolated no-dependencies host-test run reached and passed the new libvirt
  tests, then failed later in unrelated PAL status-contract tests with
  `TypeLoadException` because the isolated dependency set could not compile or
  load the current PAL status-contract types.

## Deferred work

- This remains host-only validation. It does not connect to libvirt, define a
  domain, start a guest, poll a live serial log, or prove libvirt boot.
- Real lifecycle execution still requires the planned explicit
  `--allow-real-libvirt-lifecycle` implementation, host libvirt prerequisites,
  timeout cleanup, and live serial-marker evidence.

