# Step 659 - Tool props inherit workload profile defaults

## Summary

Fixed the tools-local MSBuild props boundary so `dotnet build .\OS.slnx` keeps
the root default `OSWorkloadProfile=SingleMinimalApi` for host tools, then
refreshed the README status snapshot.

## Why

The final wrap-up build showed that `tools/Directory.Build.props` shadowed the
root `Directory.Build.props`. Tool projects therefore reached
`Directory.Build.targets` with an empty `OSWorkloadProfile` and failed the
plain solution build. Host tools need their local artifact-exclusion props
without losing root build defaults.

## Files changed

- `tools/Directory.Build.props`
  - Imports the root `Directory.Build.props` before adding tool-local artifact
    exclusions.
- `README.md`
  - Refreshed the current host-tested status snapshot for diagnostics,
    `std/no-runtime`, PAL, storage, networking, and tooling evidence.
- `done/step659.md`
  - Records the build blocker and evidence boundary.

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence

- Before the fix, the plain solution build failed for tool projects with:
  `Invalid OSWorkloadProfile ''. Valid values: SingleMinimalApi, MultiMinimalApi.`
- After importing the root props from `tools/Directory.Build.props`, the same
  plain solution build completed successfully:
  - `OS.QemuRunner`
  - `OS.ManifestTool`
  - `OS.ImageBuilder`
  - `OS.LibvirtRunner`
  - `OS.HostTests`
  - `Build succeeded.`
  - `0 Warning(s)`
  - `0 Error(s)`
- `git diff --check` completed with no diff-check errors. Git emitted
  LF-to-CRLF normalization warnings for existing files.

## Failures

- The initial unqualified solution build failed before this fix. The rerun
  passed.

## Deferred work

- Preserve formal clean-checkout build logs when closing the clean-build gate.
- Continue from the existing QEMU/libvirt dry-run, marker preset, and baseline
  artifact-summary contracts.

