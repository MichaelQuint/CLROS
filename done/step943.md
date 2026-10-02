# Step 943 - Refresh legacy EFI source-shape assertions

## Summary

Correct nine legacy fixture offsets and one prerequisite count after the second
complete host run and the subsequent focused run exposed stale assertions.
Five #517 source entries were inserted between the normalized interrupt-frame
source and the endian source. Existing
exact 75-item Compile hashes and project hashes were already current; positional
assertions were not. No production input, boot path or probe default changes.

## Why

The guarded-IST module required the endian Compile item eight positions after
the normalized-frame item. The actual indices are 44 and 57, a distance of 13.
Two related fixtures use IDTR at index46 as their base, so their distance is 11
instead of six. The earlier mechanical audit missed these numeric assertions.
The corresponding nine fixtures now use the actual closed-list positions while
retaining their exact hashes, source keys, adjacency and behavior assertions.

## Files changed

- `tests/OS.HostTests/M02GuardedIstStacksEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedGdtTssTerminalEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02NormalizedInterruptFramePageEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedCom1ReadyEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02PanicSafeOwnedCom1EfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedX64GdtEfiIntegrationTests.cs`
- `tests/OS.HostTests/M04IndependentEndianConformanceEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02OwnedIdtGatePopulationEfiIntegrationTests.cs`
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`
- `docs/completion-gates.md` and this reserved step.

Each of the nine fixtures changes one numeric offset. The first seven use 13
after normalized-frame; the final two use 11 after IDTR. The owned-COM1 fixture
also changes its whole-runner switch-construction count from 11 to 19: eight
UD2 prerequisites, eight page-fault prerequisites and three shared M01 entries.
All other assertions and every production input are unchanged. This step was
reserved with `scripts/new-done-step.ps1 -Reserve`.

## Tests run

The final sixteen-module focused harness built with zero warnings/errors and
completed with native exit 0: 102 starts, 103 passes, zero failures, one final
`TEST_PASS issue517.legacy.remaining16.focused`, and empty stderr. It ran from
`2026-10-02T11:31:15.8135886Z` through `2026-10-02T11:41:36.2861751Z`.

The harness links every complete changed fixture plus the other remaining
legacy modules, their original initializers, actual HostToolProcess and the
real Kernel/shared/QemuRunner projects. It excludes only preflight, artifact
verifier and safe-stub, which completed unchanged before the failed nineteen-
module attempt terminated. This is a separate sixteen-module passing result,
not a nineteen-module single-run pass. The subsequent full host gate reruns all
modules from the final clean commit.

```powershell
$focusedProject = '.\artifacts\orchestration-20261001\issue517-second-failed-gate-investigation\focused-remaining16-preparation\Focused.csproj'
dotnet build $focusedProject -c Debug --nologo -m:1
dotnet run --project $focusedProject -c Debug --no-build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-naming.ps1
git diff --check
```

Naming and source-firewall checks both passed, native 0 and empty stderr.
Whitespace checks passed. Independent review accepted the exact raw marker
names, native phases, log hashes, ten-literal repair scope and unchanged
production inputs, with no findings.

## Evidence

Preserved failed tuple: `artifacts/orchestration-20261001/runtime-next-wave-fc561cb/`.

| Record | SHA256 |
| --- | --- |
| Result | `38F2FC267C3C08CD05DB9ABC098BC15ACA03DFBB8994F97A2369545BC8F05D8A` |
| Stdout | `0B854AD3497D6EFFD15509709A065477F8FBBDA3240734FC049342B3BE822E36` |
| Stderr | `E8A1956F8BE2508F81E4AEEB0B131E58540C20DA555212D0962B199AC2EC65CB` |

The passing focused tuple is preserved in
`artifacts/orchestration-20261001/issue517-second-failed-gate-investigation/focused-remaining16-preparation/`.

| Record | SHA256 |
| --- | --- |
| Passing focused result | `D1AAB8369DDFF088827EC46EF5A70968EB492DF65C4ED6ABB67D651EB6B8B604` |
| Native commands | `18D6225CDAA2293BEBD36ABB56A43537FF01E0F4DE9431C042372F8F8CA82168` |
| Build stdout | `BE9A96ABA7063731F980C518865275B7D7488AAC737903CFABCFAC027358ED3F` |
| Run stdout | `CE6F8BA9F73F5404357A2D4C71C483AC7283D3BCA0E4A5835C71A4046309D44B` |
| Naming result | `09846BE1F076E3B137BD06E76CACF05D2AF1E0AF5883BA05BAE5FE3ACB8B6E0A` |
| Independent completed review | `DA371992B18E07E3186D9DFDC1B767669AB8C9422284C4A1A38D08DDA604372E` |
| Independent machine verification | `A6721B16BF79FB7E5859E66DB406C1BF235A4564E1284138C8AF2882549CE60A` |

The source/marker inventory is
`587F38EDFAAFF4458E541AD0AA935E1A1ACD34F142A880C2F71A5FF3E58FC0D5`;
the native capture wrapper is
`4379531D7235AFEF56C820994432328F48FEA60BDD3AFFDE12DA8AE3DB4D9017`.
The independent report and verification are under the adjacent
`focused-remaining16-result-review/` directory. All 88 production inputs retain
their original raw identities; the explicit
75-item Compile closure and probe defaults are unchanged.

## Failures

The second clean full run began `2026-10-02T09:27:33.0309505Z` and ended
`2026-10-02T10:13:33.2862242Z`. Actual native exit was `-532462766`:
1362 starts, 1360 passes, two failure lines, no terminal success and 262 stderr
bytes. HEAD remained `fc561cb93c6bfd8e9c8fbc7dbcf0d44399231709`, tree
`b9c77d86b4a07f3e361d0908ae98e89e4c634dce`, with a clean checkout.

The guarded-IST primitive/Compile-closure group failed; its other four groups
passed. Repaired M01 publication and loaded-image modules each passed completely
in this full run. The page-zero module and later modules were not reached.
The other eight stale offsets were found by review, not executed failures in
this terminated run. The earlier failed 05b2bde run remains preserved separately.

The focused run began `2026-10-02T10:32:31.7888733Z` and ended
`2026-10-02T11:11:21.2689018Z`: native `-532462766`, 78 starts, 76 passes,
two failure records, no terminal and 258 stderr bytes. Owned-COM1's first three
groups, including its repaired offset, passed; its whole-runner assignment
count still expected 11 instead of 19. This is another static-review miss, not
a production-code failure established by this run. The failed nineteen-module
result stays preserved in `issue517-second-failed-gate-investigation/focused-preparation/`:

| Record | SHA256 |
| --- | --- |
| Focused result | `D286B19798075DE112E761659D1C811C44E518592ED2AA7B461DA39C36EA4922` |
| Focused stdout | `52B2EAAF3FD9A45D34CD28D9E7B1664CB6CB498BA89D2AFD96BEB62039FEA1CC` |
| Focused stderr | `875A392BFD194B74B01208C8723255964DEA2EAB95D815BA095E56C5BF0A2C3B` |

The attempted seventeen-module harness failed during its build before any test
ran: native 1, four CS0103 errors at safe-stub lines 411/414/417/420. That fixture
directly calls the omitted preflight helper. Removing the already-completed,
unchanged safe-stub fixture yields the sixteen-module harness; no test source,
helper or initializer was rewritten to bypass the dependency. The failed build
result is `56AF904482BD6CEFF443505119A0FA66877E01C3375A7922E1362C5C13556588`
in `focused-remaining-preparation/`. Its stdout SHA256 is
`BB700C4ED08C610542CDDA601D2666DF136AA1666A05AF722536A18BB915A8C2`.
The initial seventeen-module dependency review missed those direct calls.

## Deferred work

Rebuild and rerun the complete host suite at the new clean repair commit.
The complete host gate reruns all modules, including the three excluded from the
final focused harness; no nineteen-module single-run pass is claimed.
Only accepted full-suite evidence can release fresh default/page-fault guest
lanes. Rebind the accounting helper to that eventual commit in a new preserved
version. #517/#54 and PR518 remain open; #76 still requires its separate export
and compiled-object guest work.

## Evidence boundary

These are fixture corrections. They provide no new CPU fault, live IST, device,
firmware-shutdown, hosted .NET/Kestrel or cloud evidence. Gates 3/4 remain Fail.
No new real guest was executed for this milestone.
