# Step 972 - Retain the five ACPI bootstrap snapshots in owned storage

## Summary

Integrate #59's independently reviewed host composition for full RSDP,
selected XSDT or fallback RSDT, MCFG, MADT and HPET byte retention. The exact
two source files are imported after #58/#532 merged in PR535. #59 remains
open and claimed; #60 is still blocked on its native and guest acceptance.

## Why

Later boot work must retain the firmware data it needs before leaving firmware.
The new composition reuses the existing RSDP/SDT validators and first-match
table directory, checks unsigned physical spans and all source/scratch/output
aliases, then copies the five complete declared ranges into caller storage.
Five POD descriptors are published only on success. Every rejection preserves
the entire destination; parser scratch may change. Embedded firmware addresses
are preserved, not rewritten into owned pointers. Callers retain stable source
bytes during the operation and use returned offsets for later retained reads.

## Files changed

- `src/OS.Kernel/Boot/RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrap.cs`.
- `tests/OS.HostTests/RSDPSelectedRSDTXSDTMCFGMADTHPETBootstrapTests.cs`.
- `docs/boot.md` and `docs/efi-build.md`: host behavior and native evidence gap.
- `.gitea/backlog/clros-issues.json`: only M03-002.currentEvidence.
- `done/step972.md`, reserved with `scripts/new-done-step.ps1 -Reserve` at
  06:25:44.357995-06:25:44.632295 UTC on 2026-10-04, native exit0.

## Tests run

The isolated author worktree at merge `c3730ef839af14834e2381b74fda40c063e0e4a6`
linked all seven complete relevant initializers: RSDP validator/snapshot,
root snapshot, SDT parser, table directory, physical range guard and the new
five-table composition. The new initializer has eleven tests. The complete
focused batch at 06:15:17.765374-06:15:17.985244 UTC returned native exit0,
56 TEST_START /57 TEST_PASS /zero TEST_FAIL, one `TEST_PASS host.tests`, empty
stderr and exact expected order. Controls cover full-byte copies and offsets,
source mutation retention, root preferences, corrupt envelopes, missing tables,
physical range wrapping/duplicates/overlaps, exact and short capacities,
aliases including unused capacity, default/forged metadata and warmed
success/failure zero-allocation behavior.

Both builds used `dotnet build -c Release --nologo -m:1 --disable-build-servers
-p:UseSharedCompilation=false --artifacts-path <attempt2 isolated outputs>`.
The focused project build ran 06:15:10.643426-06:15:17.758458 UTC; the normal
`tests/OS.HostTests/OS.HostTests.csproj` build ran 06:15:17.994018-
06:15:42.466966 UTC. Both returned native exit0, zero warnings/errors and empty
stderr. The normal full executable was not run. All 903 source/config/harness
inputs stayed unchanged, as did three focused launch/end DLLs. Exact commands
are retained in the author final command plan and result.

The canonical import is byte-identical to that tested source. The combined
clean-head solution/full-host gate is pending for this integration wave.

After import, `dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo
-m:1` passed at 06:27:58.594050-06:28:18.315731 UTC, native exit0, zero
warnings/errors and empty stderr. Catalog offline validation passed at
06:27:37.067193-06:27:43.367114 UTC without API calls. `git diff --check`
passed. Recorder prefixes `issue59-root-host-build` and
`issue59-host-catalog-offline` preserve these commands and raw streams under
`artifacts/orchestration-20261001/issue531-final-transaction/`. Root build
stdout SHA256 is
`1F63F93B66A8697595AD6EB61604A1E16FCD0F1FDF93125F534755F1E997735F`.

## Evidence

Production SHA256:
`2AFB2B08C511AC8A25CE3F8818E7730961AB3F53C032CA6B846E0BB634624AE8`.
Test SHA256:
`C2F19BA26856E74EBA57B52BC8CAB6B94AF5349DEA7BCE79F82A7F7BD75C49C1`.
Focused stdout SHA256:
`6AD9E2F68097E1DBF466EB0908A4EE5755867771A1F0F8A909C8FE15CC5A762E`.

Author `artifacts/issue59/final/report.md` in the m03-owned-acpi worktree has
SHA256 `E62754CCFE8ADAF4BB5253EC5DAD98F6CD04C95CBEF8A9F09B3BA33BAD04460F`.
Its final result, command plan, 8-output manifest and 2,189 evidence records
retain both attempts. Independent root review
`artifacts/orchestration-20261001/issue59-source-independent-review/completed/review.md`
has SHA256 `F9349F480ED73B9094CB06D187C8911C629C9D2B5EEB964FD02A2A343CE429EC`;
no actionable finding remained. Parent read the complete code, tests, report,
ordered stdout and command plan, verified both final packages and exact source
identities, then imported them. `issue59-root-integration/import.json` records
that transfer and the sole catalog field change.

## Failures

The initial recorder setup selected the wrong ancestor directory and stopped
before launching a child. After correcting that ignored recorder, attempt1's
focused build failed CS1503 because a test `MemoryMarshal.Cast` selected a
read-only overload. The only fixture repair was passing `bytes.AsSpan()`;
production bytes stayed unchanged. No tests ran in the failed build. Attempt2
passed in full. Both failures and original inputs remain preserved.

## Deferred work

Resolve physical acquisition and the existing ReadOnlyMemory/table-directory
contract at the explicit no-stdlib boundary through a separately scoped native
integration. Then run actual firmware/QEMU observations, complete common
validation and review before closing #59 or unblocking #60. No real
ExitBootServices, post-firmware access, MADT/HPET device operation, hosted
.NET/Kestrel, cloud readiness or production support is claimed. No generated
artifact, first-party native source or secret is committed.
