# Step 951 - Accept bounded double-fault delivery on original IST1

## What changed and why

The default-off M02-015 EFI implementation from step950 now has independently
accepted full-host and fresh default/PF/DF QEMU evidence. The DF experiment
captured vector 8, hardware error 0 and the complete 192-byte handler capture on
the original IST1 stack. This step updates seven canonical documents and only
M02-015 currentEvidence, evidenceBoundary, requiredCommands and desiredState in
the existing catalog. All 345 rows, ownership notes and dependencies are retained.
No production/test source or earlier done step changes in this transaction.

Source tested: `cc7f08ef434151be98245799eb6af270a9bf141a`, tree
`68a69de12d16ed5c2d6df41b550e30071bae8929`. The subsequent evidence-only commit
and PR #522 merge are separate from the tested source identity. Catalog closed
intent is not a claim of remote closure. Actual #55/#521 closure follows the
reviewed commit/merge and evidence-comment readbacks.

## Files changed

- README.md
- docs/diagnostics.md, docs/boot-order.md, docs/efi-build.md, docs/qemu.md
- docs/completion-gates.md, docs/nativeaot-nostdlib-limits.md
- .gitea/backlog/clros-issues.json
- done/step951.md (reserved with scripts/new-done-step.ps1 -Reserve)

## Commands and results

`dotnet build .\CLROS.slnx --nologo -m:1` completed native0 with zero warnings,
zero errors and empty stderr at 22:03:30 UTC on 2026-10-02.
`dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`
ran from 22:05:02.1292816 to 23:19:23.5790379 UTC: native0, 3,580 TEST_START,
3,581 TEST_PASS, zero TEST_FAIL, one final `TEST_PASS host.tests`, empty stderr.
The complete 7,242-line/463,435-byte log has all 3,577 distinct expected names,
correct multiplicities/order, 24 changed modules/208 pairs and eight ready markers.
All 497 source inputs match committed bytes. All three postrun binaries retain
their launch bytes/hashes and exact ProductVersion `1.0.0+cc7f08ef434151be98245799eb6af270a9bf141a`.
The completed V2 checker and independent final review both accepted the run.

Three fresh `-BuildEfi` wrapper invocations followed that accepted host gate.
Exact recorded commands (run from C:\Projects\CLROS):

```powershell
& 'C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe' '-NoProfile' '-ExecutionPolicy' 'Bypass' '-File' 'scripts/run-qemu-smoke.ps1' '-BuildEfi' '-Qemu' 'C:\Program Files\qemu\qemu-system-x86_64.exe' '-OvmfCode' 'C:\Program Files\qemu\share\edk2-x86_64-code.fd' '-TimeoutSeconds' '15' '-EfiArtifact' 'C:\Projects\CLROS\artifacts\efi\issue521-guest-cc7f08e-20261002T2327058759726Z-default.EFI' '-Image' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\default\clros.raw' '-SerialLog' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\default\serial.log' '-SummaryLog' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\default\summary.log'
& 'C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe' '-NoProfile' '-ExecutionPolicy' 'Bypass' '-File' 'scripts/run-qemu-smoke.ps1' '-BuildEfi' '-Qemu' 'C:\Program Files\qemu\qemu-system-x86_64.exe' '-OvmfCode' 'C:\Program Files\qemu\share\edk2-x86_64-code.fd' '-TimeoutSeconds' '15' '-EfiArtifact' 'C:\Projects\CLROS\artifacts\efi\issue521-guest-cc7f08e-20261002T2327058759726Z-pagefault.EFI' '-Image' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\pagefault\clros.raw' '-SerialLog' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\pagefault\serial.log' '-SummaryLog' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\pagefault\summary.log' '-EnableM01LoadedImageStackProbe' '-EnableM01LiveAllocatorProbe' '-EnableM01OwnedPagingProbe' '-EnableM02SafeStubProbe' '-EnableM02CheckedPortIoBindingProbe' '-EnableM02Com1OwnedPortIoProbe' '-EnableM02OwnedCom1ReadyProbe' '-EnableM02PanicSafeOwnedCom1Probe' '-EnableM02PageFaultCr2ErrorCodeProbe'
& 'C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe' '-NoProfile' '-ExecutionPolicy' 'Bypass' '-File' 'scripts/run-qemu-smoke.ps1' '-BuildEfi' '-Qemu' 'C:\Program Files\qemu\qemu-system-x86_64.exe' '-OvmfCode' 'C:\Program Files\qemu\share\edk2-x86_64-code.fd' '-TimeoutSeconds' '15' '-EfiArtifact' 'C:\Projects\CLROS\artifacts\efi\issue521-guest-cc7f08e-20261002T2327058759726Z-doublefault.EFI' '-Image' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\doublefault\clros.raw' '-SerialLog' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\doublefault\serial.log' '-SummaryLog' 'C:\Projects\CLROS\artifacts\orchestration-20261001\issue521-guest-cc7f08e-20261002T2327058759726Z\doublefault\summary.log' '-EnableM01LoadedImageStackProbe' '-EnableM01LiveAllocatorProbe' '-EnableM01OwnedPagingProbe' '-EnableM02SafeStubProbe' '-EnableM02CheckedPortIoBindingProbe' '-EnableM02Com1OwnedPortIoProbe' '-EnableM02OwnedCom1ReadyProbe' '-EnableM02PanicSafeOwnedCom1Probe' '-EnableM02DoubleFaultIstProbe'
```

| Profile | UTC start/end on 2026-10-02 | EFI bytes | Raw bytes / CLROS records |
|---|---|---:|---:|
| default |23:27:20.3283318 /23:28:30.0143982|55,808|7,947 /21|
| pagefault |23:28:31.2441691 /23:29:40.8544599|558,592|5,529 /86|
| doublefault |23:29:42.0993247 /23:30:52.4309811|560,640|6,119 /88|

Each wrapper completed native0/empty stderr. Each actual runner records owned
termination, canonical QEMU native exit -1 and a 750ms quiet drain; no timeout,
cleanup failure or spontaneous exit occurred. The drain is runner evidence,
not a second independent wall-clock measurement. All 578 archived files per
lane, the 79 Compile/92 raw inputs, 28-flag vectors (0/9/9 enabled), actual
zero-AssemblyRef OS.dll and exact tooling/link bindings were independently checked.
Default returns to firmware as before. PF/DF end at the final CRLF with no return
suffix. The pinned 304-byte OVMF prefix is unchanged. The default accepted suffix
is 6,947 bytes including the final marker's CRLF (6,945 firmware-only bytes).

## Native and current-state evidence

Actual managed IL, COFF/map and final PE bind fresh page-image Audit, final
ReadBack, serializer/cookie, entered-state assignment and full-width terminal
entry/context. Fresh DF preferred Run is 0x18006AF20, terminal call 0x18006B60E,
serializer 0x180001000; runtime entry is C+0x1300 with RCX=D+0x1000. A returned
terminal call reaches a self-loop. Actual DF EFI equals the reviewed author
image; fresh whole COFF/managed DLL differ and were independently inspected.

C=0xDC3F000, D=0xDC35000, T=0xDC0A000; C3/D10/T43 and six mapped C/D ranges give
52 leaves. Five raw walks agree on absent null and four guards. Fresh source-bound
audit/readback/publication uses the original table backing. The sole PF gate
Present-bit change leaves DF vector8/IST1 present and PF vector14/IST2 not present.
The null-read trigger is C+0x1A8A. The actual current handler RSP 0xDC38F40 equals
IST1 top 0xDC39000 minus192, before a software pivot. Current CS8/SS0, TR0x18,
busy TSS0x8B, GDTR(D+0x1018)/limit0x27 and IDTR(D)/limit0xFFF agree with original
resources and gate targets. Hardware error0 is present. The 21 saved DF
state/GPR/CR2 fields remain diagnostic-only; observed saved RIP equality is
not a success predicate. PF retains its distinct IST2/vector14 regression oracle.
There is no independent live C/D/T dump: authority is the exact executed
source/readback path plus captured current-state observations.

## Disk accounting

All12 reconstruction/decode/accounting commands completed native0/empty stderr.
Independent review bound 2,241 source/tool/archive/result identities, including
463 archived Debug ImageBuilder files and445 source inputs per lane, then checked
GPT CRCs/copies, FAT geometry, both FAT copies, allocation census, directory chains,
FSInfo and extracted EFI bytes. All three images are64MiB with126,943 data clusters.
Pristine and booted EFI payloads exactly match each captured EFI.

| Profile | Changed bytes/sectors | Free clusters before/after | New NVVARS chain |
|---|---:|---:|---|
| default |887/7|126829/126826|116-118|
| pagefault |892/7|125847/125844|1098-1100|
| doublefault |892/7|125843/125840|1102-1104|

All changed bytes belong to primary FSInfo, both FATs, root directory and the
observed1,393-byte NVVARS file. No unexplained mutation or additional allocation
remains. Primary FSInfo matches the census; backup FSInfo remains pristine and
stale. Both FAT clean/no-hard-error bits remain set. No filesystem-conformance
claim follows from this bounded comparison.

## Immutable evidence identities

All paths below are under `artifacts/orchestration-20261001/` and remain ignored.
The joint record rehashes140 result/report/command/inspection identities and
preserves original capture/accounting/helper `GuestAccepted=false` history.

| Evidence | SHA256 |
|---|---|
| issue521-build-cc7f08e/result.json |66B625EDED3946789B51AE61F81BD2AE461398CB1E2B38D300006A7202A71092|
| runtime-next-wave-cc7f08e/result.json |F30F66D6F869763C788A7C7DF9577E76C7D6DBE2A7A41E21FE95096739ED1478|
| runtime-next-wave-cc7f08e/host-tests.stdout.log |7E8A0A8ACD60EFE407972C8699D4B5AB76348B0F29FB42E3DD91FF4C2FDB324F|
| runtime-next-wave-cc7f08e/completed-check-v2.json |F70911FDCEBEAE7382C932FDC61D27944FD8B189396A5E7B0DE32B3BF6E27F93|
| runtime-next-wave-cc7f08e-independent-review/final-completion/review.md |A32F7927C7ED53C3199744A542EA6B185FBF8B59D43DEA63F287FDB26E11DFD7|
| issue521-guest-cc7f08e-20261002T2327058759726Z/capture-result.json |132BBC69EAC2DEA08DCC54BF435AB7A1D98EDF199C0E77D99A635580CA21479A|
| issue521-pristine-cc7f08e-20261002T2333536579464Z/result.json |9B3AD0E422AB3B141310F80E49B712C23F83E520E77C695A7E4DDD16CE8E2DBA|
| issue521-captured-native-review-cc7f08e-20261002T2327058759726Z/review.md |CE4680794511F6B7D0721269470C90B8CD0A0771E313F3688A0A474DE64FEA1A|
| issue521-captured-raw-review-cc7f08e-20261002T2327058759726Z/review.md |2009F57953378D6506EBE02099CBDA6AEBFB842642689E5E5E86A1349F0086E5|
| issue521-captured-raw-disk-independent-review-cc7f08e-20261002T2327058759726Z/review.md |895C835C586161E6FA4350DE59F07C9B76DEA5CCD7426651142DB997A14F8ECD|
| issue521-joint-acceptance/acceptance.json |DCBC7D5736269132E67689BA303B999D8B10637CE48700D31D770992D284E75B|
| issue521-joint-acceptance/acceptance-identities.json |65FFD30A455DFAE140D6D8340E745B9CA3165CF78F7F4AD1A0D6FA2C21CA8D40|

## Failures and final transaction checks

No failure occurred in the settled full-host run, three capture invocations or
12 disk-accounting commands. Preserved review-only failures include minimal-runtime
ReflectionOnly/type-loading incompatibility, local archive/IL-decoder/call-count
assumptions, summary BOM ingestion and the default suffix's two-byte CRLF boundary
convention. Corrected read-only callers passed; captured/product/frozen-helper
bytes were not altered. Parent display-only missing-path lookups did not mutate
evidence. Earlier source/fixture/build preparation failures remain in step950
and their original logs. Original provisional flags are not retroactively changed.

The complete catalog module passed native0/empty stderr at 23:59:00 UTC on
2026-10-02:17 starts/18 passes/zero failures and one final
`TEST_PASS manifest.focused`. Naming and source-firewall checks both passed at
00:00:15 UTC on 2026-10-03; working-tree `git diff --check` also passed. Their
actual commands, logs and results are in `issue521-final-docs-checks/`, with
`checks-result.json` SHA256
`3CB37979D54085E5BB2DE6C9B216ADF675A905F893D15F3DF27DA699A7CC88D1`.
The evidence-only commit additionally checks the exact staged path/byte inventory,
UTF-8/no BOM, LF/single final newline and staged whitespace before committing.

## Deferred work and evidence boundary

The next disjoint host scopes are M04-008 array allocation (#77) and M02-016
unexpected-vector panic reporting (#56), released only after actual #522 merge
and prerequisite readbacks. Later native/shared-file integration remains separately
reviewed and coordinated. This evidence adds no arbitrary exception recovery,
universal triple-fault prevention, nested NMI, general mapping/lifetime guarantee,
real ExitBootServices, GC/runtime expansion, hosted .NET/Kestrel, cloud or production
readiness. Dangerous probes remain default-off; Gates3 and4 remain Fail.
