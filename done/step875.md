# Step 875 - M01-030 exact live-interval identity bindings

## Summary

- Completed Gitea issue `M01-030`'s host-only implementation boundary.
- Appended exact identity-map purposes for the composite code/call-gate page,
  owned stack, protection-probe page, complete table/state arena,
  runtime-state/canary page, and owned-root page without renumbering the
  historical public enum values.
- Added an exact preparation profile containing six mapped requirements plus
  one absent boot-stack-guard sentinel. The profile rejects the historical
  ACPI, framebuffer, serial, boot-handoff, allocator-arena, loaded-image
  continuation, and `AdditionalRequired` aliases as substitutes.
- Rebound composite preparation to six exact range roles with checked
  allocation, reservation, identity-map, cache, protection, and containment
  provenance. The exact post-call return address must remain in the
  non-writable RX code/call-gate page at or beyond the audited byte-plan tail.
- Preserved the historical planner entry point, enum values, runtime-state
  layout, composite byte length, instruction/semantic counts, and byte-plan
  hashes.
- Made the host fixture's durable caller-owned requirement, normalized-range,
  page-image, and page-entry arrays persistently pinned. A forced compacting GC
  now proves that the preparation/composite pointer bindings do not depend on
  accidental managed-array placement.
- Added comprehensive positive, reordered, default, missing, duplicate, extra,
  historical-alias, overflow, overlap, capacity, provenance, cache,
  protection, guard, W+X, call-gate, and failure-atomicity tests.
- Updated the canonical boot and architecture documents without changing any
  completion-gate status.

## Why

The M01-029 preparation contract still described the owned-root interval
through historical aliases and fabricated reservations for pages that the
composite does not dereference. In particular, ACPI scratch, framebuffer
metadata, a serial-stub alias, and a separate loaded-image continuation page
could not be populated truthfully by the current three-allocation boundary.
M01-030 replaces those aliases with a closed set derived from the composite
byte plan's actual memory accesses while keeping the older public values
append-only for other boot phases.

## Files changed

- `src/OS.Kernel/Boot/X64BootIdentityMappingPlan.cs`
- `src/OS.Kernel/Boot/M01LivePagingIntegration.cs`
- `src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs`
- `tests/OS.HostTests/X64BootIdentityMappingPlanTests.cs`
- `tests/OS.HostTests/M01LivePagingIntegrationTests.cs`
- `tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step875.md`

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Passed with exit code 0.
  - Emitted exact aggregate lines `TEST_START m01.030`,
    `CLROS_M01_030_READY`, and `TEST_PASS m01.030` once each.
  - Emitted final `TEST_PASS host.tests` once.
  - Emitted no `TEST_FAIL` line.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1`
  - Passed `naming.guard` and `source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Passed `qemu.runner.dryrun`; this is tooling validation only and is not
    boot evidence.
- `git diff --check`
  - Passed.

## Evidence

- Authoritative full-harness standard output:
  `artifacts/m01030-evidence-20260727-215811-040/host.out.log`
  - Size: 412,688 bytes.
  - SHA-256:
    `DC36E741E2ABFAE54759E49BB7B35F770A1CF64C98F665E38E3FAD3007D68D15`.
- Authoritative full-harness standard error:
  `artifacts/m01030-evidence-20260727-215811-040/host.err.log`
  - Size: 0 bytes.
  - SHA-256:
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- Marker counts from the authoritative output:
  - Exact `TEST_START m01.030`: 1.
  - Exact `CLROS_M01_030_READY`: 1.
  - Exact `TEST_PASS m01.030`: 1.
  - Any `TEST_FAIL m01.030` prefix: 0.
  - Exact `TEST_PASS host.tests`: 1.
  - Any `TEST_FAIL` prefix: 0.
- Two independent read-only reviews found no blocking production,
  test-coverage, documentation, marker-firewall, or forbidden-path issue.
- The only reference influence was the test-only page-permission cross-check
  at
  `references/SharpOS-main/OS/src/Kernel/Paging/X64PageTable.cs`
  (SHA-256
  `35503B543628E5A462073D498704050EEEF7D79DEB2B84F01CACA29714200BAC`).
  No SharpOS source or algorithm was copied or materially adapted.
- The successful evidence is host-only. It proves exact POD planning,
  provenance validation, byte-plan compatibility, and failure atomicity. It
  does not prove firmware allocation, physical table materialization, CR3
  installation, privileged instruction execution, hardware W^X/NX, owned
  diagnostics, or QEMU/OVMF behavior.

## Failures

- The first baseline harness attempt stopped before M01-030 because the
  separately owned M01-029 backlog evidence had lost the literal
  `caller-supplied` term required by the M01-028 ledger test. That one-line
  evidence-contract repair was isolated in pull request `#391`, merged as
  `865ba119cab7baa8644f0799f39271c7443b7fcd`; this branch was then
  fast-forwarded to the repaired base.
- A supplemental run using a custom `--artifacts-path` was invalid because it
  changed ImageBuilder output discovery and aborted with
  `Image builder host tests failed: 62` before M01-030 ran. Its logs are
  retained under
  `artifacts/m01030-focused-20260727-212520-992/` and are not cited as passing
  evidence.
- The first normal-layout attempt exposed movable managed arrays behind raw
  preparation addresses. It failed the retained M01-027 canary matrix with
  `InvalidPriorPlanningEvidence`. Persistently pinned fixture storage plus a
  forced compacting-GC regression test corrected the lifetime defect without
  relaxing production validation.
- The next normal-layout attempt reached M01-030 and exposed three assertion
  defects: pre-normalization missing-purpose masks were expected as zero,
  inherited stack reservations were expected to bypass the earlier canonical
  ledger validator, and the marker firewall counted its own
  `[ModuleInitializer]` search literal while broadly rejecting the honest
  `IsQemuEvidence = false` declaration. The assertions now model exact failure
  phase semantics and retain a stronger source firewall.
- A proposed zero-only rule for `BootMemoryRegionRecord.Attribute` was
  rejected during review because that field represents generic UEFI memory
  attributes and the existing allocation-evidence v1 contract does not make it
  a zero-only provenance field.

## Deferred work

- `M01-031` must materialize the planned entries into the owned physical
  page-table arena and independently read them back.
- `M01-032` must add the audited composite call gate and a post-return RFLAGS
  observer.
- The preparation state retains a preflight-observation address that the
  current composite neither compares nor dereferences. Any future live issue
  that consumes that pointer must give it stable unmanaged/pinned ownership or
  stop retaining it.
- No live page allocation, table installation, `mov cr3`, call-gate invocation,
  firmware transition, QEMU/OVMF execution, or completion-gate advancement is
  claimed by this step.
