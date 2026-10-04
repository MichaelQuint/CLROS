# Step 977 - Accept owned ACPI copies and retain the reference stack-canary gap

## What changed and why

Accept the bounded M03-002/#59 owned-ACPI milestone and its #539 EFI supplement,
plus #536's COFF writer prerequisite. Record the completed common gate and fresh
reference guest progress without closing #79/#537: their explicit live
executing-stack canary requirement remains unmet. This separates the independent
ACPI dependency chain from the remaining runtime evidence work.

All executable evidence uses source commit
`eeee5232716eae338397111adf8146abe4402f26`, tree
`b360379f4629b61ed39b40063de552a15aa4952c`. Steps971-976 preserve implementation,
focused checks and repairs. This step changes documentation/catalog intent only;
reviewed merge, tracker closure and the subsequent #60 claim are separate actions.

## Files changed

- `docs/boot.md`, `docs/efi-build.md`, `docs/qemu.md`: actual pre-EBS ACPI copies,
  publication/capture evidence and exact runtime limitations.
- `docs/completion-gates.md`, `docs/nativeaot-nostdlib-limits.md`,
  `docs/repo-audit.md`: complete common validation, bounded reference progress and
  the unresolved live stack-canary criterion; no broader gate promotion.
- `.gitea/backlog/clros-issues.json`: ACPI closure intent, accurate six-row
  evidence/status updates and reproducible ACPI/reference guest commands.
  #79 stays open/claimed, #80 blocked, and #60 waits for actual #59 closure.
- `done/step977.md`: reserved with `scripts/new-done-step.ps1 -Reserve` at
  13:06:52-13:06:53 UTC before writing this milestone.

## Tests run

Actual commands on 2026-10-04 UTC:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build
.\scripts\run-qemu-smoke.ps1 -BuildEfi -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
.\scripts\run-qemu-smoke.ps1 -BuildEfi -EnableM04CheckedReferenceBarrierProbe -EnableM01LoadedImageStackProbe -EnableM01LiveAllocatorProbe -EnableM04IndependentEndianConformanceProbe -EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe -EnableM04ArithmeticConformanceProbe -EnableM04LayoutContractProbe -EnableM04ManagedHeapPageRangeProbe -EnableM04RhpNewFastProbe -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
.\scripts\run-qemu-smoke.ps1 -BuildEfi -EnableAcpiPhysicalDiscoveryProbe -MarkerPreset acpi -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd" -TimeoutSeconds 30
```

The three actual smoke commands additionally used unique explicit EFI, image,
serial and summary output paths preserved in each wrapper command record.
The clean solution build ran10:59:19.660031-10:59:38.652294, native0 with zero
warnings/errors. The complete original host suite ran11:00:20.643602-
12:45:59.076504, native0 and empty stderr: **3692 START / 3693 PASS / zero FAIL**,
one final `TEST_PASS host.tests` and all16 selected READY records. Independent
review verified all914 committed source/config/catalog inputs, five unchanged
launch/before/after/current DLLs with exact-head ProductVersions, full marker
multiplicities and chronology. No initializer or publication branch was removed.

Fresh default/reference/ACPI lanes ran12:52:30.5343381-12:56:09.6110437. All three
wrappers returned0 with empty stderr and complete serial/summary archives.
QEMU disposition was `runner-terminated`, signed exit-1; ordinary quiet drain
was configured750ms, not independently measured. Raw/lifecycle attempt2 passed
13:04:10-13:04:12; source/profile/command provenance passed13:06:32-13:06:33.
Final documentation/catalog validation passed on 2026-10-04:

- `scripts/check-naming.ps1`: naming and source-firewall checks passed;
  native exit 0, empty stderr, 13:16:39.363532-13:17:28.717985 UTC.
- `scripts/run-qemu-smoke.ps1 -DryRun`: `TEST_PASS qemu.runner.dryrun`,
  native exit 0, empty stderr. This checks configuration only.
- `scripts/sync-gitea-backlog.ps1 -Offline`: validated 345 catalog issues
  (16 epics and 329 children), 16 milestones and 68 labels; zero writes.
- `scripts/sync-gitea-backlog.ps1 -SelfTest`: all 28 milestone-policy cases
  passed; `actualApiCalls=0`. Both catalog checks returned 0 with empty stderr.
- `git diff --check`: passed before the scoped documentation commit.

Command/result records are the four `issue537-539-final-*` records under
`artifacts/orchestration-20261001/issue531-final-transaction`. These checks
follow the successful complete runtime gate; no runtime source changed.

## Logs and evidence

Original files are under ignored `artifacts/orchestration-20261001/`; generated
images and binaries are not committed.

- `issue537-539-common-gate/build-eeee523/result.json`:
  `28486DA252CBDE8AA01B0A0AC490D00EE7A3D561A9B578353BC2DD1E6F496858`.
- `issue537-539-common-gate/run-eeee523/result.json`:
  `C5C7FC40182091FC13176709BDE6B863AF5F8E880F9C8709A0EAB0C924C36C34`.
  Complete475598-byte stdout:
  `A00C15701A9DF639709F0BF86E546C56B3C9CB6249E928E88D52CA07FAFFFBB5`.
- `issue537-539-completed-common-independent-review-eeee523/completed/final-join.json`:
  `D0EE6780D1FB4AFE20DDA7A09A25C5085D185454352AFBEFA30D8C23BC52582D`.
- `issue537-539-guest-eeee523-20261004T1252138098884Z/capture-result.json`:
  `70084388B4423A6BE183EBA73302657C5069C3047986C2FDEAF3004620D2F358`.
  Its1793 archive records cover597/599/597 files. Each lane binds88 Compile
  rows,102 raw inputs,38 properties with0/9/1 enabled and zero OS AssemblyRefs.
- `issue537-539-native-actual-eeee523/native-join.json`:
  `AD7EB08099D4F38D03E27CFBFBB1BF94C0F53E30088B9C789A2484B693E25467`.
  Three actual metadata selectors and the unchanged reviewed join returned0.
  Whole PE/ILC maps match the reviewed native images;805 method and564 REL32
  associations agree, including three reference thunks and four full24-byte
  prefixes. COFF debug data differs; whole-COFF equality is not claimed.
- `issue537-539-captured-raw-independent-review-eeee523/attempt2/ordinary-result.json`:
  `3020CE71D305DED390B05BFF3C88399B9E5146F5C218F2EBFE3B04944F883BC1`.
  Its provenance result is
  `96574978AE5DCE02922B776F789F441008CA1EA67F0DCF128E723E1833605933`.
  The wording addendum correctly distinguishes23 callback events from the
  five OPERATION records. Numeric production-contract replay is not an
  independent physical-memory oracle.
- `issue537-539-disk-review-eeee523/actual-review-results.json`:
  `83CF150C16D3ED4C5D28EAF77D3C0DEB1D5991E524F115BD8488CE02533F04B2`.
  All23440 checks/2309 inputs passed. Twelve pristine-build/payload/FAT commands
  returned0. Default/reference/ACPI disks differ from pristine by887/892/887
  bytes across seven sectors each, fully accounted for as primary FSInfo,
  both FAT copies, root directory and NVVARS. EFI payloads remain exact; no
  whole-filesystem conformance or postboot/pristine equality is claimed.
- `issue537-539-closure-addendum-eeee523/acpi-address-association.json`:
  `A454FFA769BEE581CFA2CF188D984420C2774D87F3E3D528A06F4D99E71B012C`.
- `issue537-539-final-transaction/accepted-evidence.json`: parent acceptance
  `BB3B114FE92EE196A659FA60B473A3C63C2B380CD3218A652669E86E6B4E310B`.
  Original provisional slice flags remain unchanged; this record joins their
  scopes and explicitly leaves reference-issue completion false.

The ACPI stream has35 owned records. RSDP/root/MCFG/APIC/HPET offsets are
0/36/120/180/300, lengths36/84/60/120/56, totaling356 compared bytes. It reports
zero mismatches across payload,32 guard bytes and265884 unused-tail bytes.
Captured native RIP/static layout gives payload RVA1007912; the first emitted
owned pointer230760744 implies page-aligned image base0x0DB1C000. This base is
inferred, not independently observed LoadedImage telemetry. All owned spans
fit the compiled266272-byte aggregate and writable virtual image extent.
No external RAM dump, physical-source immutability or rebasing is asserted.

## Failures and unresolved acceptance

The failed76bc393 full run and intentionally stopped79dd765 run remain
unaccepted history. Steps975/976 preserve the production READY-token correction
and eight stale M02 fixture repairs. The successful eeee523 run supersedes
neither failure record.

The first independent raw audit exited1 because its expected Forbidden vector
placed `TEST_FAIL` sixth, whereas the actual runner seeds it second. The exact
same17-token set was retained; only its position changed after source review.
The second audit passed without changing product code, predicates or captures.
An ACPI reviewer arithmetic tuple initially omitted `mismatches=0`; its failed
diagnostic and corrected five-field result are preserved. Neither reviewer
correction constitutes a product failure or new runtime capability.

Closure review identified a substantive remaining #537 item4 requirement:
**no live executing-stack canary observation exists**. The same-image static
special/bulk bounds are9456/9072 bytes within the owned135168-byte stack.
Initial `M01_RSP_CONTAINED`, static-context canaries and controlled thunk tests
do not supply post-operation live stack-sentinel proof. The exact gap is in
`issue537-539-closure-addendum-eeee523/stack-canary-gap.md`, SHA
`2DEF9BEEBCC7B07B0696F95DBC745CD9C9AA08B5D582B5BB3B8B1CD7A8715A17`,
and read-back-verified Gitea comment14821. #537/#79 stay open and claimed.

## Deferred work and evidence boundary

After reviewed merge and evidence-qualified #536/#539/#59 closure, #60 is the
next firmware dependency: retain an actually acquired firmware entropy seed.
Its live RNG acquisition is not proved by static OVMF driver presence.
The separately serialized runtime repair must admit a real owned-stack
sentinel, check it after returned reference operations before READY/PASS, add
corruption controls and collect fresh evidence with a recomputed native bound.
#80 remains blocked until #79 is actually complete; no acceptance criterion is
waived to advance it.

ACPI evidence is pre-EBS owned-copy behavior only. Reference observations cover
six genuine allocations,576 retained bytes,23 callback events across five
OPERATION records and the reviewed noncollecting/exclusive-lifetime profile.
General WriteBarriers, GC/root visibility, concurrency, moving objects,
wider-vector preservation, unwinding, real ExitBootServices, post-firmware
ownership, devices, hosted .NET10/Kestrel, cloud and production readiness remain
unproved. Gates3/4/5 and M03/M04 remain incomplete.
