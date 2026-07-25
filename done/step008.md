# Step 008 - Source Firewall Guard

## What Changed

- Extended `scripts/check-naming.ps1` with a source firewall while preserving the existing naming guard.
- Added `-Root <path>` support so host tests can run the guard against temporary fixtures without dirtying the repository.
- Added first-party source-language checks:
  - reject `.c`
  - reject `.cpp`
  - reject `.h`
  - reject `.asm`
  - reject `.s`
- Scoped forbidden source checks to first-party core/std/app roots:
  - `src/`
  - `std/`
  - `apps/`
- Kept `references/`, `artifacts/`, `bin/`, `obj/`, `.git/`, and `.vs/` excluded from the source file scan.
- Added build-boundary checks:
  - reject `references/` project paths in `OS.slnx`;
  - reject `references/` project paths from first-party `src/`, `std/`, and `apps/` project files.
- Added machine-readable markers:
  - `TEST_START source.firewall`
  - `TEST_PASS source.firewall`
  - `TEST_FAIL source.firewall <path>: <reason>`
- Added host tests in `tests/OS.HostTests/SourceFirewallTests.cs`.
- Updated security, license-audit, reference-map, repo-audit, and completion-gates docs.

## Why It Changed

Reference repositories are allowed as implementation material, but they must not silently become first-party build inputs. The OS core/kernel/std/app source invariant also forbids committed C, C++, headers, assembly, and raw assembler files in first-party source roots. The guard makes both rules testable before larger reference adaptations begin.

## Files Changed

- `scripts/check-naming.ps1`
- `tests/OS.HostTests/SourceFirewallTests.cs`
- `docs/security.md`
- `docs/license-audit.md`
- `docs/reference-map.md`
- `docs/repo-audit.md`
- `docs/completion-gates.md`

## Tests Run

```powershell
.\scripts\check-naming.ps1
dotnet build .\OS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
git diff --check
```

## Logs and Evidence

- `.\scripts\check-naming.ps1` emitted:
  - `TEST_PASS naming.guard`
  - `TEST_PASS source.firewall`
- Host tests emitted `TEST_PASS host.tests`.
- New host markers passed:
  - `TEST_PASS source firewall accepts current tree`
  - `TEST_PASS source firewall rejects forbidden first-party source`
  - `TEST_PASS source firewall ignores reference source files`
  - `TEST_PASS source firewall rejects references in solution`
  - `TEST_PASS source firewall rejects references in first-party project`
- Managed build passed with 0 warnings and 0 errors.
- `git diff --check` exited 0. It printed line-ending normalization warnings only.

## Failures

No failures remain for this slice.

## Deferred Work

- Add CI wiring so the guard runs on every build.
- Add a formal allowlist mechanism only when a deliberate future host-tooling integration needs it.
- Add full license/provenance metadata and SBOM generation separately.

## Evidence Boundary

This step proves source-language and reference-build-boundary guard behavior. It does not prove full license compliance automation, SBOM generation, CI execution, runtime safety, hosted .NET 10, ASP.NET Minimal API, or Kestrel support.
