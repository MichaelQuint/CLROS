# Step 989 - Accept bounded current-image TypeManager registration

## What changed and why

The #544 EFI integration now completes #80's one-module registration milestone.
The executing NativeAOT image supplies the original M01 loaded-image authority,
one RTR16.0 module and a writable registration slot. The default-off adapter
registers that module and a slot-derived consumer reads the separate native ABI.
The full common suite and fresh default/TypeManager QEMU captures have passed
independent native, raw/lifecycle/provenance, numeric association and disk review.
This releases #81 GC static-root materialization; it does not implement roots.

No product code changes in this final evidence commit. The master merge brings
the already accepted entropy documentation/catalog/step986 into the TypeManager
branch. Its sole conflict in docs/efi-build.md preserves both complete sections.
The tested product remains commit 34bd75c82a7a9b2c43a5c098cc2782d03ec3c968,
tree 9b9b6f97aa09fe234c4e3283991f610fd696ac19. Steps983/985/987 retain the
implementation, focused tests and earlier failure history unchanged.

## Files changed

- docs/efi-build.md: actual native/common/guest acceptance and runner command.
- docs/completion-gates.md: scoped #80/#544 completion and #81 readiness.
- docs/nativeaot-nostdlib-limits.md: accepted finite adapter; general ledger deferred.
- .gitea/backlog/clros-issues.json: M04-011 done/closed, M04-012 ready/open.
- done/step989.md: this evidence record, reserved using new-done-step.ps1 -Reserve
  through the shared allocation view at 2026-10-05 02:31:53 UTC.

## Tests run

The full CLROS.slnx build ran 2026-10-04 23:57:25.914542-23:57:51.390202 UTC:
native exit0, zero warnings/errors and empty stderr. The complete unfiltered
OS.HostTests run used the normal --no-build entrypoint at the tested commit,
2026-10-05 00:01:24.203638-02:09:38.806833 UTC (7694.603195 seconds). Result:
3733 TEST_START / 3734 TEST_PASS / zero TEST_FAIL / one host.tests terminal /
17 selected readiness records. The checker verifies exact occurrence ordering.
All 923 source records and five launch DLLs match before/after/current reads;
their ProductVersion is 1.0.0+34bd75c82a7a9b2c43a5c098cc2782d03ec3c968.

The actual common default/enabled NativeAOT children and both guest builds
match all seven previously accepted whole files per profile: OS.dll, OS.obj,
OS.map.xml, EFI, link.map, link.rsp and serializer.obj. No new LLVM or metadata
analysis was needed for identical bytes. Closure: 92 Compile entries, 106 raw
inputs, 40 flags; default zero enabled flags, TypeManager exactly two.
Raw aggregate: d6754ab6ab3005d0b4293b239d4039497302d9acae060946c9023ea1c4d616ed.

Fresh ordinary-runner captures executed 02:22:56.670657-02:25:59.296218 UTC.
Both used run-qemu-smoke.ps1 -BuildEfi, the installed QEMU x86_64 executable and
OVMF code image, -TimeoutSeconds 30, and explicit fresh output/log paths.
The enabled command also used -EnableM04TypeManagerModuleRegistrationProbe
and -EnableM01LoadedImageStackProbe; default used neither. Exact argv and
605 archived files per profile are retained. Both wrappers returned exit0;
QEMU signed exit -1 was runner termination, not a natural successful exit.
Configured quiet750ms is not an independently measured duration.

The complete TypeManager serial stream has 2154 bytes: the accepted 304-byte
firmware prefix, 45 ordered unique owned records and immediate EOF. The default
7947-byte stream includes its accepted firmware prefix and UI-return envelope.
The enabled records demonstrate capacity/repeat/foreign rejection, present-empty
versus missing section lookup, one registration and retained ABI consumption.

Observed image base is 230055936, image extent 905216 bytes; the native preferred
base is 6442450944. The 31-row RTR16.0 header is at 230205864. Storage230215720
has capacity4096 and required1824; ABI230215992 is storage+272, with length48.
The full writable slot is 230215688/16 bytes, with an admitted 8-byte lookup row.
The real slot load and all six ABI qword reads are in the accepted native proof.
No GC-static rows201/207 are present in this fixture; it cannot prove GC roots.

The retained static stack analysis bounds the continuation plus entry allowance
at 16159 bytes within 135168 usable owned-stack bytes; firmware/interrupt depth
is excluded. This is not a measured guest stack-top/canary result. A5/5A tail
canaries are internal and initialized after registration, not external guards.

Eight accounting children and the independent disk reader return exit0 with
empty stderr. The reader verifies 15390 checks over 1713 stable inputs.
Pristine disks were reconstructed AFTER capture. Default887/Type892 changed
bytes each span seven sectors and are entirely explained by NVVARS and FAT/
directory/FSInfo bookkeeping. Both NVVARS payloads are 1393 bytes/three clusters;
EFI payloads remain exact. No general filesystem or firmware-conformance claim.

After the documentation-only master merge, sync-gitea-backlog.ps1 -Offline
passes at 02:54:30.551375-02:54:38.549047 UTC; naming.guard and source.firewall
pass at 02:54:30.550825-02:55:09.823959 UTC. Both return exit0 with empty stderr.
git diff --check passes. These final checks add no product/runtime acceptance.

## Logs and evidence

Ignored root artifacts under artifacts/orchestration-20261001 retain all raw
streams, command intervals, source/archive identities, reviews and failures.
The final record is issue544-final-integration/joint-acceptance.json, SHA256
8B23A838D58F32C82EB6E895F3DE3DEBA50875D0C2C647BD892E563937940889.
It joins independently scoped reviews without rewriting their historical
GuestAccepted=false fields; merge and issue closure are subsequent operations.

- Common: issue544-common-independent-review-34bd75c/completed/final-join.json,
  E622D206F3FB27CBFED8A358E9463BEC2425D0914E886ADC9663EAE3948B3538.
- Common stdout: E65B5807E57D31474C4D62435949778BC5877C9C28363B76F4A36752E6A5DD94.
- Native: issue544-native-rebind-actual-34bd75c/final-result.json,
  56CB808A2ABC4C08BCB3CDA489A9F14AAB04CBC301CCFCDD33437B7EB99ED9F9.
- Capture: issue544-guest-34bd75c-20261005T0222568912242Z/capture-result.json,
  F442CA6CFEA53C9751C39EC5DAF8652DCEC9A92F7ABFB887208497E7C84CDBDA.
- Raw/lifecycle: issue544-captured-raw-independent-review-34bd75c/result.json,
  7C2E0349F4F5C051AFB9CE0D17C6FECB158AB12271772243E7DCC3E87385D238.
- Native guest: issue544-captured-native-association-34bd75c/numeric-association.json,
  F7FE829BD62924868718F15D9087F4433A5B7EDEACDE443B7554BC5B951AFA87.
- Disk: issue544-disk-independent-review-34bd75c/result.json,
  2F74C8A3C19642F427A3680E9E3EB99C170107AAD5E18DE2138D18836323EA00.
- Type serial: 6C7EDA185617508683F72DA8A30E2AFC05E0F4A5C633351E87757772BCCA2365.
- Default serial: 0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1.

## Failures

Step985 preserves the first source-admission failure and exact-path/full-hash
correction. Step987 preserves the first full common failure and four legacy
raw-project hash repairs. The subsequent full common run above passes.

The first final offline ordinary audit failed before checks: Windows PowerShell
inherited a PowerShell7 module path and could not resolve Get-FileHash. A fresh
recorded attempt corrected only the child PSModulePath to native Windows paths;
the helper, capture, bindings, grammar and libraries were unchanged. Both
ordinary and provenance checks then returned zero with empty stderr. No product
build or QEMU rerun followed that environment correction. Exploratory read-only
schema/display errors remain in reviewer records and are not acceptance runs.

## Deferred work and evidence boundary

One bounded current-image module and slot-derived consumer are accepted. #81
must materialize authentic GC statics with allocator/barrier/lifetime evidence;
#82 collection is still blocked. Caller mapping and lifetime obligations remain.
No upstream registry, general TypeManager scanning, TLS, cctors, GC roots,
collecting GC, concurrency or hardware-fault rollback is implemented here.
There is no external RAM oracle, invented pre-slot observation or full RAM dump.
TypeManagerRtrScans stays Deferred; Gates3/4 stay Fail. Real ExitBootServices,
hosted .NET 10/Kestrel, cloud readiness and production readiness remain unproven.
