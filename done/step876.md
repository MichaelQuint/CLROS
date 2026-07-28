# Step 876 - M01-031 owned physical page-table arena

## Summary

- Completed Gitea issue `M01-031`'s host-only implementation boundary.
- Added an unmanaged fixed-point planner that accepts one caller-selected,
  page-aligned physical candidate inside a validated conventional-memory
  descriptor and returns the exact table-pages-plus-one-state-page
  `LoaderData` request.
- Kept physical identity and CPU dereferenceability separate: the materializer
  receives an explicit caller-owned backing pointer and never casts the
  candidate physical address into a host pointer.
- Retained the four fixed live-interval requirements in the arena plan, rebuilt
  the complete seven-requirement identity plan in bounded stack storage during
  materialization, and required exact equality for every nested planning field.
- Added a pre-write four-level hierarchy audit that reconstructs each virtual
  page and requires an exact 4 KiB identity leaf, strict entry encoding,
  expected protection/cache policy, unique normalized-range ownership, and
  exact per-range page cardinality.
- Added exact ordered table-page emission and independent per-entry readback,
  deterministic diagnostic fingerprints, exact `LoaderData` allocation-record
  equality including UEFI attributes, and a disjoint caller-owned 4096-byte
  scratch snapshot for byte-for-byte state/canary preservation.
- Added positive and hostile host vectors for fixed-point boundaries,
  capacity, pointer arithmetic, aliases, malformed plans/images/entries,
  hierarchy reachability, semantic leaf corruption, readback corruption,
  source immutability, state preservation, and capacity-tail preservation.
- Updated the canonical boot and architecture documents without changing a
  completion-gate status.

## Why

M01-030 established the exact identity ranges required while the future owned
root is active, but its prepared page-table image still lived in flat metadata
and entry workspaces. It did not solve the circular relationship in which the
chosen physical arena base participates in the identity map that determines
the table-page count, nor did it copy and independently verify the prepared
table bytes at physical-arena offsets. M01-031 provides that bounded,
failure-atomic host contract while deliberately leaving firmware selection and
allocation, refreshed-map reconciliation, CR3 installation, and privileged
execution to later serialized issues.

## Files changed

- `src/OS.Kernel/Boot/M01LivePagingIntegration.cs`
- `tests/OS.HostTests/M01LivePagingIntegrationTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step876.md`

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Passed with exit code 0.
  - Emitted exact aggregate lines `TEST_START m01.031`,
    `CLROS_M01_031_READY`, and `TEST_PASS m01.031` once each.
  - Emitted final `TEST_PASS host.tests` once.
  - Emitted no line beginning with `TEST_FAIL`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1`
  - Passed `naming.guard` and `source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Passed `qemu.runner.dryrun`; this validates tooling only and is not boot
    evidence.
- `git diff --check`
  - Passed.

## Evidence

- Authoritative full-harness standard output:
  `artifacts/m01031-evidence-20260727-234038-400/host.out.log`
  - Size: 413,657 bytes.
  - SHA-256:
    `3519AC43B03CFBD6A71BEE287E9C2FFEF2706D9374172A56BC717B8CDD8F0562`.
- Authoritative full-harness standard error:
  `artifacts/m01031-evidence-20260727-234038-400/host.err.log`
  - Size: 0 bytes.
  - SHA-256:
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Marker counts from the authoritative output:
  - Exact `TEST_START m01.031`: 1.
  - Exact `CLROS_M01_031_READY`: 1.
  - Exact `TEST_PASS m01.031`: 1.
  - Any `TEST_FAIL m01.031` prefix: 0.
  - Exact `TEST_PASS host.tests`: 1.
  - Any `TEST_FAIL` prefix: 0.
- The planner's independent boundary vectors prove:
  - Ordinary single-PT convergence: four table pages plus one state page.
  - 2 MiB crossing: five table pages plus one state page.
  - 1 GiB crossing: six table pages plus one state page.
  - 512 GiB crossing: seven table pages plus one state page.
  - A 48-bit address outside four-level low-canonical identity space is
    rejected as `NonCanonicalIdentityRange`.
  - A low-canonical arena crossing a narrowed 36-bit physical limit is rejected
    as `PhysicalWidthExceeded`.
- An independent read-only review initially found two production trust-boundary
  defects and three acceptance gaps. After correction, the repeat review found
  no actionable production, security, test-coverage, no-stdlib, or issue-scope
  finding. Both `OS.Kernel` and `OS.HostTests` compiled with zero warnings and
  errors during that review.
- The only reference influence was the test-only hierarchy-boundary cross-check
  at
  `references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs`
  (SHA-256
  `35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`).
  The supplied archive has no recoverable Git revision. No SharpOS source,
  allocator behavior, permanent-map assumption, or algorithm was copied or
  materially adapted.
- The accepted evidence is host-only. It proves deterministic arena geometry,
  exact capacity/allocation semantics, semantic hierarchy auditing, offset
  materialization, independent readback, and state/canary preservation in
  caller-owned backing. It is not firmware, guest, QEMU, or completion-gate
  evidence.

## Failures

- The first long host attempt used a 180-second wrapper, exceeded that wrapper,
  and left its owned `OS.HostTests` child process (PID 20456) running. Only that
  verified orphan was stopped. A concurrent build failed solely because that
  process still held the output assemblies; no unrelated process or source
  change was involved.
- The first independent review rejected the initial implementation because a
  structurally valid but semantically wrong leaf could pass, nested plan
  evidence was only partially checked, allocation attributes were omitted from
  exact equality, and state preservation relied on a diagnostic FNV value.
  The final implementation rederives the complete plan, validates exact leaf
  semantics before writes, includes attributes, and compares all 4096 state
  bytes through disjoint scratch storage.
- The first authoritative post-review run,
  `artifacts/m01031-evidence-20260727-232253-495/`, reached M01-031 but exposed
  an opaque planner-negative assertion mismatch. Its otherwise passing
  materialization groups were not accepted as issue evidence.
- A diagnostic rerun,
  `artifacts/m01031-evidence-20260727-233246-735/`, identified the stale vector:
  a candidate near the fixture's 48-bit physical limit leaves four-level
  low-canonical identity space first, so `NonCanonicalIdentityRange` correctly
  preceded physical-width rejection. The test now preserves that case and uses
  a separate 36-bit, low-canonical vector for genuine physical-width overflow.
- The local Gitea assignee-specific endpoint returned HTTP 404 while claiming
  the issue. Updating the issue with the supported general issue endpoint
  assigned `codex` successfully; no credential was printed or persisted.

## Deferred work

- `M01-032` must add the audited composite call gate and post-return RFLAGS
  observer.
- The later M01-025 integration must select the candidate from the bounded
  firmware map, request this exact base/page count with checked
  `AllocateAddress` semantics, recapture and reconcile the memory map and
  reservation ledger, materialize through the live backing, and invoke the
  composite only after all serialized prerequisites pass.
- No firmware allocator was called; no page was selected or allocated from a
  live EFI map; no refreshed allocation record was observed; no CR3, PTE, TLB,
  page protection, RFLAGS, or generated-code effect occurred.
- No real QEMU/OVMF run was required or claimed, and
  `docs/completion-gates.md` remains unchanged.
