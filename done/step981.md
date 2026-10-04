# Step 981 - Bounded reference helper and live stack-sentinel acceptance

## What changed and why

The step977 returning-reference guest lacked an executing-stack sentinel.
Step979 added the two-word/five-check source and focused controls. This step
records the accepted clean 5d894c8 common gate and fresh default/reference/ACPI
captures, completing that bounded requirement without promoting general GC or
runtime readiness. Parent owns the subsequent merge/tracker actions.

## Files changed

Only docs/nativeaot-nostdlib-limits.md, docs/efi-build.md,
docs/completion-gates.md, .gitea/backlog/clros-issues.json and this reserved
step. M04-010 requests closed/agent:done; M04-EPIC and M04-011 evidence is
refreshed. M04-011/#80 stays agent:blocked until actual #79 closure. The
reservation is the already allocated issue541-integration/reserved-step981.md;
no second allocation or product source change is made here.

## Actual commands and results

Evidence source: 5d894c873723282b7b6ff4a0fdae5af6730f31b1,
tree a0988ae08cbeeb67728fe55c6feb355c0424dbb6, C:/Projects/CLROS.

- `dotnet build ./CLROS.slnx --nologo -m:1`: 14:57:59.336663-
  14:58:26.047528 UTC, native 0, zero warnings/errors, empty stderr.
- `dotnet run --project ./tests/OS.HostTests/OS.HostTests.csproj --no-build`:
  15:01:22.940092-16:53:10.583367 UTC, native 0, empty stderr;
  **3704 START / 3705 PASS / 0 FAIL**, final TEST_PASS host.tests, 16 exact READY.
  All 916 raw source/catalog inputs match committed blobs and pre/post records;
  all five launch DLL hashes/versions remain unchanged.
- Native Windows PowerShell ran
  `issue60-537-capture-release-5d894c8/Run-Issue532GuestCapture.ps1`
  with ExpectedHead above, HostResultPath
  `issue60-537-common-gate-5d894c8/run/result.json`, accepted host hash 424D4D89
  and helper manifest 247F39C0 (full hashes below/in the command record).
  The exact outer argv, 16:56:51.127741-17:02:11.294062 UTC interval, native 0
  and empty stderr are preserved in capture.result.json. Its three genuine
  child smoke commands contain the full profile vectors (38 flags, 0/9/1 true),
  QEMU/OVMF paths, 30-second timeout and actual EFI/image/serial/summary paths.
- Strict ordinary/raw/lifecycle review passed 17:06:41.744402-
  17:06:44.408531 UTC; finite provenance passed 17:06:44.415044-
  17:06:45.803730 UTC. Both native 0/empty stderr. Reference has 71 protocol and
  observed rows, ACPI 35; wrappers exit 0 and signed QEMU exits -1 are classified
  runner-terminated. Ordinary 750ms is configured drainage, not measured quiet.
- The accepted native join passed 17:25:41.315807-17:25:43.716663 UTC,
  native 0/empty stderr. Three actual metadata inspections also pass.
- Twelve actual reconstruction/accounting children and the outer accounting
  command passed 17:03:16.119749-17:03:34.252222 UTC. The read-only auditor
  passed 17:05:35.855628-17:05:42.046197 UTC, 23514 checks/2313 inputs.
  Independent review rehashed 2326 identities without rerunning those tools.

These are completed evidence records, not commands newly executed for this
documentation patch. Detailed exact argv remain in the cited command/result
files; no dry-run or controlled transcript is substituted for guest evidence.

Final documentation/catalog checks on 2026-10-04 also pass:
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-naming.ps1`
at 17:41:33.428730-17:42:37.193587 UTC, native0, two START/two PASS and empty
stderr; `scripts/sync-gitea-backlog.ps1 -Offline` at 17:39:41.039693-
17:39:59.948549 validates all345 catalog issues; `-SelfTest` at
17:39:41.058575-17:40:01.126203 passes28 policy cases with zero actual API
calls. Both catalog commands use native PowerShell with ExecutionPolicy Bypass,
exit0 and empty stderr. Exact command/stream/result records are under
`issue537-final-transaction/` as naming-direct, catalog-offline and
catalog-selftest. `git diff --check` passes. Product inputs remain the same
as the accepted common gate; this change is documentation/catalog only.

## Native and live observations

All three whole PE images and complete ILC maps equal their reviewed proofs;
fresh COFF-to-PE joins cover 112/547/153 methods and 57/485/72 REL32 fixups.
Only COFF debug sections differ. The source closure remains 88 Compile/102 raw,
aggregate 341ce1b74d47028a9cf7ce96fa5f70fc1ac51893938673a45237268e3c712ed6,
with zero managed AssemblyRefs. Three special thunks, original 13-input heap
authority, six genuine allocation roles, four static full 24-byte prefixes and
six observed relocated prefix rows are bound to the current image.

The reference stream reports 23 callback events in five OPERATION rows. Two
fixed qwords at the original 135168-byte usable stack bottom are armed once and
remain unchanged through five checks at counts 18/19/20/22/23. The final check
follows bulk/retained-page verification and precedes STACK/READY. Actual
compiled reader 488B01C3, two arm stores/read calls and five two-load checks are
bound. Witness=end-1248=entryRSP-72; 96-byte sentinel state and 768-byte M01 state
are disjoint static spans outside that stack. Exact returning bound 8448 and
conservative local bound 8456 below the witness fit 16384; this record leaves
125448 bytes between that conservative bound and sentinel end. This is not a
sampled RSP or a measured whole-stack high-water mark.

ACPI regression retains 153 methods/72 relocations and original physical-copy
ABI; five owned copies total 356 bytes, 32 guard bytes and 265884 untouched tail
bytes with zero mismatches. Disk accounting preserves exact EFI payloads and
limits changes to primary FSInfo, FAT copies, root directory and new NVVARS
allocation. Default/reference/ACPI changed 887/892/887 bytes across 7 sectors
per lane. Pristine ordinary images were reconstructed after capture with the
archived actual ImageBuilder; they are not pre-QEMU snapshots.

## Evidence

All following paths are under artifacts/orchestration-20261001.

- `issue537-final-transaction/accepted.json` records the parent joint bounded
  acceptance after the separate common, native, raw and disk reviews.

- `issue60-537-common-independent-review-5d894c8/completed/final-join.json`
  SHA256 `B77168F7B8FCA1F1C5CF80BB32DA03DF57D77B4028B17452CE0020E21F274588`.

- `issue60-537-common-gate-5d894c8/run/result.json`
  SHA256 `424D4D891C07420715D1C2363BF0BFA5D502FF4595AD96EF47121501EA1D5D36`.

- `issue60-537-common-gate-5d894c8/markers-5d894c8.json`
  SHA256 `48186CE05B2CEBB5A2A2DFD1D20B16BA3308FD260CA856BC88E28E84A56ADD2A`.

- `issue60-537-guest-5d894c8-20261004T1656514158139Z/capture-result.json`
  SHA256 `1591B53809D0897219C316DBFAB7AF7054390096D3749E83417B17D4B1C9E19D`.

- `issue60-537-captured-raw-independent-review-5d894c8/review.md`
  SHA256 `3ED864BA197DAB632AC15E4D884083373FE43E6A387C08A7E9C9D8741ABD5825`.

- `issue60-537-captured-raw-independent-review-5d894c8/result.json`
  SHA256 `AB33B1CC5419725C945DADD4767AEC9D64B35D578413A79C7AFC03DFF87FF1DA`.

- `issue60-537-captured-native-independent-review-5d894c8/review.md`
  SHA256 `D1A04D51CDE00D4878A0C9810576990C52CA9BA4B98409DBCCACFF2F4EEF05F9`.

- `issue60-537-captured-native-independent-review-5d894c8/result.json`
  SHA256 `3524D843CCB44CD68F9FEE4FE762D5E1EFDC57084B675B8263FAEBDD77F6C842`.

- `issue537-539-pristine-5d894c8-20261004T1703166718707Z/result.json`
  SHA256 `67CA5CDB1550AF826D25F01F6B87390A87F064736EF9AF1050B7F80F12E0A16A`.

- `issue60-537-actual-disk-review-5d894c8/actual-review-results.json`
  SHA256 `8603BC02FF48F9C012B93AB4755F5B96933FA9CBB57D80CCA06BF7D9362FF515`.

- `issue60-537-disk-independent-review-5d894c8/review.md`
  SHA256 `9EE830A0D98B534DAAEB2ADF8AB29BF08685923144E68D67D8F2C0A4C9B17149`.

Reference serial SHA256:
`38CD509BFF0A1CD591102A2EEE83A195CBE2D8CC056F5A7F70C8A606BCF90A94`.
Reference EFI SHA256:
`5CCA886E4676FC4763B561E2F3ABF951F4475D936819E0732B793CAD9DBF4450`.
Capture helper manifest SHA256:
`247F39C03644E6F9FEDCFC293BE5C151061B3DDEB35663F306DF859082C8DBB9`.
The native output/input manifests retain 58 review outputs and 2123 unique inputs;
raw provenance retains 1892 unchanged inputs and 1796 captured archive records.

## Preserved failures and corrections

Step979 preserves the earlier source/focused failures and exact repairs. The
current ordinary review's first native WinPS process failed to resolve
Get-FileHash because of inherited PSModulePath; the 660-byte stderr/native 1
is retained. Retry changed only the process environment to native module
paths; no parser or grammar changed.

The first native join stopped at ACPI selected metadata inequality. The finite
approved rebase proves five selected types, 116 signatures, 8263 IL instructions
and 1659 named token operands; 22 added sentinel declarations explain token/RVA
shifts. Non-token bytes, attributes and layouts remain equal; no arbitrary
metadata is ignored. The unsupported function-pointer decoding and two
proposal path-selection failures remain recorded before the accepted join.
The final comparator also adds explicit type-cardinality/order checks.

The disk binding draft's backslash/bell typo was caught and corrected before
any auditor invocation; original draft/diff are preserved. Only seven actual
bindings changed; all 12 audit functions are unchanged. The successful run is
not a relabeled failed attempt. Common-preparation path-correction history
also remains intact. No historical failed result was overwritten.

The parent's first final naming wrapper incorrectly treated an unset
LASTEXITCODE as a failed native command after the repository script emitted
both passing markers. That wrapper's exit1/error is preserved as naming.*.
The unchanged repository script was then invoked directly as a PowerShell
process, using the established recorder; naming-direct.* records its actual
exit0 and empty stderr. No naming or source-firewall rule was changed.

## Deferred work and evidence boundary

Acceptance is limited to the noncollecting exclusive retained-page reference
profile and synchronous modeled local graph. General WriteBarriers/collecting
GC, root enumeration, concurrent barriers, wider-vector/unwind support,
external firmware/interrupt stack effects, transient overwritten-and-restored
sentinels, whole-stack peak usage, real ExitBootServices, hosted .NET/Kestrel,
cloud and filesystem conformance remain unproved. Production numeric replay
is not an independent numeric/RAM oracle. Gates3/4/5 and M04-EPIC remain
incomplete. Tracker #79/#537 closure and subsequent #80 readiness are separate
parent actions. The separate #541 entropy wave and its interrupted CRLF-byte
common run supply no evidence for this milestone.
