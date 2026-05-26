# Step 710 - Native Manifest Environment Diagnostic Formatter

## Summary
Added a caller-owned native manifest environment diagnostic formatter. The new
formatter writes the `NATIVE_MANIFEST_ENV status=` marker from an already-parsed
manifest, preserving environment count/capacity and first key/value source-range
metadata without creating process environment variables.

## Why
The native app model already carries parsed `env.KEY=value` metadata, but the
boot/app-launch path needed a compact diagnostic boundary before those values
can be projected into process or hosted-runtime configuration. This step makes
the metadata observable while keeping the evidence boundary explicit:
environment variables are not materialized, hosted .NET is not started, and
Kestrel is not started.

## Files changed
- `src/OS.Kernel/Manifest/NativeManifestEnvironmentDiagnosticFormatter.cs`
- `tests/OS.HostTests/NativeManifestParserTests.cs`
- `docs/manifest.md`
- `docs/app-model.md`
- `done/step710.md`

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- "manifest env diagnostic|host.tests|TEST_FAIL"`

## Evidence
- Focused host-test log: `artifacts\logs\hosttests-issue634-20260525.log`
- Build passed with 0 warnings and 0 errors after adding the missing local test
  byte-fill helper.
- Focused host-test command exited 0 and emitted:
  - `TEST_PASS manifest env diagnostic formats environment metadata`
  - `TEST_PASS manifest env diagnostic formats empty environment`
  - `TEST_PASS manifest env diagnostic reports short destination`
  - `TEST_PASS manifest env diagnostic rejects default result`
  - `TEST_PASS manifest env diagnostic rejects invalid source range`
  - `TEST_PASS host.tests`
- Marker shape:
  - `NATIVE_MANIFEST_ENV status=Success initialized=true environmentCount=<n> maxEnvironmentCount=16 firstKeyPresent=<true|false> firstKeyOffset=<offset|-1> firstKeyLength=<length|0> firstValueOffset=<offset|-1> firstValueLength=<length|0> processEnvironmentMaterialized=false hostedDotNetStarted=false kestrelStarted=false`

## Failures
- The first build failed because `NativeManifestParserTests` did not already
  have an `AllBytesEqual` helper for short-destination preservation checks.
  Adding the helper fixed the build; the same host-test project build then
  passed.

## Deferred work
- This does not read the manifest from live storage, create app-visible process
  environment variables, export PAL config/environment functions, start hosted
  .NET, start Kestrel, or wire a new BootSequence/QEMU marker.

