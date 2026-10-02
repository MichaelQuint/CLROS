# Step 949 - Accept bounded RhpNewFast guest allocation evidence

## Summary

Accept the complete integrated host gate and five fresh QEMU profiles for
M04-007/#76 and EFI supplement #519. Actual guest execution allocates ten finite
fixture objects through four compiled-new and six direct-export routes, then
proves unchanged-state rejection before binding and at exhaustion. Independent
reviews bind managed metadata, native ABI, relocated prefixes, whole raw
transcripts, process lifecycle and pristine/booted disks to the same source.

M02-015/#55 also passes the complete host suite. Its actual double-fault/vector8/
IST1 guest remains a separate pending integration.

## Why

Source, controlled tests and linked artifacts alone did not establish actual
allocation under firmware. This milestone supplies that bounded guest proof
without promoting general allocation, GC or runtime completion. It follows the
warning correction in step947 and catalog ownership-note repair in step948;
their failed/interrupted runs remain preserved.

## Files changed

- `README.md`: current host and finite allocation guest status.
- `docs/diagnostics.md`: complete DF host integration, guest still pending.
- `docs/efi-build.md`: accepted clean build, closure and five profiles.
- `docs/nativeaot-nostdlib-limits.md`: allocation scope and remaining limits.
- `docs/qemu.md`: successful and expected-negative evidence/lifecycle boundary.
- `docs/completion-gates.md`: accepted evidence; Gates3/4 remain Fail.
- `.gitea/backlog/clros-issues.json`: only M02-015/M04-007 currentEvidence changes.
- `done/step949.md`: this reserved evidence record.

No production/test/helper code changes in this milestone. All catalog values
except the two named currentEvidence fields remain unchanged, including exact
ownership notes. Generated evidence stays ignored.

## Tests run

The clean source commit is `77860a689b8460114cf179e44dd442533a1ad638`, tree
`2bd0015594a9a0107eae732f3694d61de42d3c42`.

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
```

Build: native0, zero warnings/errors, empty stderr, 18:10:55–18:11:25 UTC.
Complete host: native0, 18:11:37.0260050–19:27:35.8587337 UTC on 2026-10-02;
3,573 starts, 3,574 passes, zero failures, one final `TEST_PASS host.tests`, empty
stderr and clean unchanged source. The exact independent inventory covers
3,570 distinct names with required multiplicities, 27 affected modules/219
selected pairs and seven ready records. It includes all nine DF host groups,
the actual no-stdlib closure, revised allocation module and20 legacy fixtures.
All three host DLL hashes/lengths match launch; each ProductVersion identifies
this exact source commit. No binary-change exception was needed.

The reviewed v2 capture wrapper executed fresh build/image/OVMF/QEMU for each
profile from19:33:26 to19:40:03 UTC. Its recorded invocation is:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/orchestration-20261001/issue519-guest-wrapper-v2/Run-Issue519GuestCapture.ps1 -ExpectedHead 77860a689b8460114cf179e44dd442533a1ad638 -HostResultPath C:/Projects/CLROS/artifacts/orchestration-20261001/runtime-next-wave-77860a6/result.json -AcceptedHostResultSha256 D300D66EBBD5EF60C9DB5E3862E4DC2741655EC4FEB9C3CF2E4CECDF118DC875 -AcceptedHelperManifestSha256 6FC77E99AB5327EF92BD091DAE70CC307E6C4BA01327CE53F37060095060A809
```

All nine wrapper/build/image/observer commands exit0 with empty stderr and no
timeout/cleanup failure. All five actual QEMU exits are signed -1 following
deliberate owned termination. Default/heap/positive use the normal runner and
750ms drain. Before-bind/exhaustion use the separately reviewed observer and
measured quiet756.2881ms/752.4209ms; exact expected TEST_FAIL remains at EOF.
Spontaneous exit, silence or the rejection marker alone is not accepted.

| Profile | Owned records | Accepted observation |
| --- | ---: | --- |
| default | Ordinary inventory | Allocation callers absent; boot plus permitted firmware return suffix |
| heap | 52 | Retained one-page heap behavior without allocation callers |
| positive | 78 | Ten objects, four compiled-new/six direct, used272/count10, intact prior objects/canaries, boot success |
| beforebind | 49 | Leaf status5/not_initialized; zero used/count/constructors/mutation; one attempted MT token; terminal halt |
| exhaustion | 71 | Ten fixtures plus159 plain fillers, used4088/count169; leaf status15/exhausted, full heap/bump bytes unchanged, constructorCount163; terminal halt |

Five fresh metadata-only processes inspect the archived OS.dll bytes: zero
AssemblyRefs, unique local owner/export, exact object(MethodTable*) signature,
RuntimeExport payload/parent, NoInlining and initialized IL body. Five native
drivers pass0/0/10/1/10 routes;21 current final-PE associations are independently
derived. Default and heap lack all24 named allocation callers/getters/readers
and their export enters Halt. Four exact matching historical EFI images/maps
and selected methods/graph permit reuse of reviewed native ABI analysis.
The managed DLLs were inspected afresh; whole OS.obj files differ only in
file-backed .debug$S section payload, with other section payloads and raw
relocations matching. No equality of whole objects/DLLs is assumed.

The bounded path distinguishes the leaf's hidden POD result buffer from object
RAX, preserves shadow space/alignment/nonvolatile state and supplies constructor
RCX/scalar RDX. Reader offsets are8/16/16/16; six value routes prove allocation
only. Positive/exhaustion each supply ten full24-byte prefix observations:
exact COFF ADDR64 to PE DIR64 coverage and signed relocation reproduce every
byte, with deltas -6212726784/-6212722688. Before-bind has only the actual token
association and no prefix readback. No recursive allocation/GC/barrier/cctor
dependency was found in the bounded reviewed helper closure.

Disk accounting uses each archived builder for five pristine64MiB images and
compares all ten images. All20 reconstruction/decoder/accounting commands exit0
with empty stderr. Both negative prelaunch images equal reconstruction byte
for byte. EFI payloads and manifest stay exact. Each booted disk changes only
seven sectors: primary FSInfo, one sector in each FAT, root directory and three
NVVARS sectors. NVVARS is1,393 bytes and explains all three added clusters.
Backup FSInfo retains stale pristine hints; filesystem conformance stays false.
No extra guest or rebuild was used to manufacture review evidence.

The final documentation/catalog transaction passes the complete existing
manifest module (17 starts/18 passes/zero failures), both naming/source-firewall
groups and `git diff --check`, each with native0 and empty stderr. Commands ran
20:05:58–20:07:40 UTC; their records are retained under `issue519-finalization`.
The outcomes were added to this new step and its reserved-template line endings/
extra EOF normalized afterward; catalog and source bytes stayed unchanged.

## Evidence

Paths below are relative to `artifacts/orchestration-20261001/` and remain
ignored. The parent read and hash-verified each final report before accepting
the combined result. Original capture/helper records retain their narrower
provisional/GuestAccepted=false flags unchanged.

| Record | SHA256 |
| --- | --- |
| `issue519-build-77860a6/result.json` | `D83764050C425E1D104E93807D3E5D40BF1192D20CA40661CEE4A5B389316DE3` |
| `runtime-next-wave-77860a6/result.json` | `D300D66EBBD5EF60C9DB5E3862E4DC2741655EC4FEB9C3CF2E4CECDF118DC875` |
| Complete host stdout | `4C7D8DD3CB73C3B080457361EE4076678612AA4C83F9E598CEE5927263F901C0` |
| `issue519-full-host-77860a6-review/review.md` | `8FDAC61F30180B56054469F3795019768BDAB081CBCAF7B952DEAC8E5D540DD5` |
| `issue519-guest-77860a6-20261002T1933267352899Z/capture-result.json` | `257ADDEFAB45306F627F330D086549E87AB7173A769CCEBA52EC1D33505490DA` |
| `issue519-actual-lifecycle-77860a6/review.md` | `2936AD92777B38FBD60B284506EB3156D5CBC250E493552B36C7F208C919B821` |
| `issue519-captured-native-review-77860a6-20261002T1933267352899Z/native-review.md` | `18488E640A65E139D0366712ECC368DBF2FC9D004B92E411932039E3EDAD3160` |
| `issue519-pristine-77860a6-20261002T1940426362017Z/result.json` | `451ACA7588E705797A4320E00C496DBF49A7931EB931ED7051A8ADFF1A67697B` |
| `issue519-actual-capture-disk-review/review.md` | `85429676BCF29EA8E3977166CB68C489ABA5DCDD8840ADD8F957F1A45CA6C799` |
| `issue519-finalization/joint-acceptance.json` | `22EE49F651CE941A906FF7A8C869D03BDCE2726253BB05E11145448B0B59FCA3` |
| `issue519-finalization/checks-result.json` | `DFB23B56FD49D54A06B87F1BEAFB5216217DE954EFD4CF7AEC2D300E3465DE91` |

| Profile | EFI SHA256 | Serial SHA256 |
| --- | --- | --- |
| default | `46EDB9DE137332B6B33EA53EA479C17CA1F1E792CD86BED10257E87781E63679` | `0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1` |
| heap | `374509995FD0B7ADB83E100EE4CF394719C1B700D5D854D2A5975FF968E6A03F` | `F81E9F6482471E9515840B82992194E86F296521DCC45D089A92A76921B443C7` |
| positive | `D71A72EFCA91690C87FF9E66CCC8844D74B30203635179A7466DE666D920853A` | `36B18BFD077EA85874D5EA8323724A3D7075681DD14BB2CC637BDC17B7DA7AA0` |
| beforebind | `65DC817C6967271E6A4164BE957F32EE1CD06333C2EDA866CF4E4A3954AD9FB5` | `6848D6E4BE3CD85D5178D7DAB970E14156E30FC823F0DB306BA2DE82DC719302` |
| exhaustion | `372E147C6900F142F1C6E0674F92288160B073C59316D4744FBEAE98B630AB13` | `BBAD42B63FB7E186F84949EE3F45C3628368FBD29003ED68B16BFB2ADC10E72A` |

The unchanged authority is77 Compile items/90 raw inputs/27 profile properties,
raw aggregate `77c2779c669b7f3491344570418e9d1dcb27061f35041e60e7c3bd66e929d1cd`.
Accepted host evidence was posted with GET readback to #519/comment14034 and
#76/comment14036. Final closure comments and exact-head merge follow this
reviewed documentation commit; this record does not claim they already occurred.

## Failures

No actual command in the accepted build, full host, five captures, raw audits,
native/metadata/prefix or disk run failed. Expected negative guest TEST_FAIL
records remain specified rejection evidence. Steps946–948 retain earlier
warnings/interrupted runs, the failed catalog run and its ownership-note repair.

Review attempts remain preserved: unavailable initial PowerShell path;
PowerShell7 JSON Int64 values rejected by the pinned WinPS Int32 helper before
unchanged-helper success under native WinPS; and a section-diff observer that
initially treated BSS size as file bytes, corrected to represent uninitialized
sections. The initial milestone patch was rejected for duplicate operations on
one target before any mutation; an ordinary update replaced the reserved text.
A convenience terminal projection printed null fields from ordered dictionaries;
the actual saved command records retain native0 and full identities. A no-index
whitespace wrapper misclassified diff exit1 with no diagnostic as failure;
the staged whitespace check remains the final transaction check. No product/
capture change or acceptance-predicate relaxation followed.

## Deferred work

Merge the exact reviewed documentation head in PR520, then post closure evidence
and close #519/#76. Keep #55 open and release its reviewed30-path EFI supplement
after this shared lane merges. #77 array work is the next runtime slice and
must claim the actual dependent source/hash paths before editing the leaf.

## Evidence boundary

This is a finite ten-type, retained one-page, pre-firmware-exit allocation proof.
It does not establish general allocation/refill/reclamation, GC/roots/barriers,
reference writes, boxing copy/nullable substitution, arrays/strings,
cctors/dispatch/exceptions, arbitrary callbacks, post-firmware lifetime, actual
DF delivery, real ExitBootServices, hosted .NET10/Kestrel, cloud or production
readiness. Gates3/4 remain Fail. Historical steps and raw captures are immutable;
documentation changes do not relabel their narrower evidence.
