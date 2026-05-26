# Step 420 - Virtio-blk native app selection preflight

## Summary

Added a host-tested app-model preflight that gates native app selection on a
completed caller-owned virtio-blk read-session result, valid injected block
device geometry, and app-selection scratch-plan success.

## Why

Issue #405 needed the next small bridge from storage read-session provenance
toward selected-app metadata without wiring live boot storage or executing app
code. The preflight rejects incomplete read-session state before scratch
validation, validates boot-owned scratch sizing, delegates to the existing
`NativeAppSelectionLoader`, and preserves nested selection evidence.

## Files changed

- `src/OS.Kernel/Manifest/VirtioBlockNativeAppSelectionPreflight.cs`
- `tests/OS.HostTests/VirtioBlockNativeAppSelectionPreflightTests.cs`
- `docs/app-model.md`
- `docs/cstorage.md`
- `docs/completion-gates.md`
- `done/step420.md`

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Host test build passed with 0 warnings and 0 errors.
- Host test run ended with `TEST_PASS host.tests`.
- Full solution build passed with 0 warnings and 0 errors.
- Naming/source firewall emitted `TEST_PASS naming.guard` and
  `TEST_PASS source.firewall`.
- `git diff --check` exited 0; Git reported CRLF normalization warnings only.
- New host markers:
  - `TEST_PASS appmodel.virtio selection preflight validates scratch`
  - `TEST_PASS appmodel.virtio selection preflight rejects incomplete session`
  - `TEST_PASS appmodel.virtio selection preflight preserves selection provenance`
  - `TEST_PASS appmodel.virtio selection preflight preserves no-launch boundary`

## Failures

- Claude subagent review ran, but its first Gitea comment attempt could not use
  Bash/curl under `dontAsk` permissions. Codex proceeded with local
  implementation and evidence, then retried Claude with `auto` permissions; the
  retry posted a `claude` user review comment to issue #405 with HTTP 201.

## Deferred work

- This is host-tested app-selection preflight only. It does not perform live
  virtio-blk reads, boot-time package loading, app execution, executable
  mapping, hosted .NET, Minimal API, Kestrel, cloud-provider support, or
  production readiness.
