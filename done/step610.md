# Step 610 - Host Tool Artifact Source Exclusion

## Summary

Added a central host-tool project boundary that excludes each tool-local
`artifacts/**` tree from SDK default source globs, then fixed libvirt CI smoke
profile propagation and Kestrel package publish profile propagation that were
exposed by the same verification path.

## Why

The combined verification pass exposed a real failure mode: generated scratch
C# files under `tools/OS.QemuRunner/artifacts/obj/...` were picked up by SDK
default compile items and produced `CS2001` build errors after the scratch files
were removed. Host tools commonly write verification artifacts, so the guard
belongs at the `tools/` project boundary instead of a single tool project.

The follow-up host-test run also exposed that `scripts/run-libvirt-smoke.ps1`
still invoked `dotnet run --project` without passing `OSWorkloadProfile`, which
caused repo-wide workload-profile validation to reject an empty profile during
CI smoke libvirt lanes.

The next integrated host-test pass reached the Kestrel/ImageBuilder package
round trip and exposed the same issue in
`apps/MinimalApiSmoke/publish-kestrel-smoke-package.ps1`: nested
`dotnet publish` and `dotnet run --project tools/OS.ManifestTool` invocations
did not pass `OSWorkloadProfile`, so `Directory.Build.targets` rejected the
empty profile before `OS.ManifestTool` could validate the package contract.

## Files changed

- `tools/Directory.Build.props`
- `tools/OS.QemuRunner/OS.QemuRunner.csproj`
- `tests/OS.HostTests/HostToolProjectBoundaryTests.cs`
- `scripts/run-libvirt-smoke.ps1`
- `scripts/run-ci-smoke.ps1`
- `apps/MinimalApiSmoke/publish-kestrel-smoke-package.ps1`
- `docs/qemu.md`
- `docs/libvirt.md`
- `docs/image-builder.md`
- `docs/kestrel.md`
- `docs/completion-gates.md`
- `tests/OS.HostTests/ImageBuilderTests.cs`
- `tests/OS.HostTests/KestrelSmokePackageContractTests.cs`
- `done/step610.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 /p:OSWorkloadProfile=SingleMinimalApi /p:UseSharedCompilation=false -nr:false`
- `.\scripts\run-libvirt-smoke.ps1 -DryRun -Image C:\Projects\OS\artifacts\tmp\ci-smoke-failure-repro\qemu-qcow2-intent.qcow2 -DiskFormat qcow2 -SerialLog C:\Projects\OS\artifacts\tmp\ci-smoke-failure-repro\libvirt-dryrun-serial.log -DomainXml C:\Projects\OS\artifacts\tmp\ci-smoke-failure-repro\libvirt-dryrun.xml`
- `.\scripts\run-ci-smoke.ps1 -LogRoot artifacts\tmp\ci-smoke-failure-repro2 -DotNet artifacts\tmp\ci-smoke-failure-repro2\dotnet-fails.cmd`
- `.\apps\MinimalApiSmoke\publish-kestrel-smoke-package.ps1 -OutputRoot artifacts\tmp\kestrel-profile-check -WorkloadProfile SingleMinimalApi -SkipNegativeValidation`
- `.\tests\OS.HostTests\bin\Debug\net10.0\OS.HostTests.exe *> .\artifacts\logs\host-tests-step610.log`
- `.\scripts\check-naming.ps1`
- `git diff --check`

## Evidence

- Solution build passed with 0 warnings and 0 errors after the central
  `tools/Directory.Build.props` exclusion was added.
- Host-test project build passed with 0 warnings and 0 errors after the tool
  project boundary tests were added.
- Libvirt dry-run now emits `WORKLOAD_PROFILE SingleMinimalApi`,
  `TEST_PASS workload.profile.tooling default single`, writes the XML artifact,
  and exits 0.
- CI-smoke failure-injection now reports only the intended two failures:
  `ci.smoke.build` and `ci.smoke.host.tests` exit 42, while
  `ci.smoke.libvirt.dryrun` and `ci.smoke.libvirt.missingtool` exit 0.
- The MinimalApiSmoke publish script now emits `WORKLOAD_PROFILE
  SingleMinimalApi`, validates that only the supported smoke profile is used,
  and passes `/p:OSWorkloadProfile=SingleMinimalApi` through its nested
  `dotnet publish` and `OS.ManifestTool` validation commands.
- Focused MinimalApiSmoke publish verification emitted `WORKLOAD_PROFILE
  SingleMinimalApi`, `TEST_PASS kestrel.smoke_app publishes package`,
  `TEST_PASS kestrel.package.contract`, and `TEST_PASS kestrel.smoke_app`.
- The integrated host-test executable wrote
  `artifacts/logs/host-tests-step610.log`, exited 0, and ended with
  `TEST_PASS host.tests`.
- The integrated host-test log includes the new host-tool and Kestrel package
  markers:

```text
TEST_PASS tools.project boundary excludes artifact sources
TEST_PASS tools.project boundary keeps four host tools
TEST_PASS imagebuilder.kestrel_package stages published package
TEST_PASS imagebuilder.kestrel_package readback preserves published files
TEST_PASS imagebuilder.kestrel_package readback preserves manifest json
TEST_PASS kestrel.smoke_app host fixture publishes
```

- `scripts/check-naming.ps1` emitted:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

- Host tool project-boundary tests were added for the next full host-test run:

```text
TEST_PASS tools.project boundary excludes artifact sources
TEST_PASS tools.project boundary keeps four host tools
```

- `git diff --check` passed with line-ending normalization warnings only.

## Failures

- Earlier verification failed with `CS2001` because stale compile inputs pointed
  at generated files under `tools/OS.QemuRunner/artifacts/obj/...`. The central
  guard prevents tool-local artifact trees from becoming source across all host
  tools.
- A first full host-test run after the project-boundary tests failed in
  `CiSmokeScriptTests` because the libvirt dry-run and missing-tool lanes
  failed repo-wide workload-profile validation. The libvirt script now accepts
  `-WorkloadProfile`, emits profile evidence, and passes the profile MSBuild
  property through nested `dotnet run --project` calls; the CI smoke script now
  forwards its selected profile into both libvirt lanes.
- A later full host-test run failed in the Kestrel/ImageBuilder tests because
  the MinimalApiSmoke publish script reached `OS.ManifestTool` through
  `dotnet run --project` without `/p:OSWorkloadProfile=SingleMinimalApi`. The
  script and the direct host fixture publish test now pass the profile property
  explicitly.
- An intermediate parallel verification attempt ran the solution build at the
  same time as the host-test project build and hit `CS2012` on
  `OS.HostTests.dll`; the sequential solution build passed with 0 warnings and
  0 errors.

## Deferred work

- This is host-tool build hygiene only. It does not add libvirt real boot,
  cloud-provider import validation, ISO support, guest storage/networking, or
  hosted .NET/Kestrel evidence.
- Real QEMU/libvirt boot, cloud-provider import, ISO generation, in-guest
  hosted .NET, live Kestrel, and cloud readiness remain unproven.

