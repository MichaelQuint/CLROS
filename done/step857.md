# Step 857 - Deterministic Boot-Page Allocator Stress Oracle

## Summary

Implemented Gitea issue `M01-010` as a host-test-only leaf over the existing
`BootPageAllocatorMutator.AllocateOnePage` and receipt-bound
`FreeOnePage` contracts.

The new test:

- Drives a bounded 128-page synthetic arena with a test-local xorshift64-star
  generator, fixed seeds, fixed operation counts, rejection-sampled bounded
  choices, and a documented 65-percent allocation / 35-percent free policy.
- Derives first-fit allocation, ownership, source provenance, free extents,
  totals, and host-owned content tags from an independent page model rather
  than from the allocator's mutated node table.
- Traverses and validates the allocator's active chain after every operation.
- Preserves and checks inactive logical slots, reservation bytes, declared
  capacity-tail canaries, and host-owned content storage.
- Requires byte-identical full storage snapshots after every expected failure
  and matching strict-LIFO exact-inverse free.
- Scriptedly covers whole, head, tail, and middle allocation shapes and the
  corresponding `None`, `Next`, `Previous`, and `PreviousAndNext` restoration
  classifications.
- Covers exhaustion, first/last synthetic pages, the highest valid one-page
  range, alignment overflow, default/empty states, malformed requests,
  metadata capacity, altered accounting/node metadata, replay, out-of-order
  receipt use, equal-accounting foreign receipt topology, and deliberate
  shadow ownership/content corruption.
- Evaluates the actual Release EFI `Compile`, `ProjectReference`, and
  `Reference` closures for both `SingleMinimalApi` and `MultiMinimalApi`, fails
  closed on malformed/empty evaluation output, requires canonical EFI source
  anchors, and proves the stress source and host-test project are absent.
- Proves the readiness marker remains unique to the owning host test and is
  absent from guest source, QEMU/libvirt tooling, scripts, workflows, and
  sibling host tests.

No production allocator, EFI entry, project, boot-sequence, diagnostics,
completion-gate, README, or reference file changed.

## Why

M01-008 and M01-009 established focused host evidence for one-page first-fit
metadata mutation and receipt-bound exact-inverse restoration. They did not
provide a long deterministic replay against an independently maintained
ownership/provenance/content oracle. This step supplies that regression
boundary before later paging and live guest work without changing production
behavior or promoting a completion gate.

## Files changed

- `tests/OS.HostTests/BootPageAllocatorStressTests.cs`
  - New self-registering host suite with twelve focused groups.
  - Canonical replay and three additional fixed-seed streams.
  - Independent page/provenance/content model and canonical SHA-256 transcript.
  - Evaluated EFI-closure and marker-boundary regressions.
- `docs/boot.md`
  - Records the arena, generator, operation policy, seeds, golden hashes,
    reference influences, markers, limitations, and evidence boundary.
- `docs/architecture.md`
  - Adds the compact host-test capability and preserves all unsupported/live
    boundary statements.
- `done/step857.md`
  - This evidence record.

Production source remained unchanged. In particular:

```text
src/OS.Kernel/Memory/BootPageAllocatorMutation.cs
SHA-256 403FCEB4154458BA6E2BBDA89EF73293D717D9F8CB406588DB279BA0117CCCD5
```

## Deterministic replay contract

Canonical replay:

```text
seed=0x6A09E667F3BCC909
generated_churn_steps=1024
transcript_records=1278
successful_allocations=549
successful_frees=549
expected_no_range_failures=178
sha256=12F1D263FC34D423E628574705A22F426BF43BACBAA38F53BEF886F5AB5D8D96
```

Additional 384-step streams:

```text
seed=0x3C6EF372FE94F82B stream=2 sha256=A74D2B2234A669DE8FDD7A478A3D12BF6601378E3557961B4EE1ADAAD20DB4D7
seed=0xA54FF53A5F1D36F1 stream=3 sha256=83AD88944AC8DB83A235E41A981F99EE02B652BC8DB2D596A0CF8AF585FC88B9
seed=0x510E527FADE682D1 stream=4 sha256=767149D6F2EEC88CDC524E96C5C2F2E2024B92B90E1D92A200C353E1EB1231C8
```

The canonical transcript uses explicit fixed-width little-endian fields and
excludes pointers, timestamps, process data, object hashes, unordered
enumeration, and culture-sensitive text. The reported record count includes
both the initial and final state records.

## Tests run

All commands ran from `C:\Projects\CLROS-M01-010` on 2026-07-26 with .NET SDK
`10.0.302`, branch `codex/M01-010-seeded-allocator-stress`, and base commit
`73150960c53bb282e0c8d5e40ffb3e07a05b8fee`.

### Solution and host-test builds

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
```

Result: exit `0`; build succeeded with `0` warnings and `0` errors.

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
```

Result: exit `0`; build succeeded with `0` warnings and `0` errors.

### Evaluated canonical EFI closures

Command shape, run for both profiles:

```powershell
dotnet msbuild .\src\OS\OS.csproj -nologo `
  -getItem:Compile,ProjectReference,Reference `
  -p:Configuration=Release `
  -p:OSWorkloadProfile=<profile> `
  -p:RestoreIgnoreFailedSources=true
```

Results:

```text
EFI_CLOSURE profile=SingleMinimalApi compile=13 projectReferences=0 references=0 stress=0 efiEntry=1 minimalRuntime=1
EFI_CLOSURE profile=MultiMinimalApi  compile=13 projectReferences=0 references=0 stress=0 efiEntry=1 minimalRuntime=1
```

Both commands exited `0`. The host suite independently executes and parses
these same evaluations with bounded process cleanup and stable failure reason
codes.

### Complete host harness

```powershell
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Authoritative post-review result: exit `0` in 256.2 seconds.

Captured ignored evidence files:

```text
artifacts/m01-010-evidence/host-tests.stdout.log
  bytes=775964
  lines=6076
  sha256=49DE55C8C7FF8CEDBC948AC5A80DFD0204C20CED8F6737FAF77B45213A7854CE

artifacts/m01-010-evidence/host-tests.stderr.log
  bytes=0
  sha256=E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
```

Marker counts:

```text
TEST_START m01.010                         count=1  line=472
TEST_START m01.010 <group>                 count=12
M01_010_REPLAY                             count=1  line=497
CLROS_M01_010_READY                        count=1  line=498
TEST_PASS m01.010                          count=1  line=499
M01_010_EFI_CLOSURE_FAIL                   count=0
TEST_FAIL m01.010                          count=0
TEST_FAIL <any>                            count=0
TEST_PASS host.tests                       count=1  line=6076
```

The preserved successful boundary is:

```text
M01_010_REPLAY seed=0x6A09E667F3BCC909 churn=1024 records=1278 allocations=549 frees=549 expected_failures=178 hash=12F1D263FC34D423E628574705A22F426BF43BACBAA38F53BEF886F5AB5D8D96
CLROS_M01_010_READY
TEST_PASS m01.010
```

The complete log terminates with:

```text
TEST_PASS host.tests
```

### QEMU dry run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-qemu-smoke.ps1 -DryRun
```

Result: exit `0`; emitted `TEST_PASS workload.profile.tooling default single`
and `TEST_PASS qemu.runner.dryrun`. This validates command/marker planning
only. It is not QEMU, OVMF, EFI, guest, or boot evidence.

### Naming and source firewall

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\check-naming.ps1
```

Result: exit `0` with:

```text
TEST_PASS naming.guard
TEST_PASS source.firewall
```

### Repository checks

```powershell
git diff --check
git status --short
```

`git diff --check` exited `0`; Git emitted only the configured LF-to-CRLF
working-copy notices. Before commit, `git status --short` contained exactly the
two owned documentation changes, the new owned host test, and this reserved
step:

```text
 M docs/architecture.md
 M docs/boot.md
?? done/step857.md
?? tests/OS.HostTests/BootPageAllocatorStressTests.cs
```

No generated artifact is tracked.

## Evidence

Final implementation hashes before commit:

```text
425C51E36CDA583E7093313D864E218F538D3169D62553935E84E11C61B50D9F  tests/OS.HostTests/BootPageAllocatorStressTests.cs
2249EDEB50DC1E5F244C4E1E8FC8A2CCC25679ABB20DC3ECB7CD60E776C2C522  docs/boot.md
1EA16BD5FC45B545D77A7B5757CDA463B85158BDD529B9FF8445435DAA206201  docs/architecture.md
```

Reference use was behavioral and test-only. No reference project was built,
linked, imported, or used as completion evidence, and no reference source was
copied. The local snapshots have no recoverable Git revision.

```text
22AF15CD46CBAEB28E4C8C1C18FF624C98B1EBFAA3ED0904D187263ACB2AFF7A  references/abanu-master/src/Abanu.Lib/Tests/BinaryBuddyAllocator_TestImplementation.cs
5FFC32601255779DA00A01503BD4DF08D0516C09424344A9FDB2ABF01D6443E5  references/abanu-master/src/Abanu.Test.Console/Alloc6Test.cs
EC216B54C61765DC83F7CD6945614C2D97BFBDA6D2925668B12C4239D2E2368B  references/nativeaot-patcher-main/src/Cosmos.Kernel.Core/Memory/PageAllocator.cs
```

The manifest-listed Abanu `BinaryBuddyAllocator_TestImplementation.cs` is a
split/merge cross-check, not the seeded content-oracle source. The actual
fixed-seed churn/content-check pattern is the sibling `Alloc6Test.cs`; this
step and `docs/boot.md` record that correction. The durable manifest correction
is deferred to the post-evidence backlog-maintenance/closure change because
`.gitea/backlog/clros-issues.json` is not an M01-010 implementation-owned path.

Three independent read-only audit passes reviewed:

- Model independence, PRNG/rejection sampling, transcript encoding, unsafe
  pointer use, failure statuses, and atomicity.
- Marker uniqueness/order, evaluated EFI closure, process timeout cleanup, and
  stable failure classifications.
- Documentation facts, reference provenance/hashes, and live-evidence
  boundaries.

Every actionable finding was corrected before the authoritative run.

## Failures and corrections

- A preliminary disposable focused runner was initially placed outside the
  repository. The shared repository-root locator correctly rejected that
  context. The ignored runner was moved beneath the repository for focused
  development evidence and its temporary source was removed afterward.
- A preliminary attempt to redirect all temporary project output with
  `BaseOutputPath` caused a circular `ResolveProjectReferences` diagnostic.
  That invocation was discarded; no production/project file changed.
- Independent review identified five possible false greens: a nonzero
  capacity-only slot, a foreign receipt rejected too early by accounting, an
  off-by-one published transcript record count, text-only EFI exclusion, and
  an incomplete marker-surface scan. The fixtures now use a default
  capacity-only slot plus canary, equal-accounting two-allocation foreign
  evidence, the correct 1,278 count, evaluated canonical closures, and an
  expanded marker/throw-order firewall.
- Follow-up review found that malformed or empty MSBuild JSON and process
  timeout/output failures needed stronger handling. The helper now requires a
  nonempty schema, exact canonical paths and two known EFI anchors, disables
  MSBuild node reuse, drains both streams together, requires bounded process
  termination, and emits stable profile-scoped reasons.
- One preliminary full-harness run was intentionally terminated after the last
  exceptional cleanup-path correction. The resulting `OS.HostTests.exe` and
  parent `dotnet.exe` processes were identified by exact command line and
  explicitly stopped. That partial log was overwritten and is not evidence.
- No product/test assertion failed in the final authoritative run.

## Deferred work

- The stress source remains host-only and is not linked into or executed by the
  canonical EFI artifact.
- The synthetic page model and content tags are caller-owned host storage. They
  prove oracle sensitivity, not physical page access, zeroing, preservation,
  mapping, or ownership.
- This step does not prove live firmware allocation, a populated EFI free list,
  guest allocation/free, QEMU/OVMF execution, paging, CR3, NX/W^X, DMA,
  interrupts, concurrency, or post-`ExitBootServices` behavior.
- Successful frees remain caller-serialized strict LIFO exact inverses. This
  step does not add arbitrary-order free, allocator identity, generation
  authority, unforgeable receipts, indistinguishable-copy rejection, or ABA
  safety.
- M01-024 owns the first explicit pre-EBS guest
  allocation/free/double-free-rejection cycle.
- No completion gate, CloudReady claim, Kestrel claim, hosted-.NET claim,
  provider claim, or production-readiness claim changes.

## Evidence boundary

This step proves only that the existing caller-owned boot-page allocator
metadata API survives the committed finite deterministic host corpus and
agrees after every operation with an independently maintained logical
ownership/provenance/content oracle. It also proves, through evaluated Release
project items, that this host stress source is absent from both current
canonical EFI workload-profile closures. The host readiness marker is not
guest evidence. All live physical-memory, EFI, QEMU, paging, DMA, concurrency,
cloud, and later completion-gate work remains unproven.
