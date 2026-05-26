# Step 639 - PAL Status Contract Extended Domains

## Summary
Extended the host-tested `OSHostStatusContract` status-domain validator beyond
config/environment/time/console-log to cover memory, package-file, and socket
PAL domains.

## Why
The PAL boundary needs deterministic, status-coded domain validation before the
future `OSHost_*` exports are wired. Memory, package-file, and socket facades
already have host-only mock behavior; this step adds matching domain-level
status masks and caller-buffer domain names without exporting PAL symbols.

## Files changed
- `src/OS.Kernel/Pal/OSHostStatusContract.cs`
  - Added `Memory`, `PackageFile`, and `Socket` ABI status domains.
  - Added deterministic allowed-status masks for those domains.
  - Added canonical `OSHOST_ABI_DOMAIN_MEMORY`,
    `OSHOST_ABI_DOMAIN_PACKAGE_FILE`, and `OSHOST_ABI_DOMAIN_SOCKET`
    caller-buffer names.
- `tests/OS.HostTests/PalStatusContractTests.cs`
  - Added markers for memory/package/socket status validation.
  - Added markers for extended domain-name formatting and short-buffer
    preservation.
- `docs/pal.md`
  - Updated the implemented PAL status-contract boundary.
- `work/PAL/pal-design.md`
  - Updated the PAL design status diagnostics description.
- `docs/completion-gates.md`
  - Updated Gate 10 host-tested PAL contract evidence wording.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check -- src/OS.Kernel/Pal/OSHostStatusContract.cs tests/OS.HostTests/PalStatusContractTests.cs docs/pal.md work/PAL/pal-design.md docs/completion-gates.md done/step639.md`

## Evidence
- Build passed with 0 warnings and 0 errors.
- Host-test output reached and passed:
  - `TEST_PASS pal.status_contract validates memory package and socket statuses`
  - `TEST_PASS pal.status_contract formats extended domain names`
  - `TEST_PASS pal.status_contract result layouts contain no managed references`
  - `TEST_PASS pal.status_contract default results are not success`
- `git diff --check` passed for touched files; it reported only existing
  LF-to-CRLF working-copy warnings in markdown files.

## Failures
- The full host-test process exited nonzero after the PAL markers because an
  unrelated QEMU runner test failed:
  `TEST_FAIL qemu dry-run emits default workload profile`.
- No live PAL exports, hosted runtime, Kestrel, QEMU boot proof, or cloud
  readiness are claimed by this step.

## Deferred work
- Add domain-contract coverage for thread/wait/timer/random/app-log if those
  surfaces need first-class status-domain validation.
- Reconcile the unrelated QEMU default-workload-profile failure in a separate
  host-tooling slice.
- Wire these contracts into real exported `OSHost_*` PAL functions only after
  the hosted-tier boundary is implemented.

