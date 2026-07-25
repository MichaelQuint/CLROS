# Step 776 - QEMU FetchApp TCP exchange readiness marker preset

## Summary
Changed the QEMU `fetch-http-tcp-exchange` marker preset to require the
boot-visible `FETCH_APP_HTTP_TCP_EXCHANGE_READY` marker instead of the nested
diagnostic payload token `FETCH_HTTP_TCP_EXCHANGE`.

## Why
`SerialMarkerSummary` matches complete marker lines, either by exact line match
or by a line that starts with the required marker followed by a space. A real
boot readiness line is shaped as:

```text
FETCH_APP_HTTP_TCP_EXCHANGE_READY FETCH_HTTP_TCP_EXCHANGE ...
```

Requiring `FETCH_HTTP_TCP_EXCHANGE` would not match that line. The preset now
tracks the same readiness marker that `BootSequence` emits, while docs still
record that the diagnostic payload starts with `FETCH_HTTP_TCP_EXCHANGE`.

## Files changed
- `tools/OS.QemuRunner/SmokeMarkerPreset.cs`
- `tests/OS.HostTests/QemuRunnerTests.cs`
- `tests/OS.HostTests/CiSmokeScriptTests.cs`
- `docs/qemu.md`
- `done/step776.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -p:UseAppHost=false -o .\artifacts\issue734\bin\Debug\net10.0 *>&1 | Tee-Object -FilePath .\artifacts\logs\issue734-host-build-20260525.log`
- `dotnet .\artifacts\issue734\bin\Debug\net10.0\OS.HostTests.dll *>&1 | Tee-Object -FilePath .\artifacts\logs\issue734-hosttests-20260525.log`
- `.\scripts\run-qemu-smoke.ps1 -DryRun -MarkerPreset fetch-http-tcp-exchange *>&1 | Tee-Object -FilePath .\artifacts\logs\issue734-qemu-fetch-http-tcp-exchange-dryrun-20260525.log`
- `git diff --check *>&1 | Tee-Object -FilePath .\artifacts\logs\issue734-git-diff-check-20260525.log`
- `git diff --check -- .\tools\OS.QemuRunner\SmokeMarkerPreset.cs .\tests\OS.HostTests\QemuRunnerTests.cs .\tests\OS.HostTests\CiSmokeScriptTests.cs .\docs\qemu.md .\done\step776.md`

## Evidence
- Build log: `artifacts/logs/issue734-host-build-20260525.log`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- Host test log: `artifacts/logs/issue734-hosttests-20260525.log`
  - `TEST_PASS qemu.smoke.preset fetch http tcp exchange adds exchange marker`
  - `TEST_PASS qemu.smoke.preset fetch http tcp exchange aliases normalize`
  - `TEST_PASS qemu.smoke.preset script dry-run includes fetch http tcp exchange preset`
  - `TEST_PASS ci.smoke.dryrun includes fetch-http-tcp-exchange marker preset`
  - `TEST_PASS host.tests`
- Focused dry-run log:
  `artifacts/logs/issue734-qemu-fetch-http-tcp-exchange-dryrun-20260525.log`
  - `QEMU_MARKER_PRESETS fetch-http-tcp-exchange`
  - `MARKER_PRESETS fetch-http-tcp-exchange`
  - `REQUIRED_MARKERS ...;FETCH_APP_HTTP_TCP_EXCHANGE_READY`
  - `TEST_PASS qemu.runner.dryrun`

## Failures
- No build or host-test failures were observed.
- Repository-wide `git diff --check` is expected to emit existing LF-to-CRLF
  warnings in the active multi-agent worktree. The scoped check over the files
  changed by this step is the whitespace gate for this slice.

## Deferred work
- This is marker-contract tooling only. A future guest image still needs to
  invoke `BootSequence.RunFetchAppHttpTcpExchangeReadiness` after staged
  endpoint, DNS/route, TCP, HTTP, completion, and close-frame preconditions
  exist.
- Real QEMU proof for FetchApp HTTP-over-TCP remains deferred until the guest
  emits the readiness marker from an actual boot image.

## Evidence boundary
This step proves the QEMU runner and CI dry-run marker presets require the
boot-visible FetchApp TCP exchange readiness marker. It does not prove live
virtio-net, DHCP, DNS, TCP, HTTP GET, FetchApp execution, hosted .NET, Kestrel,
or real QEMU boot behavior for this marker.
