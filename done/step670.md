# Step 670 - CI gate dry-run promotion guard

## Summary
Added a host-test guard that keeps Completion Gate 12 honest: local dry-run
and workflow-skeleton evidence cannot be documented as a passing CI gate unless
the repo records executed Gitea runner evidence.

## Why
Issue #588 requires the CI completion gate to remain failed until a compatible
Windows Gitea runner executes the workflow from a clean checkout and preserves
the expected logs/artifacts. Recent dry-run CI scaffolding is useful evidence,
but it is not runner execution evidence.

## Files changed
- `tests/OS.HostTests/CompletionGateTests.cs` - added a module-initialized host
  test that reads `docs/completion-gates.md` and asserts Gate 12 remains `Fail`
  with the runner-evidence requirement text.
- `README.md` - reflected the dry-run CI workflow scaffolding and completion-gate
  guard, and updated the conservative progress estimate from 22% to 23% without
  changing the CI claim boundary.
- `done/step670.md` - recorded this evidence slice.

## Tests run
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `dotnet build .\OS.slnx --nologo -m:1`
- `git diff --check`

## Evidence
- Focused host build succeeded with 0 warnings and 0 errors.
- Host tests emitted
  `TEST_PASS completion.gates ci remains failed without runner evidence`.
- Host tests emitted final `TEST_PASS host.tests`.
- Full solution build succeeded with 0 warnings and 0 errors.
- `git diff --check` passed with only LF-to-CRLF working-copy warnings.

## Failures
- None.

## Deferred work
- Run the dry-run CI workflow on a compatible Windows Gitea runner from a clean
  checkout and preserve runner logs/artifacts before changing Gate 12 away from
  `Fail`.
- Add real QEMU smoke execution to CI-compatible hosts after runner execution
  evidence exists.

## Evidence boundary
- This is host-test documentation-guard evidence only. It does not prove that
  Gitea CI has run, does not prove real QEMU smoke in CI, and does not change
  the CI completion gate status.

