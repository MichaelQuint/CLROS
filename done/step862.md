# Step 862 - Page-zero policy and disabled null-fault plan

## Summary

Implemented M01-015 as two no-stdlib-compatible production contracts:

- `PageZeroMappingPolicy` validates raw mapping intents and performs a
  read-only index-zero walk over a caller-owned four-level x64 page-table
  image.
- `NullPageFaultProbePlan` records a default-off supervisor-null-read
  `#PF` intent without emitting bytes, exposing an invocation target, or
  dereferencing address zero.

Added the sole `m01.015` host aggregate, canonical architecture/boot/
diagnostics documentation, and retained this evidence record.

## Why

Rejecting one new map request does not prove that page zero is absent from an
existing hierarchy. Absence may terminate at PML4E, PDPTE, PDE, or PTE, while
a 1 GiB or 2 MiB parent leaf can map page zero without any PTE. This slice
therefore binds absence evidence to the complete supplied table image and
keeps actual fault execution deferred until owned diagnostics exist.

## Files changed

- `src/OS.Kernel/Memory/PageZeroMappingPolicy.cs`
- `src/OS.Kernel/Diagnostics/NullPageFaultProbePlan.cs`
- `tests/OS.HostTests/PageZeroMappingPolicyTests.cs`
- `docs/architecture.md`
- `docs/boot.md`
- `docs/diagnostics.md`
- `done/step862.md`

## Tests run

All commands were run from `C:\Projects\CLROS-M01-015` after the final review
changes:

```text
dotnet build .\CLROS.slnx --nologo -m:1
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
  exit 0; Build succeeded; 0 warnings; 0 errors

dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
  exit 0
  TEST_START m01.015
  CLROS_M01_015_READY
  TEST_PASS m01.015
  TEST_PASS host.tests
  no TEST_FAIL, OS_SMOKE_FAIL, or M01_015_CASE_FAIL marker

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
  exit 0
  TEST_PASS naming.guard
  TEST_PASS source.firewall

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun
  exit 0
  TEST_PASS workload.profile.tooling default single
  TEST_PASS qemu.runner.dryrun

git diff --check
git diff --cached --check
  exit 0; no whitespace errors

git status --short
  exactly the seven paths listed under Files changed were staged
```

The authoritative host-harness output was captured under
`C:\Users\Roger\AppData\Local\Temp\clros-m01-015-final-20260726-1627`;
`host.exit` contains `0`. The small PowerShell CLIXML progress record on
standard error is not a test failure. The QEMU command was a dry run only: it
validated command/marker planning and supplied no guest or boot evidence.

## Evidence

The host aggregate owns these stable markers:

```text
TEST_START m01.015
CLROS_M01_015_READY
TEST_PASS m01.015
```

It freezes append-only byte-enum values and unmanaged POD sizes, audits
canonical absence at all four paging levels, rejects present 1 GiB/2 MiB/
4 KiB mappings and malformed/non-present encodings, covers pointer/count/
capacity/alignment/span/alias failures, snapshots all caller-owned table
storage, validates the default-off vector-14/error-code-zero/CR2-zero intent,
and rejects default-on, missing-prerequisite, requested-execution, and
attempted-execution states.

The exact production source union is compiled with `NOSTDLIB_EFI`,
`NoStdLib=true`, no framework references, and zero assembly references.
Evaluated `SingleMinimalApi` and `MultiMinimalApi` EFI compile closures exclude
both new production files and their test. No EFI binary was inspected or
executed.

Reference influence:

- `references/SharpOS-main/OS/src/Kernel/Paging/PageFlags.cs`
  - local snapshot SHA-256:
    `DC565A4B8BFA38CDACFBDB5B53B2D5616BB9C1057DEDE7A1673956D6F3198652`
  - `reference:adapt` influence for the Present/NX semantic cross-check.
  - affected CLROS path:
    `src/OS.Kernel/Memory/PageZeroMappingPolicy.cs`.
- `references/SharpOS-main/OS/src/Kernel/Paging/PagingValidation.cs`
  - local snapshot SHA-256:
    `B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6`
  - `reference:test-only` influence for missing-map and malformed-entry
    categories.
  - affected CLROS path:
    `tests/OS.HostTests/PageZeroMappingPolicyTests.cs`.

The canonical upstream is `https://github.com/daniilvaino/SharpOS`.
Owner-supplied permission authorizes reuse of SharpOS-authored source, and the
upstream independently corroborates CC0-1.0 at license-evidence commit
`b80ee3a4175e1522d1a12ba7fe371a49da3207f5`. That commit is not asserted to be
the supplied local snapshot revision; the local revision is unknown, so the
exact paths and hashes above identify the reviewed source. No file-local
third-party term is overridden.

No reference implementation was copied. CLROS independently authored both
production contracts and their tests around its strict M01-011 decoder,
typed-address boundary, explicit caller-owned image, status results, and
pointer preflight.

## Failures

The first production build exposed C# ambiguity when comparing `sizeof`
(`int`) to frozen `ulong` layout constants. Explicit checked-domain casts
resolved all seven compiler errors before test implementation.

The first metadata hardening build used a nonexistent `MetadataReader`
collection property. It was replaced with the ECMA-335 `ModuleRef` table-row
query. Review then found that hidden nonzero bytes in the embedded default
intent could survive the success predicate; exact canonical-default
validation and byte-mutation tests closed that gap.

Two preliminary host-harness runs were stopped after their exact process trees
were inspected: one released a reviewer-held output lock, and one was
superseded when the final source/IL firewalls were strengthened. Neither run is
cited as evidence. The final single captured run above used the fully reviewed
source state and completed normally.

## Evidence boundary

- This proves absence only in the caller-owned snapshot supplied to the host
  audit. It does not prove the snapshot is current, installed in CR3, stable
  against another writer, or representative of another address space.
- Both evaluated EFI compile closures exclude the new sources and test. No EFI
  binary was inspected or executed, so this is not a binary-linkage claim.
- No null read, `#PF`, IDT dispatch, CR2/error-code capture, owned-COM1 panic
  output, QEMU run, or completion-gate transition occurs.
- M01-026 owns cumulative QEMU evidence for page-zero absence during the
  owned-root interval. M02-014 owns the separately disabled live null-read
  fault and captured register/error evidence.
- `Probes.cs`, `BootSequence.cs`, `EfiEntry.cs`, `OS.csproj`, `README.md`,
  and `docs/completion-gates.md` remain unchanged.
