# Step 940 - Bounded fixed allocation over retained heap authority

## Summary

Implemented the host-only portion of issue #76 / M04-007 from merged base
`cf677286dd16dce00c3e191be2ead2bae3d85d92`. `GuestTestRhpNewFast` binds one
retained 4096-byte page and allocates fixed class/value-shaped byte intervals
through the original #75 authority. It exposes no runtime export or CLR object.
Independent source review accepted the two-file implementation with no
actionable findings. Issue #76 remains open; the combined settled-source full
gate and EFI/export/guest acceptance are pending.

## Why

The accepted heap-page guest hands off storage but does not allocate objects.
The next bounded slice needs exact origin/header/MethodTable arithmetic,
zeroing, metadata admission, rejection and lifetime checks before EFI export
integration. The reviewed v2 plan includes required primitive/enum/struct and
generic/reference-bearing value shapes as well as classes; the earlier
Class-only proposal remains superseded.

## Files changed

- `src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs`: thirteen-field original
  access bundle, 80-byte stable bump state, 88-byte result, bind/allocation
  operations and local pinned fixed-type admission.
- `tests/OS.HostTests/GuestTestRhpNewFastTests.cs`: nine focused subgroups,
  independent byte/canary checks and actual ten-source no-stdlib compilation.
- `docs/nativeaot-nostdlib-limits.md`, `docs/completion-gates.md`: implemented
  host behavior and the remaining export/compiled-new/guest boundary.
- `.gitea/backlog/clros-issues.json`: only M04-007's status label and current
  evidence change; dependency, ownership, acceptance and open state remain.
- `done/step940.md`, reserved by `scripts/new-done-step.ps1 -Reserve`.

The two author files came from the isolated `m04-007-host` managed worktree.
No frozen #74/#75 leaf, EFI project, runtime shim, boot order, probe, shared
utility, existing test or generated artifact is changed by this milestone.

## Tests run

Author worktree:

- Release focused build/run over the actual kernel and new test module:
  actual exits 0, zero warnings/errors, empty stderr, ten starts, eleven
  passes, zero failures and one host-ready record.
- Complete `tests/OS.HostTests/OS.HostTests.csproj` Release compilation:
  actual exit 0, zero warnings/errors. This was a build, not a full test run.
- Actual ten-source no-stdlib compilation: exit 0, zero AssemblyRefs.
- `git diff --check` and explicit whitespace checks for the untracked files.

Parent root integration:

```powershell
dotnet build artifacts/orchestration-20261001/issue76-integration/Focused.csproj -c Release --nologo -m:1
dotnet run --project artifacts/orchestration-20261001/issue76-integration/Focused.csproj -c Release --no-build
```

Both actual exits are 0; build has zero warnings/errors and both stderr files
are empty. The 2026-10-02 06:42:59-06:43:04 UTC run has ten starts, eleven
passes, zero failures and exactly one `TEST_PASS m04.007.focused`. The module
also builds the actual ten-source no-stdlib closure and verifies zero
AssemblyRefs. The parent also ran the existing manifest-focused build/run:
actual exits 0, seventeen starts, eighteen passes, zero failures and exactly
one terminal marker. Naming and working-tree whitespace checks passed with
actual exit 0. All stderr files were empty. Full settled-source host/QEMU
acceptance is not claimed here.

## Evidence

| Input/result | SHA256 |
| --- | --- |
| Production leaf | `22DF5D019EC4F09C5CB870B807ED91B9EA62C35E30CF59C031C3A15290CB9F45` |
| Test module | `E2966CB52BFDE3499ED7FABA0057E0BD9055166ED2D5DB5915DF6616B8B0B932` |
| Parent focused result | `4546E499131D89FDDA88E0B046398C7931AEE325E88CE5A0EB88D3FEAC3EC3F1` |
| Parent build stdout | `5614A550745B536AD71A0DBC82D09F829B1EEF83B8C5D3B2377B887E91A52D37` |
| Parent run stdout | `65E0F8FC965C620DA53DF6B3378970EBD565B9B816CBEC1E1BD9D2F57C192E11` |
| Manifest run stdout | `83F7A21A90295B6D3822D4505847274316C587AF5529005E63DA5DC60578E42F` |
| Naming stdout | `FB9E2F9DEB178474FD6713AA598348A053B28FC79CA27732159CDD0AC565CE18` |
| Independent source review | `AF6C75B02805365690D209CCD065008B6E343C0F8734DF61B4ED1767A97A7C38` |
| Archived author evidence inventory | `B0C387E177E51AD6658D718D53E788FCEC7B1B49BEA40500E0E2696CA1332967` |

Logs, exact native commands/times and structured results are in
`artifacts/orchestration-20261001/issue76-integration/`. The author handoff and
all 94 prior/final evidence files were archived byte-for-byte in
`artifacts/orchestration-20261001/issue76-author-evidence/`; their worktree
originals remain intact. The independent report and raw metadata checks are
in `artifacts/orchestration-20261001/issue76-independent-review/`.

The tests use actual #75 reserve/retained validation with synthetic physical
addresses and real aligned host backing. Full 65,536-byte snapshots check
unchanged authority, tails and canaries. Literal byte oracles verify 24/32-byte
allocations, token placement at origin+8, preserved prior payload, all seventeen
fixed elements, generic/reference positives, padding/category limits, invalid
tokens/profiles/storage, 128x32 exact fill, 170x24 leaving sixteen bytes and a
single 4096-byte allocation. Existing nonzero objects and legitimate activity
on the other allocator page remain valid; a free extent overlapping the retained
page rejects. Warm bind/allocation/rejection paths allocate zero CLR bytes.

```text
CLROS_M04_007_HOST_READY pageBytes=4096 minimumObjectBytes=24 stateBytes=80 hostConformance=true guestIntegrated=false compiledNew=false runtimeExport=false gc=false gate4=Fail
```

## Failures

No product build or test run failed. Parent review corrected the subgroup
failure marker to include a bounded reason; final builds/tests passed after
that test-only change. Initial issue-release script calls were rejected before
mutation for unsupported combined helper arguments; corrected sequential body,
assignee and label operations were independently read back successfully.

## Deferred work

The separately owned EFI/export supplement still needs a named RhpNewFast
export, actual compiled helper calls, authentic image-backed MethodTables,
Windows x64 RCX/RAX and stack/register proof, physical object/header/cursor
readback, required fixed class/value/generic/reference cases and terminal
failure evidence. The original zero-page checkpoint must precede object writes.
Boxing payload/nullable behavior, reference barriers and GC remain separate work.

## Evidence boundary

All new execution is host caller-buffer evidence. MethodTable tokens are
opaque numbers and never dereferenced; scalar admission does not prove real
metadata. Stable exclusive readable/writable buffers, actual mappings and one
controller remain caller obligations. There is no forged-state/ABA/SMP
protection, reset/free/grow, allocation refill, OOM exception or collector.
No real CLR object, compiled guest allocation, post-firmware lifetime, hosted
.NET/Kestrel, cloud or production readiness is established. Gate 4 remains Fail.
