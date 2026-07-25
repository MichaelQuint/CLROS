# Step 227 - Kestrel Profile Completion Gates

## Summary

Split the hosted .NET 10 Minimal API/Kestrel completion gate by workload
profile.

## Why

Issue #251 needed the completion gates to reflect the new workload-profile ADR:
Kestrel is mandatory, but the near-term `SingleMinimalApi` profile can be
claimed with one packaged ASP.NET Minimal API/Kestrel workload and does not
require a process table, multi-app supervisor, or multi-app crash-isolation
proof. The later `MultiMinimalApi` profile requires those extra multi-app
runtime and isolation guarantees.

## Files changed

- `docs/completion-gates.md`
- `docs/kestrel.md`
- `docs/cloud-provider-requirements.md`

## Tests run

- `git diff --check`
- `dotnet build .\OS.slnx --nologo -m:1`

## Evidence

- `git diff --check` exited 0. It printed line-ending normalization warnings
  for modified Markdown files only.
- `dotnet build .\OS.slnx --nologo -m:1` passed with `Build succeeded.`,
  `0 Warning(s)`, and `0 Error(s)`.
- A follow-up edit updated `docs/cloud-provider-requirements.md` to map the
  provider traceability tracks to the explicit `SingleMinimalApi` and
  `MultiMinimalApi` profile names.

## Failures

None.

## Deferred work

- No Kestrel runtime, PAL export, socket implementation, guest storage,
  scheduler, hosted .NET runtime, external HTTP probe, process table,
  supervisor, or crash-isolation behavior was implemented.
- `SingleMinimalApi` and `MultiMinimalApi` remain failed gates until the
  required in-guest evidence exists.
- Gitea issue #251 was not closed from this documentation-only pass.
