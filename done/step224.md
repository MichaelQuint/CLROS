# Step 224 - Workload Profile ADR

## Summary

Added the workload profile ADR for issue #248. The new document defines
`SingleMinimalApi` as the near-term default profile and `MultiMinimalApi` as a
later multi-app profile that requires process, isolation, scheduler, and
supervisor semantics before it can be claimed.

## Why

Provider requirement notes describe a larger multi-app ASP.NET Minimal API
platform, while the OS product goal and current architecture keep the near-term
path to a single configured cloud workload. The profiles separate those tracks
so agents can work toward Kestrel without accidentally adding process-table or
per-process isolation requirements to the first hosted workload proof.

## Files changed

- `docs/workload-profiles.md`
- `docs/architecture.md`
- `docs/app-model.md`
- `docs/manifest.md`
- `docs/completion-gates.md`
- `done/step224.md`

## Tests run

- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence

- `dotnet build .\OS.slnx --nologo -m:1` completed successfully with
  `Build succeeded.`, `0 Warning(s)`, and `0 Error(s)`.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  completed successfully and emitted `TEST_PASS host.tests`.
- `git diff --check` returned exit code 0.
- Evidence boundary: verification ran in a shared worktree where other agents
  had driver and done-note changes present. This step claims only the
  workload-profile documentation files listed above.

## Failures

None. `git diff --check` emitted line-ending normalization warnings for some
Markdown files, including files outside this step, but returned exit code 0.

## Deferred work

- No runtime, manifest validator, app launcher, PAL, socket, storage, process,
  scheduler, supervisor, or Kestrel implementation changed in this step.
- `SingleMinimalApi` remains unclaimed until a guest image can start one
  Minimal API/Kestrel workload and pass an external `/healthz` probe through the
  OS network path.
- `MultiMinimalApi` remains blocked on process table, per-app address spaces,
  supervisor/restart policy, scheduler, per-app security, and crash-isolation
  work.
