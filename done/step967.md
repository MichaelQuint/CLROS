# Step 967 - Retained-page checked-store and bulk-move host prerequisite

## What changed and why

Issue #79 needs real bounded stores and overlap-safe moves before native helper
integration. Add one allocation-free leaf over the original retained 4 KiB page,
with explicit noncollecting/exclusive caller obligations. Every operation calls
the existing thirteen-input retained-authority validator. No second allocator,
binder, cursor, runtime export or reference registry is introduced.

`TryStore` writes one aligned eight-byte opaque word. `TryMove` validates both
full unsigned spans, then copies aligned words in the correct overlap direction.
Valid zero/self moves write nothing but still validate authority. Default
results fail; rejection preserves storage. Numeric values are not authenticated
CLR references, and page membership does not prove object/field ownership.

## Files changed

- `src/OS.Kernel/Runtime/CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.cs`.
- `tests/OS.HostTests/CheckedReferenceBulkBarriersIncludingDotNetN10BulkTests.cs`.
- `docs/nativeaot-nostdlib-limits.md` and `docs/completion-gates.md`.
- `.gitea/backlog/clros-issues.json`: only M04-010 current evidence.
- `done/step967.md`: this separately reserved record.

The author used clean base `7e64f4cfac99428df60f717bd51e34f4a382779b` in
`C:\Users\Roger\.codex\worktrees\m04-checked-reference\CLROS`. Root integration
starts at `2e457ead81172d15dcb8ce64de91ffdd1d9200d4`; intervening PR533/534 changes
are documentation only. Parent imports the exact two reviewed source files.

## Actual tests and commands

Final attempt2 ran 2026-10-04 00:28:28.676007–00:28:42.873646 UTC:

```powershell
dotnet build artifacts/issue79/attempt2/harness/Focused.csproj --nologo -m:1 -c Release -nodeReuse:false
dotnet artifacts/issue79/attempt2/harness/bin/Release/net10.0/Focused.dll
dotnet build tests/OS.HostTests/OS.HostTests.csproj --nologo -m:1 -c Release -nodeReuse:false
```

All three commands returned native 0 with empty stderr; both builds reported
zero warnings/errors. The focused harness includes the complete new module and
unchanged retained-page, fixed-allocation, array and String modules. Its actual
stream has 43 starts, 44 passes including one terminal `TEST_PASS host.tests`,
and zero failures. This is the complete five-module harness, not the repository's
full suite. The new module contributes eleven pairs and explicitly reports
`hostOnly=true noncollecting=true guestIntegrated=false runtimeExports=false
gc=false gate4=Fail`.

Independent byte oracles cover null/high-bit/scalar words, two real mapped-page
positions, both overlap directions, boundaries, unsigned overflow, zero/self
moves, invalid capacities, aliases, stale/free authority and mixed allocations.
Rejection fixtures immediately compare all 65,536 bytes after each call, including the
page, metadata, cursor and canaries. Warmed leaf calls allocate zero CLR bytes.

All five modules perform their real no-stdlib child builds. The new eleven-source
C#14/net10 library uses the unchanged shim, NOSTDLIB_EFI, removed framework
references and warnings as errors. PEReader verifies zero AssemblyRefs and IL
bodies for all four leaf methods. The four unchanged modules also pass their
8/10/11/11-source checks. All 896 recorded inputs and both harness binaries are
unchanged across execution. No native target image is executed.

## Evidence

Author records are under `artifacts/issue79/attempt2/` and `final-v3/` in the
assigned worktree. Parent reviewed the complete source, strengthened test delta,
actual command records and raw stream, and rehashed the 22 package records,
108 evidence records, eleven no-stdlib inputs and 896 stable batch inputs.

- Production, 5,127 bytes: SHA256
  `8F40B7416297C73EE252D25573EF84755C48B27A7C3E60F498C708D6C3C68021`.
- Tests, 30,021 bytes: SHA256
  `B4AED0852606AF004564D5E58E21E2EFB0D395FA6FBDF20242FD19A961D2C3CE`.
- Attempt2 `result.json`: SHA256
  `9B4AD1CF6B7639E195BF24EC0CFEA2313EC1D8B010FB75E2F5DBD7D5460E48AF`.
- Focused stdout, 3,769 bytes: SHA256
  `FD32406D1121B9588649D06C103705E052E22790DD3D767EC4D51C9579FAB653`.
- Final report: SHA256
  `73E57A2F24251C5101FBEC6768BBA88EA43960E9824EC86EA9177CEB9BF96D8A`.
- New no-stdlib DLL, 65,024 bytes: SHA256
  `5D99DEE3D5ABFFBC9E6AA337C33A77AAB1A832FD90B6F41C74FF35E85EBB2EBC`.

`actual-child-closures.json` preserves each inherited child's actual evidence
format. The retained-page fixture saves no separate exit-code file; its complete
initializer checks the child result. No missing evidence file is fabricated.
Step967 was reserved with `scripts/new-done-step.ps1 -Root C:/Projects/CLROS
-Reserve` under explicit WinPS ExecutionPolicy Bypass, native 0.

The independent host review is
`artifacts/orchestration-20261001/issue79-host-independent-review/review.md`,
SHA256 `F4F9EB1D2433FF4E3302A3AD4752FD7CC40D00110A13CE406208749DC1B171C9`.
Parent's integrated `dotnet build .\CLROS.slnx --nologo -m:1` also passed at
00:39:58.672451–00:40:20.919685 UTC on 2026-10-04, native 0, zero warnings/errors
and empty stderr. Its 1,513-byte stdout SHA256 is
`A457EFEA0955824866A05971F8182EA0AC438EB39A8AFE899580F9122261D2D2`;
the command/result/raw logs are under
`artifacts/orchestration-20261001/issue531-final-transaction/issue79-root-solution-build.*`.
This build does not execute the full repository suite.

## Failures and corrections

No product build or test failed. Attempt1 also passed 43/44/0. Self-review found
that a rejected zero store on a zero page could hide an unintended write;
attempt2 strengthens the test with patterned bytes, a distinct nonzero payload
and immediate full-buffer comparisons. Production is unchanged.

The initial parent staged whitespace check found an extra blank line at EOF.
The step was normalized to LF with one final newline before the final check;
no production source or test changed.

Two packaging attempts expected the same exit-file format from every inherited
child. Both failed associations and partial packages remain preserved. Final-v3
uses the actual formats without changing product code or rerunning tests.

## Deferred work and evidence boundary

The common full repository suite and native/guest integration remain pending.
The leaf does not authenticate CLR objects/fields, accept outside-page writes,
implement runtime helper or special byref ABI, publication ordering, GC polls,
remembered sets, collection, reclamation or concurrency. Mapping/lifetime remain
caller obligations. No EFI Compile link, shim/helper ledger, probe or boot order
changes are included. Issue #79 stays open and #80 is not promoted. Gates3/4 stay
Fail; real EBS, hosted .NET/Kestrel and cloud readiness remain unproved.
