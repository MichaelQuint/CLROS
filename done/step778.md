# Step 778 - PAL status formatter shared ASCII builder migration

## Summary
Migrated the PAL status-name and status-category caller-buffer formatters from
direct span copying to the shared no-runtime `OS.Std.Text.AsciiTextBuilder`
append path.

## Why
The status diagnostic helpers should use the same shared ASCII text builder as
newer PAL and diagnostic formatters, while preserving their exact status-code
behavior, required-byte query behavior, short-destination no-write behavior,
unknown-value handling, and default-result rejection.

## Files changed
- `src/OS.Kernel/Pal/OSHostStatusNameFormatter.cs`
- `src/OS.Kernel/Pal/OSHostStatusCategory.cs`
- `tests/OS.HostTests/PalStatusNameFormatterTests.cs`
- `tests/OS.HostTests/PalStatusCategoryTests.cs`
- `docs/pal.md`
- `done/step778.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue735-pal-status-formatters\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue735-host-build-20260525.log`
- `dotnet .\artifacts\issue735-pal-status-formatters\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue735-hosttests-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue735-git-diff-check-20260525.log`
- Scoped owned-file whitespace/source check saved to
  `artifacts/logs/issue735-scoped-whitespace-source-20260525.log`

## Evidence
- Build log: `artifacts/logs/issue735-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host-test log: `artifacts/logs/issue735-hosttests-20260525.log`
  - `TEST_PASS pal.status_names formats all canonical statuses`
  - `TEST_PASS pal.status_names reports required bytes`
  - `TEST_PASS pal.status_names rejects short buffer`
  - `TEST_PASS pal.status_names rejects unknown status`
  - `TEST_PASS pal.status_names preserves caller buffer on failure`
  - `TEST_PASS pal.status_names default result is not success`
  - `TEST_PASS pal.status_names uses shared ascii text builder`
  - `TEST_PASS pal.status_categories maps canonical statuses`
  - `TEST_PASS pal.status_categories formats category names`
  - `TEST_PASS pal.status_categories reports required bytes`
  - `TEST_PASS pal.status_categories rejects short buffer`
  - `TEST_PASS pal.status_categories maps unknown status`
  - `TEST_PASS pal.status_categories default result is not success`
  - `TEST_PASS pal.status_categories uses shared ascii text builder`
  - `TEST_PASS host.tests`
- Scoped check log:
  `artifacts/logs/issue735-scoped-whitespace-source-20260525.log`
  - `TEST_PASS issue735.scoped_whitespace_source`

## Failures
- No build or host-test failures were observed.
- Repository-wide `git diff --check` returned only existing LF-to-CRLF
  normalization warnings in the active multi-agent worktree. The scoped
  owned-file whitespace/source check passed.
- No Gitea credential was visible in this task context, so issue #735 was not
  commented or closed from this worker.

## Deferred work
- Parent should post the evidence from this step to Gitea #735 and close the
  issue if the issue policy requires API-side closure.
- These helpers remain diagnostic/status-contract formatters only; no exported
  PAL diagnostic function or hosted-runtime consumer is implemented here.

## Evidence boundary
This step proves by host tests and source guards that the PAL status-name and
status-category formatters use `AsciiTextBuilder.AppendBytes` for successful
caller-buffer writes while preserving existing observable behavior. It does not
export PAL symbols, write serial output, expose diagnostics to hosted .NET,
start Kestrel, or prove cloud readiness.

