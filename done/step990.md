# Step 990 - Read-only GC-static metadata admission

## What changed

Adds the first host prerequisite for #81/M04-012. The status-coded,
allocation-free `GCStaticRootsMalformedMetadata.ValidateOne` revalidates the
existing registered TypeManager owner via section201, then uses section204 for
a single-entry payload. It decodes one signed field-relative entry, tag1 cell,
16-byte abbreviated
MethodTable and one positive GCDesc series without writing or allocating.

Admission checks numeric extents, signed arithmetic, full owner capacity and
slot16 aliases, cell/metadata/Object overlap, the caller-provenanced related
Object address, flags,
base size24..4096 and series offset/length. Missing/empty sections, malformed
data and valid unsupported forms return distinct results. Results contain
immediate scalar observations, not retained publication authority.

## Why

#80 now provides authenticated module/section lookup. Before adding GC-static
materialization, #81 needs a bounded decoder that rejects malformed bytes
without touching root storage. Keeping this host pair separate also allows
#546 to retain exclusive ownership of the shared EFI integration lane.

## Files changed

- src/OS.Kernel/Runtime/GCStaticRootsMalformedMetadata.cs
- tests/OS.HostTests/GCStaticRootsMalformedMetadataTests.cs
- docs/nativeaot-nostdlib-limits.md
- docs/completion-gates.md
- .gitea/backlog/clros-issues.json: only M04-012 status/evidence/path correction
- done/step990.md

The allocator, TypeManager core, EFI project and shared runtime sources are
unchanged. The step was reserved with scripts/new-done-step.ps1 -Reserve using
the shared allocation view at03:40:09.836635-03:40:10.062705 UTC on2026-10-05.

## Tests run

The source pair was frozen at checkpoint2 on baseline
e7e543b09a960081ebe6f8bf32696af3d2a9f4cd and independently reviewed before running
the three-stage recorded attempt once. Actual UTC intervals on2026-10-05:

1. Focused Release build,03:32:15.606074-03:32:22.892644, exit0.
2. Five complete original initializers,03:32:22.898930-03:32:31.730622, exit0.
3. Normal OS.HostTests Release build,03:32:31.739034-03:32:50.041323, exit0.

Exact commands and inputs are retained in the command-plan and attempt1
records. The normal command is `dotnet build tests/OS.HostTests/OS.HostTests.csproj
--nologo -c Release -m:1 --disable-build-servers -p:UseSharedCompilation=false`.
Both builds report zero warnings/errors; all three stderr
streams are empty. The focused stream has42 START/43 PASS/zero FAIL/one host
terminal/five READY lines. All15 new groups and the unchanged TypeManager,
object-layout, retained-heap and RhpNewFast initializers ran.

Cases cover a literal byte oracle, both rel32 directions and numeric extremes,
exact16-byte MethodTable readability, supported/unsupported forms, full span
and alias controls, fresh observation after payload replacement, complete
buffer/canary preservation and a warmed1000-call zero-allocation assertion.
Observed POD sizes are readable-window16/result88.

The new child uses the actual92 explicit OS.csproj sources plus the validator:
93 unique C# inputs and94 source/project identity rows. It compiles at
03:32:23.2817645-03:32:25.0970351, exit0/no timeout/empty stderr, with zero
AssemblyRefs and nonempty ValidateOne body RVA45036. Its779264-byte DLL is
31A54E6C7AF6ED11143EF1A1C0299BAB0BD74861AACFF5709C01F42F4866454C.
That no-stdlib DLL is inspected, not executed. The original Type/layout/heap/
Rhp children use92/3/8/10 sources. Legacy layout/heap children enforce native0
before PASS and retain streams but have no separate saved exit-result file.

All934 recorded inputs and nine launch files match before/after and their
retained copies. Independent review accepted source, command membership,
ordered markers, actual child inputs/results and the three terminal records.

Final integration checks on2026-10-05 also pass: catalog offline validation at
03:44:05.870544-03:44:13.986994 UTC, and naming.guard/source.firewall at
03:44:05.875742-03:44:50.406137 UTC. Both commands exit0 with empty stderr.
`git diff --check` passes. These add no runtime or guest acceptance.

## Logs and evidence

Ignored worktree artifacts/issue81-gc-static-roots retains source-checkpoint1,
source-checkpoint2, command-plan, all attempt1 raw streams/source snapshots and
the additive final-v2 package. Ignored root artifacts/orchestration-20261001
retains the independent review and parent integration records.

- Core:45E385BD4B61F4572ABDF24FE858E46FE78797D017A6887B977B4EFB1FD66783.
- Tests:FFE2CF9039EAD984CA828FB4C9293D858873AF290401EC09122CAC46188401F0.
- attempt1/result.json:AE9BD2E38682327E64833AB6692989FD1A838442BEDFC744C8F86984AA32C501.
- Focused stdout6279 bytes:46793502C570F1FF3FE8303EB0FA5F36E24E7A17BB5EE25E0EA27262FFD8AD26.
- final-v2/report.md:BC36D128C8EF6F7B93D31E0B8FCB6EDB51B813934A6C8A20EBC55D75C907FC29.
- Independent issue81-host-source-review/completed/result.json:
  7E6D763AA304CC0676B1F1E267B6A7B0541883FEF0CA90BE9FE3D8E746D45765.
- Independent completed/review.md:
  E39355DB3C34EFF07C9D7192A33E39D40C8D5D8D36F999C06A235C8B55EC885D.

## Failures

No product stage failed or was retried. Before execution, source inspection
found that the alias-test setup seeded tagged bytes into the genuine slot.
Checkpoint2 restricts that setup to the intended descriptor/MethodTable cases;
the core and command membership did not change. Both checkpoints are preserved.

The first artifact-only package writer incorrectly expected93 identity rows;
the actual ledger also records OS.csproj. An additive writer accepts exactly
93 source rows plus that project. The original failed package is preserved;
no source edit or product rerun followed. A read-only exploratory lookup of a
nonexistent legacy metadata.json was corrected by inspecting its actual
initializer assertions/streams. It was not a product execution failure.

## Deferred work and evidence boundary

#81 remains open/claimed; #82 remains blocked. The windows are caller-attested
readable mappings/lifetimes, not PE write permissions or hardware fault probes.
The fixture uses controlled host bytes, not genuine compiler GC-static data.
Explicit registered cell-write admission, actual compiler201/GCDesc/Object
association, sole-allocator static-base materialization, static-field barrier
authority and retained nonmoving root lifetime remain future work.

No complete common-suite, native-image or QEMU run is claimed for this change.
No GCHandle/spine, collecting GC, general runtime initialization, real
ExitBootServices, hosted .NET/Kestrel, cloud or production acceptance follows.
Gates3/4 remain Fail. Shared EFI work requires a separately owned integration
issue after the current #546 lane finishes.
