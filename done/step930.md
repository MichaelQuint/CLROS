# Step 930 - Bounded diagnostics page-table image for M02-014

## Summary

Supplemental #510 adds a host-only builder and independent auditor for the
supplied four-level page-table image needed by a later terminal null-read
adapter. It supports at most eight disjoint low-canonical identity ranges,
each one to sixteen 4 KiB pages with supervisor RX or RW+NX permissions, plus
a separately owned RW+NX table arena. It adds no EFI inclusion or boot phase.

## Why

The accepted M01 interval restores firmware CR3; the accepted terminal UD2
profile does not install an owned root. Neither proves that page zero is
absent during a later null read. This prerequisite supplies a bounded prepared
image while keeping live root/mode/TLB/frame observations separate.

## Files changed

- `src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultPageTables.cs`
- `tests/OS.HostTests/SeparateDisabledDefaultPageFaultPageTablesTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- This centrally reserved step.

Parent integration copied the two independently reviewed author files with
exact SHA256 verification. No existing source, EFI project, script, probe,
shared memory helper or backlog dependency changed for this slice.

## Tests run

Author worktree: `C:\Users\Roger\.codex\worktrees\m02-014-host\CLROS`.

- Focused Release harness: passed `m02.014.page_image` and its focused
  envelope. Eight test groups independently inspect raw hierarchy bits and
  exact coverage, cross hierarchy boundaries, exercise maximum layouts,
  reject corrupt metadata/entries and pointer aliases, preserve output
  canaries and rejected inputs, and measure zero allocation over 64 warmed
  combined build/audit iterations.
- Actual production source plus ten existing dependencies compiled through
  the ignored no-stdlib harness: zero warnings/errors and zero assembly
  references, independently inspected with PE/metadata readers.
- HostTests build: zero warnings/errors. No full suite ran in that worktree.
- Parent focused Release rerun passed from the integrated root using
  `dotnet run --project artifacts/orchestration-20261001/page-image-focused/Focused.csproj -c Release`.
- Whitespace checks passed, including both new files. Independent source
  review found no remaining actionable findings. All ten no-stdlib dependency
  files are unchanged between the author base and parent merge baseline.
- Independent parent-integration review accepted the unchanged source hashes,
  focused evidence, two documentation updates and this step without findings.

The parent-coordinated complete gate for this new wave is still pending.
The previous 3,517-pass gate at `07da84e` did not include #510 and is not
claimed as coverage for this change. No EFI publish or QEMU test was run.

## Evidence

The two source identities are:

| File | SHA256 |
| --- | --- |
| Production contract | C03FF7F0D1B9BE2EC5A17CDC01057D8AB10C7C097786797B0DFA1BB1EA016FBB |
| Host tests | B310BBE0F106909A61F35E9FE6DE18A2604149A63C6C8D1BCCB39B6A4A91ADB4 |

Author logs and projects are under `artifacts/m02-014/` in the assigned
worktree. The parent copies the evidence report to
`artifacts/orchestration-20261001/page-image-evidence.md`. Focused stdout
(both author and parent) SHA256:
`9F2215A6042EFC4AAFC28705CCE0F7AFF6D768A4354D889B817A9C2DF7B3CF4D`.
No-stdlib build log:
`FBF81DD90777486180B249DCF53889C98FC4E6702A0ADCC3EF00B4F7E50A25F8`.
No-stdlib DLL:
`84582ABD780C8EA2743957CECC12448E620DC9ECC36A49BE0C02A738B812DC19`.
HostTests build log:
`CDC7E7AC08CB05755761F4523CF7F03A17A2F2E1CD0920D0F242F77D541432C7`.
Author stderr logs are empty. The distinct marker is:

```text
CLROS_M02_014_PAGE_IMAGE_HOST_READY maxRanges=8 maxRangePages=16 maxTablePages=55 suppliedImageAudited=true guestExecution=false cr3Installed=false activePermissionsObserved=false tlbInvalidated=false faultTriggered=false
```

The conservative table bound is `1 + 6 * (rangeCount + 1)`, at most 55 pages.
Every resource interval is at most 64 KiB; the arena is at most 220 KiB.
Each is shorter than 2 MiB and touches at most two regions at each of the
2 MiB, 1 GiB and 512 GiB hierarchy levels. Thus one root plus six nonroot
tables per interval, including the arena, suffices. The API checks this bound
before writing, even when a particular layout could fit in a smaller arena.

## Failures

Development corrected an incorrect existing typed-address method name and
refined missing-versus-surplus hierarchy statuses. Tests were corrected where
the existing entry decoder already rejects W+X or zero physical targets as
malformed entries. Independent raw negatives still exercise a valid nonzero
target mapped at virtual zero and aligned huge-page zero mappings. Final
focused/build checks pass; no known code-review finding remains.

## Deferred work

Complete the settled next-wave host gate, commit/merge with informative
descriptions, and post/read back #510 evidence
before closing it. Keep #54 open for the separately owned real null-read
adapter and fresh guest proof. The proposed terminal resource layout remains
design material and is not implemented by this host prerequisite.

## Evidence boundary

The result audits a supplied pristine image, with accessed/dirty bits clear.
It is a non-owning snapshot, requiring a new audit after storage or lifetime
changes, and grants no authority to load CR3. It does not prove active page
permissions, TLB invalidation, guard enforcement, descriptor installation,
hardware fault capture, recovery, firmware exit or any completion gate.
