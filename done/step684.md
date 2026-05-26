# Step 684 - Kestrel Host Fixture Bounded HTTP Read

## Summary

Hardened the host-only Kestrel smoke fixture so full host-test runs do not hang
when the Minimal API app is started under redirected output. The fixture now
uses bounded raw loopback HTTP reads and decodes chunked response bodies for
the `/config` route.

## Why

The integrated host-test run for the app/runtime/storage slices reached
`kestrel.smoke_app host healthz returns configured body`, then left a
`dotnet MinimalApiSmoke.dll` process alive with the redirected log handle open.
That blocked the test harness from reaching later markers. The host fixture is
not guest Kestrel evidence, but it must be deterministic so it does not mask
kernel/storage/runtime regressions.

## Files changed

- `tests/OS.HostTests/KestrelSmokePackageContractTests.cs`
  - Replaced the fixture HTTP client path with a bounded raw TCP HTTP/1.1 GET
    helper using `Connection: close`.
  - Added response-header parsing, content-length handling, and chunked-body
    decoding for Kestrel JSON responses.
  - Added bounded stdout/stderr task waits during fixture shutdown so process
    cleanup cannot hang indefinitely after a kill.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build *> .\artifacts\logs\host-tests-integrated-602-605.log`

## Evidence

- Host-test build passed with 0 warnings and 0 errors.
- Integrated host-test run exited 0 and emitted `TEST_PASS host.tests`.
- Kestrel host-fixture markers from the integrated run:
  - `TEST_PASS kestrel.smoke_app host fixture publishes`
  - `TEST_PASS kestrel.smoke_app host healthz returns configured body`
  - `TEST_PASS kestrel.smoke_app host config exposes contract metadata`
  - `TEST_PASS kestrel.smoke_app host env override controls body`

## Failures

- Before this fix, the full host-test run hung with an orphaned
  `dotnet MinimalApiSmoke.dll` process after the health-route test started.
- The first raw HTTP implementation handled `Content-Length` responses but not
  Kestrel's chunked JSON response for `/config`; the host test then failed with
  `JsonReaderException` because the raw chunk framing was passed to the JSON
  parser. The final implementation decodes chunked bodies before parsing.

## Deferred work

- Keep this evidence scoped to host fixture determinism.
- Do not treat host Kestrel fixture success as in-guest hosted .NET, Kestrel, or
  cloud workload evidence.
- Future hosted-tier work must still prove Kestrel through the guest network
  path with the markers defined in `docs/completion-gates.md`.

## Evidence boundary

This is host-only fixture reliability evidence. It does not prove live guest
Kestrel, hosted .NET 10, PAL sockets, guest TCP listeners, cloud-provider
networking, or production readiness.
