# Step 936 - Terminal page-fault transition host emitter

## Summary

Integrate the independently reviewed #513 host leaf. Prepare bounded context,
emit and read back the terminal x64 fault-transition stream as ordinary data.
Keep #54 open for the separate disabled EFI adapter and actual fault evidence.

## Why

The #510 owned diagnostics page image does not supply executable transition or
capture code. This leaf provides the fixed byte contract and independent host
oracles before any privileged integration. It adds no execution API.

## Files changed

- `src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultTransition.cs`:
  status-coded prepare/emit/readback with fixed C3/D10/T43 bounds.
- `tests/OS.HostTests/SeparateDisabledDefaultPageFaultTransitionTests.cs`:
  literal context/field/transcript fixtures, independent decoding and synthetic
  machine, corruption/preflight/canary/zero-allocation checks.
- `docs/diagnostics.md` and `docs/completion-gates.md`: supported host contract
  and outstanding activation/fault boundaries.
- This step, reserved with `scripts/new-done-step.ps1 -Reserve`.

No existing test, EFI source, project, script, runtime shim, BootSequence or
Probes changes belong to this leaf.

## Tests run

- Author focused tests: actual exit 0, one start, two passes including terminal,
  zero failures and empty stderr. HostTests Release compilation and the actual
  no-stdlib closure each report zero warnings/errors. No full harness run yet.
- Independent reviewer read both complete files and reused contracts, verified
  all 37 manifest records, and decoded the preserved stream with LLVM. All 361
  instruction boundaries and 1910 raw instruction bytes match across 21 blocks.
  The reviewed 17-source closure DLL has zero AssemblyReferences.
- Parent copied only the frozen source/test pair and verified raw hashes.
  Fresh actual-source no-stdlib Release build: exit 0, zero warnings/errors,
  1.09 seconds. Fresh focused actual-kernel-reference Release build: exit 0,
  zero warnings/errors, 7.86 seconds. Build/test stderr files are empty.
- Fresh parent focused module: one start, two passes, zero failures, one exact
  `TEST_PASS m02.014.transition.focused`, actual exit 0. Its separate ignored
  packaging harness independently inspects the actual no-stdlib DLL and reports
  zero AssemblyReferences. Synthetic execution reports 33243 modeled steps,
  2053 serial polls, 57 fields and a 12-line/2053-byte transcript.
- Tests cover all 4096 context-byte and 8192 stream-byte corruptions, unchanged
  rejected buffers/canaries, high/low/boundary addresses, independent exact
  six-range image coverage, full-width relocation, early CR2/GPR capture,
  descriptor/root/mode/frame/walk/serial negatives and warmed zero allocations.
- Parent `git diff --check` and staged new-file whitespace check pass across
  all five paths. Complete settled-wave acceptance is pending.

## Evidence

Author worktree: `C:/Users/Roger/.codex/worktrees/m02-014-transition/CLROS`,
base `b193f590c30e95dc1f7b3b156aafd13722889f65`. Original attempts and independent
LLVM/metadata review are retained under its `artifacts/issue513` directory.
Parent report copies are under
`artifacts/orchestration-20261001/issue513-host-handoff`; fresh parent logs are
`issue513-root-nostdlib-build.*`, `issue513-root-focused-build.*`,
`issue513-root-focused-test.*` and `issue513-root-focused-result.json`.
Fresh parent compiler output and synthetic audit are under
`artifacts/issue513/nostdlib` and `artifacts/issue513/audit`.

| Item | SHA-256 |
| --- | --- |
| Production source | 5A21080FAA46414A30740C8EA5F972608333D10973F6988CEB1EC39F50508373 |
| Tests | 463DB1A1108FB4A62917C6FB4408C6C08B21301B9ED62E94FAF62FD8AE4DAA8E |
| Author report | 644022227B552F8C03EA2E848A05E73A2629FF9BF0D0F10600FA3B6477801EA5 |
| Author evidence manifest | 058BACACFB9751A5C2943426F78FD9E48EBF393BE4156BFE0ACB67D1ED2D8D17 |
| Independent review | 0539FC22E89A86CE382B7BB552D99661EA917DCB94AE1FE9026B7ED924D66533 |
| Parent no-stdlib build stdout | 69B66041331D20C8D255E908606CCCF25078174887FE66E93F6E91E0A7EC3981 |
| Parent focused build stdout | 47D9A3E4692216FE8A79E3015BC6FADC08932DF56AEDC91C4D18BACE42BD245C |
| Parent focused test stdout | 32F221C2BABF01353B24F97CDCF7AA33383D5B01D0B5B06BAE73805C77071003 |

## Failures

No fresh parent build/test failure occurred. Preserved author development
failures include scratch/vector backing alignment, template sentinel collision,
missing decoder PUSH-imm32 support, unsupported no-stdlib string operations,
duplicate harness inclusion and a synthetic TSS-read phase assertion. The
settled implementation corrects these; earlier 359-instruction/1898-byte output
is superseded, not reused as current evidence. Independent metadata inspection
also preserved an initial extension-method invocation error before correction.

## Deferred work

Run the complete settled-wave host gate after all reviewed source integrations.
Merge #513 before releasing the #54 shared EFI adapter. That adapter must use
the frozen API/offset mapping, freshly audit the exact six derived ranges and
current C/D/T backing, publish under owned policy and collect real default and
terminal vector-14 QEMU evidence. No issue closes from this focused step alone.

## Evidence boundary

The host never calls emitted instruction pointers. Modeled descriptor/CR3
changes, frame capture, page absence and serial output are synthetic evidence.
There is no live fault, installed-root permission/TLB proof, guest execution or
post-firmware lifetime claim. Trusted CPL0 entry remains required; synthetic
bad-CPL diagnostics do not establish safe ring-3 recovery. The 3264-byte context
bound includes the output slab; the actual 2053-byte transcript ends at offset
3205. Snapshots/fingerprints are not ownership, lifetime, concurrency or
tamper-proof authority. One modeled fault does not cover arbitrary nested
faults or inaccessible diagnostics. Gates 3 and 4 remain Fail.
