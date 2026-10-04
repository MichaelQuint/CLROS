# Step 971 - Integrate bounded COFF fixups and start the next boot slice

## Summary

Import the exact reviewed #536 writer and complete fixture after PR535 merged
and #58/#532 closed. The writer exposes an explicit opt-in for REL32 to an
undefined external function, needed by the separately owned #537 reference
bridge. Existing overloads and the original instruction serializer stay intact.

#59's only declared prerequisite is now complete. Its labels/assignee and
managed issue body reflect the active owned-ACPI-copy implementation. The
catalog changes only M03-002's agent label and current evidence. The isolated
author owns two new host files; shared native work requires a separate release.
No ACPI-copy implementation or test result is included in this commit.

## Why

The original writer rejected the relocation shape required to call an external
scalar callback from the special NativeAOT reference-store entries. Keep the
extension narrow: REL32 type4, code-section source, undefined function symbol
section0/value0/type0x20/storage class2, complete four-byte file-backed fixup,
and no overlapping fixups. Validation and sizing finish before destination
writes. Other unsupported relocation/symbol forms continue to fail.

## Files changed

- `tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs`.
- `tests/OS.HostTests/CheckedReferenceBarrierCoffWriterTests.cs` (new).
- `docs/nativeaot-nostdlib-limits.md`: tooling proof and pending native boundary.
- `.gitea/backlog/clros-issues.json`: M03-002 claimed/current evidence only.
- `done/step971.md`: reserved at 06:06:42.031195-06:06:42.282032 UTC on
  2026-10-04 with `scripts/new-done-step.ps1 -Reserve`, native exit0.

## Tests run

The original complete new writer initializer plus unchanged
`M01InstructionPublicationSerializerGeneratorTests` initializer ran on isolated
inputs at 02:23:49.669782-02:24:12.857235 UTC on 2026-10-04: native exit0,
2 TEST_START /3 TEST_PASS /zero TEST_FAIL, one terminal `TEST_PASS host.tests`,
empty stderr. Tool/focused/child builds have zero warnings/errors. The tests
independently specify the complete233-byte object, decode headers/relocations/
symbols/strings, preserve nonzero addends, check fixup endpoints and malformed
kinds/symbols/bounds/overlaps, and compare the full patterned destination for
every rejection. This is retained historical evidence on the exact imported
writer/test bytes, not a new run of the canonical complete host suite.

After import into the root at `c3730ef839af14834e2381b74fda40c063e0e4a6`:

```powershell
dotnet build .\tests\OS.HostTests\OS.HostTests.csproj --nologo -m:1
dotnet build .\tools\OS.X64TextSerializerGenerator\OS.X64TextSerializerGenerator.csproj --nologo -m:1
git diff --check
```

The normal HostTests build ran 06:04:24.699314-06:04:41.290225; the generator
build ran 06:06:40.513178-06:06:41.752933. Both returned native exit0 with zero
warnings/errors and empty stderr. The diff check passed. A combined clean-head
solution/full-host gate is still required before #536 closure; the earlier
step970 common gate does not cover these newly imported files.

Naming and source-firewall checks passed at 06:09:16.672777-06:09:57.348922 UTC,
native exit0/empty stderr. Catalog offline validation also passed without API
mutations after the two-field M03-002 update. No new full-suite or guest result
is inferred from these checks.

## Evidence

Writer SHA256:
`5ADDA00451B4986560022786A9AC1E70A93D8D8D11B669949F2CB02DBBE8489E`.
Test SHA256:
`10CFC467B51C27DD82FAC46992580B30266C3FA88B365DDB8608D1F5F04E8E05`.
The unchanged default serializer is336 bytes, SHA256
`DA97E50C95E479541B65EB3F96D9C6DED163735857650BEE9416324086D55C7B`.

Author worktree `artifacts/issue79-coff-writer-prerequisite/final/report.md`
has SHA256 `84CE7315C80318D18DF972B4501E12B81FE1B6CF69766899EFB21C1B6BBFE6B8`.
Independent root report
`artifacts/orchestration-20261001/issue79-coff-writer-independent-review/review.md`
has SHA256 `0D3A17E7321280F9F844A2499A77EFECF544F584BE28CA25DE65FC8F7D1D057D`;
no actionable finding remained. Parent read the complete code/tests/results and
verified the retained package/input identities before import.

Root `artifacts/orchestration-20261001/issue536-root-integration/import.json`
binds the two exact imported sources. Recorder prefixes
`issue536-root-host-build` and `issue536-root-generator-build` under
`issue531-final-transaction/` retain actual commands/results/raw streams.
Host build stdout SHA256:
`E7EE32FBC009B22328264EBEF15109D5A66ACB21FF5D7CE8A42D8727390E5B5B`;
generator build stdout:
`051DB103095AFDCC8693BC3461BF8305E5F624F393297C81C64B82F709708E26`.

PR535 merge `c3730ef839af14834e2381b74fda40c063e0e4a6` has the exact reviewed
tree of final head `7ae5c096dd5103b4e29dfa033d1b04492ab7c3db`. #58/#532 closed
after evidence comments14697/14700. #59 claim/assignment readback succeeded,
followed by release comment14712 and the exact body update to version2.
`issue532-final-transaction/claim59/` and `issue59-owned-copy-release/` preserve
those requests/readbacks. Only intended fields and expected enclosing milestone
count/time changes occurred. Source ownership and commands are in comment14712.

## Failures

The first isolated fixture build produced eight CS8752 errors from target-typed
test constructions. Explicit relocation constructors fixed the test source;
the second complete attempt passed. Production bytes did not change between
attempts. Both attempts remain preserved. The canonical import/builds passed.
One parent read-only closure assertion initially omitted Gitea's expected
milestone count updates; exact count deltas were verified without replaying
any mutation. No product claim derives from that readback correction.

## Deferred work

Finish #537's actual returning callback/thunk, source/profile propagation,
complete controls, native ABI/call-graph/stack/SIMD inspection and guest proof.
Finish #59's five-table owned-copy host contract, then explicitly resolve its
native closure/integration and QEMU evidence. #60/#80 remain blocked.

This step proves bounded host-side object encoding and compile integration.
It does not prove a returning barrier, linked callback, live references,
collector cooperation, guest execution, post-firmware ownership, hosted
.NET/Kestrel, cloud readiness or production support. #79/#536/#537/#59 remain
open. No generated object, native source or secret is committed.
