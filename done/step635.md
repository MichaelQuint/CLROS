# Step 635 - Native manifest string-limit validation evidence

## Summary

Added host-test coverage for the kernel-facing native manifest parser's
existing bounded string fields. The new markers prove exact maximum accepted
lengths for `app.name`, `app.path`, `args`, environment key/value fields, and
listen host metadata, plus one-byte-over status-coded rejection for each
bounded field.

## Why

Issue #5 needs small, host-tested native-tier app-model slices before the
manifest can safely feed package selection and future app launch. The parser
already exposed deterministic limits and status codes, but the boundary cases
were not covered by machine-readable evidence. This step turns those limits
into repeatable validation evidence without changing app execution,
networking, diagnostics, storage, or launcher behavior.

## Files changed

- `tests/OS.HostTests/NativeManifestParserTests.cs`
  - Added `TEST_PASS manifest kernel accepts maximum string field lengths`.
  - Added `TEST_PASS manifest kernel rejects oversized string fields`.
- `docs/manifest.md`
  - Documented the exact parser byte limits and the one-byte-over rejection
    evidence.
- `docs/app-model.md`
  - Documented that the new evidence is parser validation only and not app
    execution evidence.
- `done/step635.md`
  - Recorded this evidence boundary.

## Tests run

- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:UseSharedCompilation=false -nr:false`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo --no-restore --no-dependencies /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build --no-restore /p:OSWorkloadProfile=SingleMinimalApi`
- `dotnet run --project .\artifacts\tmp\manifest-limit-probe\ManifestLimitProbe.csproj --nologo /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`

## Evidence

- The scoped no-dependency host-test build compiled the changed
  `OS.HostTests.dll` with 0 warnings and 0 errors.
- Focused manifest-limit probe log:
  `artifacts/logs/manifest-limit-probe-step635.log`.
- Focused manifest-limit probe markers:

```text
TEST_START manifest kernel accepts maximum string field lengths
TEST_PASS manifest kernel accepts maximum string field lengths
TEST_START manifest kernel rejects oversized string fields
TEST_PASS manifest kernel rejects oversized string fields
TEST_PASS manifest.limit.probe
```

## Failures

- The first host-test build failed before compiling tests because the current
  workspace requires an explicit workload profile:

```text
Invalid OSWorkloadProfile ''. Valid values: SingleMinimalApi, MultiMinimalApi.
```

- The profile-aware full dependency build is blocked by an unrelated dirty
  `tools/OS.LibvirtRunner` compile error:

```text
error CS1929: 'long' does not contain a definition for 'ToStringInvariant'
```

- The full host-test harness currently stops before the native manifest parser
  markers because unrelated libvirt diagnostics tests fail:

```text
TEST_FAIL libvirt diagnostics report real-run path readiness
TEST_FAIL libvirt diagnostics emit marker requirements
TEST_FAIL libvirt.runner.tests failures=2
```

The full harness log is
`artifacts/logs/host-tests-step635-manifest-validation.log`.

## Deferred work

- Fix the unrelated libvirt runner/test failures so the full host harness can
  run through the native manifest parser module-initializer markers again.
- Keep future manifest slices small: schema evolution, app ABI fields,
  restart policy, and multi-app profile semantics still need separate
  status-coded contracts and tests before any launcher claim.
