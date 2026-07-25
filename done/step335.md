# Step 335 - Documentation Claim Boundary Consolidation

## Summary
Continued Gitea issue #320 by consolidating repeated host-only, Kestrel, provider, PAL, app-model, storage, boot, and QEMU claim-boundary prose across subsystem docs while preserving local warnings and linking broad readiness boundaries to the canonical gate documents.

## Why
The repo remains the canonical source for build/run commands, ABI contracts, completion gates, subsystem status, and evidence. Repeated negative-evidence lists had grown across subsystem docs; this slice keeps the local context that prevents false claims and redirects broad hosted-workload or provider-readiness scope to `docs/completion-gates.md` and `docs/kestrel.md`.

## Files changed
- `docs/app-model.md`
- `docs/boot.md`
- `docs/cloud-provider-requirements.md`
- `docs/cstorage.md`
- `docs/kestrel.md`
- `docs/manifest.md`
- `docs/pal.md`
- `docs/qemu.md`
- `docs/workload-profiles.md`
- `tests/OS.HostTests/WorkloadProfileTests.cs`
- `done/step335.md`

## Tests run
- `git diff --check -- docs\boot.md docs\qemu.md`
- `git diff --check -- docs\pal.md`
- `git diff --check -- .\docs\kestrel.md`
- `git diff --check -- .\docs\workload-profiles.md .\docs\cloud-provider-requirements.md`
- `git diff --check -- docs\app-model.md docs\manifest.md docs\cstorage.md`
- `rg -n "^## Claim Boundary And Non-Goals$|^## Provider Scope$|docs/completion-gates.md Gate 10" .\docs\kestrel.md`
- `dotnet build .\OS.slnx --nologo -m:1`
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
- `git diff --check`

## Evidence
- Boot and QEMU docs now use `docs/completion-gates.md` for the full evidence boundary and consolidate repeated `APP_*` readiness warnings.
- PAL docs now keep one PAL-local claim-boundary paragraph near the Kestrel-critical ABI section and trim repeated per-domain export/Kestrel tails to host-tested in-process wording.
- Kestrel docs now keep one `Claim Boundary And Non-Goals` section, rename provider wording to `Provider Scope`, and link repeated negative-evidence references back to Gate 10.
- Workload profile and provider docs now keep provider-specific false-claim warnings while linking detailed provider-scoped Kestrel evidence to `docs/completion-gates.md` and `docs/kestrel.md`.
- App-model, manifest, and storage docs now keep subsystem-specific missing behavior while linking broad hosted .NET/Kestrel/readiness claims to `docs/completion-gates.md`.
- Host docs-contract tests were realigned to the consolidated `Provider Scope` wording and the concise provider-gate language.
- Full host tests emitted `TEST_PASS docs.kestrel provider scope present`, `TEST_PASS workload profile single minimal api keeps provider gates distinct`, and `TEST_PASS host.tests`.

## Failures
- The first full host run after the documentation consolidation failed because `WorkloadProfileTests` still expected the pre-consolidation `Cloud Provider Claim Boundaries` heading and older provider-warning strings. The tests and workload-profile link were updated to match the new canonical wording.
- `git diff --check` commands reported only LF-to-CRLF warnings.
- Initial Gitea status had no credential environment variables, but Basic Auth credentials supplied later worked against issue #320.

## Deferred work
- Continue using `docs/documentation-map.md` before adding or moving docs.
- Issue #320 was closed after posting evidence. Reopen it or split a smaller follow-up if another duplicated documentation boundary is found.

