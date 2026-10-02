# Step 931 - Version-pinned NativeAOT layout value contract

## Summary

M04-005 / #74 now has a pure scalar evaluator for fixed canonical objects,
SZ arrays, strings and the fixed MethodTable prefix under NativeAOT 10.0.8,
Windows x64, little-endian. It returns explicit statuses and source-derived
offsets/raw/aligned sizes without accessing memory or creating objects.
The two-file host leaf passed independent review and focused validation.

## Why

Later allocation and runtime helpers need explicit layout coordinates and
the selected compiler's flags. Reference-project assumptions are insufficient:
low ushort flags are not a component size unless HasComponentSize is set,
and the selected .NET 10 flag/kind encodings differ from older references.
This bounded value contract precedes allocation, GC and guest integration.

## Files changed

- `src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.cs`
- `tests/OS.HostTests/ObjectArrayStringEETypeMethodTableLayoutContractsTests.cs`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/completion-gates.md`
- This centrally reserved step.

No EFI project, compiler shim, boot phase, probe, manifest dependency or
reference-project source changes. Parent copied the frozen reviewed source
and tests with exact SHA256 verification.

## Tests run

Author worktree: `C:\Users\Roger\.codex\worktrees\m04-005-layout\CLROS`,
based on `1b6fbe5539210739bbe0709e12aca58a42918772`.

- `dotnet run --project artifacts\m04-005\focused\Focused.csproj -c Release --nologo`:
  passed one `TEST_START/TEST_PASS m04.005` pair, no failures. It covers 13
  literal size rows, independent prefix/string/array bytes, every low-ushort
  value under fixed/array interpretations, invalid/unsupported inputs and
  status precedence, unmanaged/default results and zero allocation across
  40,000 warmed calls. The maximum component/count case detects 32-bit
  truncation: raw size 140735337136209, aligned size 140735337136216.
- The focused module compiles the actual production source with
  `NOSTDLIB_EFI`, no framework references and warnings as errors. Metadata
  inspection confirms zero assembly references and `Evaluate` NoInlining,
  using the existing canonical MethodImplAttribute. This is a C# compiler
  check, not an ILCompiler publish.
- `dotnet build tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1 -c Release`:
  zero warnings/errors. The author did not execute the full test assembly.
- Independent review rechecked all 12 pinned primary snapshots, evaluated
  flag/shape/size boundaries and test oracles, and reran the focused Release
  checks. No actionable findings remain; source hashes stayed unchanged.
- Parent integration reran
  `dotnet run --project artifacts/orchestration-20261001/m04-layout-focused/Focused.csproj -c Release --nologo`:
  passed with the same exact transcript/hash. Whitespace checks pass,
  including both new files.
- Combined solution build passed with zero warnings/errors in 12.31 seconds;
  naming/source-firewall checks passed. Logs are
  `artifacts/orchestration-20261001/layout-wave-solution-build.log` and
  `layout-wave-naming.log`. Independent final integration/documentation
  review found no findings and confirmed both frozen source hashes.

One complete settled-source host gate will cover this leaf and #510 after
the combined wave is frozen. It has not run yet. Earlier full-suite or guest
evidence does not cover this new source. No EFI/QEMU execution occurred here.

## Evidence

Author handoff/logs/projects: `artifacts/m04-005/` in the worktree above.
Copied handoff: `artifacts/orchestration-20261001/m04-005-implementation-report.md`.
Reviewed primary snapshots and package provenance:
`artifacts/orchestration-20261001/m04-005-prep/`.
Both pinned packages identify `dotnet/dotnet` source commit
`94ea82652cdd4e0f8046b5bd5becbd11461482ca`. Source paths, exact URLs, raw
SHA256 values and Git blob identities are recorded for all 12 files; none
is a runtime dependency or proposed committed native source.

| Item | SHA256 |
| --- | --- |
| Production source | 44F8A34E2F1911AAE4FD5B028ADB88BF1D8BF06CE915625E9F13D11964F579AA |
| Host tests | D22AB6680E73713844C62E2FC66C4DC474F96955FD66D94740618DE16DFB249B |
| Focused stdout, author and parent | AE5590221AD864BE9A2741292DBAF4E1FD2FBE503B17D93BF17DCBE069CDD665 |
| Release host-project build log | 27AA76F7D38812B520AC44211338362E86D0DF194F20AA660B386C79A0E4B41C |
| Primary-source identity report | 7F9D94FAD01E489587916B6D7109057AB03D8636349AE6309A69E48B1905FEB2 |
| Final no-stdlib build log | 5D6EEF2B50DD3EA884AC5A90FD97A06BEF02FEDE4E994114BE7F3CD49DB52DE7 |
| Final no-stdlib DLL | 6AD21CA736E3308DF10C215BCB67FDC44872402E19A9574A9ED82E40F08E4888 |

The author's final isolated compile is
`nostdlib/5fd5057828ed4f7989eaefb89555fe86`. Exact host record:

```text
CLROS_M04_005_READY profile=ilc10.0.8-win-x64-le shapes=3 prefixBytes=24 referenceFilesRequired=false hostConformance=true guestIntegrated=false gate4=Fail
```

## Failures

The first focused attempt incorrectly expected a PackageVersion field in
ResolvedILCompilerPack XML; the test now checks the project's actual version,
target and path bindings. A second attempt reached the isolated compile but
its generated test shim lacked Delegate/MulticastDelegate predefined types
needed by Roslyn enum equality binding. Established test-only declarations
resolved CS0518; no canonical shim or runtime delegate capability was added.
Both failed logs remain preserved. A successful intermediate compile without
explicit NOSTDLIB_EFI is also preserved; the final run defines it and supplies
the accepted no-stdlib evidence. No unresolved failure remains.

## Deferred work

Finish the combined wave's full gate and merge review. Keep #74 open, claim a
separate EFI/project/script/fixture integration issue, and execute the actual
value evaluator in a default-off guest profile with independent expectations.
Fresh builds, source/artifact/profile identities and real QEMU/OVMF transcripts
are required before closure. The inherited marker-only command is insufficient.

## Evidence boundary

The prefix is a scalar observation, not a native overlay or validated type
identity. Its token/count/hash fields do not prove pointers or optional-tail
storage; numeric capacity does not reserve memory. No memory access, parser,
allocation, GC, type discovery, dispatch or exception/unwind support is added.
Gate 4 remains **Fail**. Later guest execution of these value checks would
still not establish physical compiler-emitted object layout, a hosted runtime,
real firmware exit, Kestrel or cloud readiness.
