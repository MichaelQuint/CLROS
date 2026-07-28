# Step 877 - M01-032 audited composite call gate

## Summary

- Completed Gitea issue `M01-032`'s host-only implementation boundary.
- Preserved the frozen 3,830-byte, 732-instruction composite prefix and added
  one exact 256-byte, 74-instruction gate in the tail of the same 4 KiB code
  page.
- Encoded one base-independent direct `call rel32` at page offset `0xFC4`.
  Its exact call-next return site is `0xFC9`, which matches the return address
  already required by the composite before its first privileged effect.
- Added a caller-owned 160-byte version-1 observation POD and a pointer-free
  776-byte result POD covering preparation, materialization, exact readback,
  and modeled returned-observation audit.
- Added a no-write/no-call guard path that validates state and observation
  pointers, packed headers, exact self addresses, arena containment, stack
  geometry, pristine counters/signature storage, and the RIP-derived return
  address before the first observation write.
- Preserved the Windows x64 ABI, 16-byte call alignment, 32-byte shadow space,
  nonvolatile GPRs, and XMM6-XMM15. The gate uses an exact 48-byte
  gate-entry-to-composite-entry stack delta.
- Restored the prepared full-RFLAGS snapshot immediately before transfer with
  the exact consecutive bytes for `push qword [RDX+48]; popfq; call rel32`.
  The first return-site instruction is `pushfq`, before any flag-changing
  post-return instruction.
- Added exact page materialization and independent readback, frozen FNV/SHA
  fingerprints, a complete independent 256-byte test decoder, hostile
  pointer/count/capacity/alias/overflow tests, failure-atomic storage checks,
  impossible gate-stack transcripts, and returned-observation corruption
  vectors.
- Updated the canonical boot and architecture documents without changing a
  completion-gate status.

## Why

The M01-027 composite could validate a caller-supplied expected return address
at `[rsp]`, but an ordinary unmanaged C# function-pointer call could not know
its compiler-generated call-next RIP in advance. The composite also restored
RFLAGS immediately before returning, but could not execute a post-`popfq`
readback without changing the restored state. M01-032 closes those two
host-contract gaps with a deterministic same-page gate whose direct call fixes
the exact return site and whose first post-return instruction captures RFLAGS.

The gate deliberately does not claim to be a trusted live caller. It cannot
self-validate full live entry RFLAGS before using the not-yet-validated stack,
and the pre-call `popfq` normalizes any mismatch. M01-025 must therefore prove
that live gate-entry RFLAGS already equal the prepared snapshot, keep state and
observation storage mapped and exclusively owned while retaining
gate/composite write access, prevent external or concurrent mutation of their
validated input/header fields, and establish the live protection and transfer
preconditions.

## Files changed

- `src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs`
- `tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs`
- `docs/boot.md`
- `docs/architecture.md`
- `done/step877.md`

## Tests run

- `dotnet build .\CLROS.slnx --nologo -m:1`
  - Passed with 0 warnings and 0 errors.
- `dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo`
  - Passed with 0 warnings and 0 errors.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
  - Passed with exit code 0.
  - Emitted exact aggregate lines `TEST_START m01.032`,
    `CLROS_M01_032_READY`, and `TEST_PASS m01.032` once each.
  - Emitted final `TEST_PASS host.tests` once.
  - Emitted no line beginning with `TEST_FAIL`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1`
  - Passed `naming.guard` and `source.firewall`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-qemu-smoke.ps1 -DryRun`
  - Passed `qemu.runner.dryrun`; this validates tooling only and is not boot
    evidence.
- `git diff --check`
  - Passed.
- `git status --short`
  - Reported only the four issue-owned files plus the newly reserved evidence
    step:
    - ` M docs/architecture.md`
    - ` M docs/boot.md`
    - ` M src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs`
    - ` M tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs`
    - `?? done/step877.md`

## Evidence

- Source binding before the implementation commit:
  - Base commit:
    `1a275aa81f3f1e99932b77fae0407f51aff57940`.
  - Base tree:
    `cee6d2b25fadf54087add0043f89cba9983ecc86`.
  - `src/OS.Kernel/Boot/M01OwnedCr3CompositeTransition.cs`:
    425,686 bytes, SHA-256
    `2AAFAAB40F88FDE40E1697B224D62150C5F6C68AB0610CBAEB7FCF81274C1DCD`.
  - `tests/OS.HostTests/M01OwnedCr3CompositeTransitionTests.cs`:
    445,742 bytes, SHA-256
    `69F34250FA0F63DA535AEC0C56146154D042FA8B0B5F502BD54737C553228B32`.
  - `docs/boot.md`: 274,067 bytes, SHA-256
    `9CB77D706E6AE737AE37837BB9231047471796A4E63603A4C60A9436F6C20AD4`.
  - `docs/architecture.md`: 67,960 bytes, SHA-256
    `02E7A1E946E7BA20C38CC188E24D8EDCE068B897F4C195462D6522129A8761A3`.
- Authoritative full-harness standard output:
  `artifacts/m01-032-authoritative-20260728-015918/host-tests.stdout.log`
  - Size: 414,463 bytes.
  - SHA-256:
    `B2044AA5DD53D0D2EE88EF462E239122A05E9B19E6B92EB63F7558B1A0201E32`.
- Authoritative full-harness standard error:
  `artifacts/m01-032-authoritative-20260728-015918/host-tests.stderr.log`
  - Size: 0 bytes.
  - SHA-256:
    `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.
- The two hashed harness logs are deliberately local ignored artifacts under
  `artifacts/`; they will not appear in a clean checkout. Their durable
  provenance, sizes, hashes, marker counts, source-file hashes, base commit,
  and command outcomes are preserved in this tracked step and are repeated in
  the reviewed Gitea evidence comment before issue closure.
- Marker counts from the authoritative output:
  - Exact `TEST_START m01.032`: 1.
  - Exact `CLROS_M01_032_READY`: 1.
  - Exact `TEST_PASS m01.032`: 1.
  - Any `TEST_FAIL m01.032` prefix: 0.
  - Exact `TEST_PASS host.tests`: 1.
  - Any `TEST_FAIL` prefix: 0.
- Frozen emitted-code evidence:
  - Composite prefix: 3,830 bytes, 732 instructions, FNV-1a64
    `F00446F61BE13CDD`.
  - Gate: 256 bytes, 74 instructions, FNV-1a64
    `F903C5DE16D3A5B2`, SHA-256
    `8B2965E741AC40B725B898D7B2BB2FFBDF7CC4C50DF9AEB163C17485270BD88A`.
  - Combined page: 806 instructions, FNV-1a64
    `42CAF3B4A6C2119A`, SHA-256
    `FA1B2EC2332CF70A5222B22B672758A1E948FD0F179FB91A16D7DD4280FD5631`.
  - Direct call: absolute page offset `0xFC4`, signed displacement
    `0xFFFFF037`, call-next/return offset `0xFC9`.
  - Final pre-call bytes:
    `FF 72 30 9D E8 37 F0 FF FF`.
- The independent decoder verifies every byte reaches an allowed decoder case,
  exactly one direct call targets the composite entry, the first return-site
  instruction is `pushfq`, exactly two guarded pre-call `FE /0` increments
  update lifecycle and call count, no instruction lies between `popfq` and the
  call, post-return count/lifecycle stores are flag-neutral, every instruction
  balances the ABI stack model, and the only successful return is the final
  byte at gate-local offset `0xFF`.
- The observation audit independently reconstructs gate-entry `RSP` from the
  composite transcript, checks `RSP mod 16 == 8`, 128 bytes of lower headroom,
  and 40 bytes of upper call-frame space, and cross-binds the
  composite-captured original RFLAGS to the prepared observation.
- Two independent post-fix reviews found no remaining P0, P1, or P2
  production, ABI, control-flow, stack, RFLAGS, no-stdlib, test-coverage,
  documentation, or issue-scope finding. An independent reflection-based
  emitter reproduced all byte lengths, instruction counts, offsets, hashes,
  and the final pre-call sequence.
- The selective reference influence was
  `references/SharpOS-main/bootasm/BootAsm.Generator/BootAsmGenerator.cs`
  (SHA-256
  `B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500`).
  The owner grant and current upstream CC0-1.0 evidence permit adaptation, but
  the supplied archive has no recoverable Git revision. The CLROS encoder,
  POD/status surfaces, materialization/readback contract, decoder, and hostile
  tests were independently authored. No Roslyn/Iced generator, sentinel-hole
  mechanism, runtime absolute patch, fallback skeleton, Iced source/binary, or
  SharpOS runtime behavior was copied.
- The accepted evidence is host-only byte/data, metadata, and modeled
  transcript evidence. Neither the gate nor the composite was invoked.

## Failures

- The first source-closure run found that the enlarged production file carried
  a second `#if !NOSTDLIB_EFI` region, while the exact EFI closure permits one
  canonical guard. The observation factory was made no-stdlib compatible
  within the existing guard, and the canonical preflight then reported 28
  reviewed sources, 3 prerequisites, and 41 compile items.
- An initial gate draft allowed its guard/lifecycle instructions to change
  arithmetic flags before the composite captured them. Independent review
  rejected that design. The final gate reloads the prepared full-RFLAGS qword
  immediately before its only call and the audit cross-binds that snapshot to
  the composite transcript.
- Independent review also found that the first modeled stack proof validated
  only the older composite-entry window. The final audit derives gate-entry
  `RSP` with the checked 48-byte delta and rejects separate impossible lower
  and upper gate-stack transcripts.
- The first post-fix full harness stopped before M01-032 at the historical
  M01-028 blocker-ledger assertion because M01-031's closure evidence had
  replaced four superseded facts. A separate one-field compatibility change
  restored those durable facts, passed the complete 413,657-byte harness, and
  was merged independently as PR `#396` before this branch was fast-forwarded.
- The first M01-032 authoritative run after that repair exposed a
  self-inspecting source test that embedded a second literal
  `[ModuleInitializer]`. Splitting the test needle preserved the single actual
  attribute and restored the M01-030 marker contract.
- The next run exposed a test-helper defect: materialization and readback took
  the prepared runtime state by value, relocating a deliberately
  self-address-bound POD. Production correctly returned
  `InvalidCompositePreparation`. Both helpers now pass the durable
  `fixture.StateDestination` pointer, and the stale combined instruction-count
  expectation was corrected from 804 to 806. Materialization, exact readback,
  atomicity, and the independent decoder then passed.
- The next modeled observation run supplied runtime return value `7` while
  expecting the narrower observation-return mismatch. That correctly failed
  the composite-state audit first. The negative vector now retains the
  canonical runtime return and corrupts only the observation's recorded return,
  reaching `CompositeReturnMismatch` without weakening production audit order.
- One concurrent diagnostic process briefly held the normal HostTests output
  assemblies during an intermediate rebuild. The process was verified as
  task-owned and stopped; an isolated-output build and the later canonical
  builds both passed with zero warnings and errors.

## Deferred work

- M01-025 must provide the serialized default-off live integration: select and
  allocate the exact physical arena from the refreshed firmware map, bind its
  real backing, materialize the accepted page, establish effective RX/WB and
  non-writable code permissions, prove exact full live-entry RFLAGS equality,
  keep writable state/observation memory mapped and exclusively owned, prevent
  external or concurrent mutation of validated inputs, satisfy TF/CET and
  LA57/PCIDE/IF/PGE policy, and invoke the gate from a trusted C#-authored
  transfer thunk.
- M01-026 retains cumulative real-QEMU/OVMF evidence ownership.
- No firmware allocator or service was called; no page was selected from a
  live EFI map; no generated code executed; no CR3, CR4, RFLAGS, PTE, TLB,
  interrupt, or protection effect occurred; no live guard/page-zero property
  was observed; and no NMI/SMI safety claim was made.
- No real QEMU/OVMF run was required or claimed. The dry-run validates command
  construction only, and `docs/completion-gates.md` remains unchanged.
