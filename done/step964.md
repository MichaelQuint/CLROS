# Step 964 - Accepted bounded String guest allocation

## Summary

Accept the bounded M04-009/#78 and separately owned #528 String milestone on
clean source `be2762fe5c648592d049b9c49ee2844f1eb87f8d`, tree
`085082d7ca8b79943db9e7ed40ae63e800750232`. The complete original host suite and
seven fresh QEMU/OVMF profiles pass independent host, source, native, raw,
lifecycle and disk reviews. Parent joint acceptance follows those reports.
No production source changes for this evidence step.

## Why

Step963 recorded source and focused compiler evidence, with actual guest
acceptance still pending. This step binds the completed common-head gate and
fresh observations. Default/fixed/mixed-array regressions retain the shared
allocator boundary. The same host suite includes #58's memory-map fixtures and
#529's complete milestone-policy module; #58 still requires its separate
native observation, and #529's live milestone update remains a post-merge task.

## Files changed

- `.gitea/backlog/clros-issues.json`: only M04-009.currentEvidence and the
  M03-001.currentEvidence common-host gate update.
- `docs/completion-gates.md`
- `docs/efi-build.md`
- `docs/nativeaot-nostdlib-limits.md`
- `docs/qemu.md`
- `docs/repo-audit.md`
- `done/step964.md`

All other catalog fields, desired states, ownership notes and dependencies
remain unchanged. Earlier steps, including step963, are immutable. The source
head above identifies the evidence run; this later documentation transaction
does not relabel its pre-run source inventory.

## Tests run and actual observations

- `dotnet build .\CLROS.slnx --nologo -m:1`: native 0, empty stderr, exactly
  zero warnings/errors, 2026-10-03 20:28:03.5018215–20:28:32.5513030 UTC.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`:
  native 0, 3,653 TEST_START, 3,654 TEST_PASS, zero TEST_FAIL and one final
  `TEST_PASS host.tests`, 20:31:30.8695053–22:05:18.4352288 UTC. All 471,363
  stdout bytes, global marker Counters/order, 251 selected pairs across 31
  changed modules and 15 tracked READY records were independently checked.
  The source-bound 17 new pairs are #58's eleven and String EFI's six.
  All 511 source/catalog inputs match the committed run head, and all three
  post-run Debug DLLs equal their launch hashes and ProductVersions.
- The complete fixed and array initializers each passed all six groups;
  all eight previously deferred NativeAOT publications are associated with
  this actual run and have zero OS AssemblyRefs. No duplicate publication
  was needed for independent review.
- Seven sequential fresh captures ran 22:34:29.870553–22:43:19.925056 UTC:
  default, fixed, mixedarray, string, string-beforebind, string-exhaustion and
  string-invalidlength. All 13 capture commands return native 0/empty stderr.
  All 4,152 archive entries, current artifact/tool/Debug builder identities,
  36 requested/evaluated flags and ordered 84 Compile/97 raw inputs are bound.
  True-flag counts are 0/8/9/10/11/11/11. Smoke source is separately pinned.
- Whole raw fixed/array/String streams contain 78/68/72 records; the three
  negatives contain 49/65/50. Their exact 304-byte OVMF preamble, CRLF/ASCII,
  order, fields and EOF pass unchanged independent auditors. Default's whole
  7,947-byte returning stream equals the accepted baseline, including firmware
  suffix. String positive observes five strings among seven allocations,
  used200/count7/constructorCount6, exact UTF-16 contents/terminators/padding
  and retained prior objects. Exhaustion adds 161 empty plus one length-three
  string, reaches used4096/count169/constructorCount168, then status 15 without
  mutation. Before-bind status 5 and full-width 4294967297/status 18 retain zero
  used/count/constructors and unchanged storage.
- Four ordinary lanes bind native wrapper 0 and signed QEMU exit−1 with owned
  `runner-terminated` disposition. Their 750 ms drain is configured, not a
  measured duration. Negative observers independently measure
  754.7916/764.772/755.3515ms quiet, unchanged candidate/final bytes and owned
  termination before deadline, with no timeout/spontaneous exit/cleanup error.
  These intentional TEST_FAIL endings remain ordinary runner failures; no
  fictitious ordinary summary or expected-panic success is assigned to them.
- Native review maps 415 selected method bodies and all 28 complete allocation
  exports. All seven OS.dll files have zero AssemblyRefs and exact export
  signatures. Genuine NewString -> String__Ctor -> RhNewString supplies the
  authentic MT and preserves returned RAX; UTF-16 readers use the returned
  reference. The direct invalid-length route retains the full signed64 value
  4,294,967,297 and has no retained constructor. Fresh joins bind 26 observed
  full 24-byte prefixes: fixed 10, array 6, String positive 5 and exhaustion 5.
  Before-bind/invalid-length each bind two attempted tokens only, with zero
  observed prefix words. Current source retains one authority/binder/cursor
  and commit, with full 80-byte state/4,096-byte page rejection comparisons.
- All seven linked EFI images/maps equal accepted static evidence. Only
  fixed/array also have whole managed-DLL/COFF equality. The other five use
  fresh managed metadata and matching nondebug COFF contents, symbols,
  relocations and unwind records; changed .debug$S is not whole-COFF identity.
  Parent additionally verified all 3,240 compared section-header virtual-field
  pairs as equal zero bytes. Host inspectors do not execute target images.
- Disk reconstruction records 28 native 0/empty-stderr commands using each
  lane's archived Debug ImageBuilder, followed by one independent native 0
  audit (57,832 checks). Pristine and booted EFI payloads exactly match archived
  EFI bytes. All three negative PREBOOT images equal reconstruction. Booted
  images instead contain the precisely accounted 1,393-byte/three-cluster
  NVVARS addition and associated FAT/root/primary-FSInfo changes. Default has
  887 changed bytes in seven sectors; the other lanes have 892, with seven
  sectors except invalid-length's nine due to both FAT copies crossing the
  639/640 entry boundary. Backup FSInfo retains pristine advisory hints.
  No postboot/pristine equality or whole-filesystem conformance is claimed.
- `scripts/check-naming.ps1` passes native 0/empty stderr at
  22:11:54.905904–22:12:34.333553 UTC, with exactly two TEST_START/two TEST_PASS
  records for naming.guard and source.firewall. Evidence is
  `issue528-final-checks/naming-be2762f/`; stdout is 100 bytes, SHA256
  `F859FFF4688D92C662BD161F4DEDF4C64778B133B7ABDBED70D634D8A2F4B651`.
- Actual final WinPS `-NoProfile -NonInteractive -ExecutionPolicy Bypass -File
  scripts/run-qemu-smoke.ps1 -DryRun` passes native 0/empty stderr at
  23:19:48.335815–23:19:50.709292 UTC, with one qemu.runner.dryrun START/PASS and
  workload.profile.tooling PASS. This is configuration evidence only.

The new step was reserved using explicit Windows PowerShell ExecutionPolicy
Bypass and `scripts/new-done-step.ps1 -Root C:\Projects\CLROS -Reserve`,
native 0. Post-edit transaction checks/review belong to the parent and are not
represented here as another whole-host run or guest execution.

## Evidence

All paths below are under `artifacts/orchestration-20261001/` unless stated.

- `issue528-joint-acceptance-be2762f/acceptance.json`:
  `4D9EDB9789D7F3251942B8D8AF6D91793548A656D0F44F54DD0CB302027F4E44`.
- Joint `review.md`:
  `60CC4BCB4C5CC3E91C60F7C4CBD02CCD6945345393F8EF6C4F16A7BB796978F3`.
- `issue528-build-be2762f/result.json`:
  `3D65703ED292692830C7F3EC05B1AB181E91A8D1A806167BB9D83B1452E778D5`.
- `runtime-next-wave-be2762f/result.json`:
  `591ADB7FCAB5E18F8B75DD00730EA9169337BD4681830F52E24D1AE37A95CAA1`.
  `host-tests.stdout.log`:
  `4AE1F1EDCF39CEA9B4C942C2FE7A8131C21B17ADFE4C56625D0F2F46345962B7`.
- `issue528-guest-be2762f-20261003T2234301007068Z/capture-result.json`:
  `D29C1414FF327669EEF5C3BC7A834593DB538559C4D5FFE4235460743E489671`.
- `issue528-pristine-be2762f-20261003T2246510918206Z/result.json`:
  `ED9325CF38F9600CE6F5E5D6D51B4209FF732E458C2E9A8AC4D25E7D07737120`.
- `issue528-final-dryrun-be2762f/result.json`:
  `7BB03931FF39B8CF372104571D4C149AC8A89566D9E423A9BDAFF94CB14CD369`;
  stdout 5121 bytes:
  `BEB006E54268B5B7B5DEA5760E639702F3F47A5B4404B1E07713BD42F20423F2`.

Independent acceptance records:

- `issue528-completed-host-independent-review-be2762f/review.md`: `C002A320642985E40736AD27C5DBA0E90EE990A4E8218044D3E0074F3987830F`.
- `issue528-host-final-pins-be2762f/completed-check.json`: `52D0AEB84C625270D2787FDB0300096ECAF2A2CBCE9993E9C6DD2A728110ECE6`.
- `issue528-captured-raw-lifecycle-review-be2762f/review.md`: `FCFED3042C73CCFD915B273B96EBD8CF4808A79FB28CA36543DB71DC6C68106D`.
- `issue528-captured-native-review-be2762f/review.md`: `6FAF894FF1EC940BDF7CF6875386254167B32A02ABDB6D1D50BB62B752A7D267`.
- `issue528-captured-native-review-be2762f/review-result.json`: `C7A07D168877482350B278F0BE9479D584FC9ADD05EBBE798DC1B21A289E359D`.
- `issue528-actual-disk-review-be2762f/review.md`: `0C77E0C5371704A22255F92796ABA2C487ED16B314502A26D6C5CA72664D9DA5`.

Exact EFI and serial byte identities (SHA256):

| Profile | EFI bytes / hash | Serial bytes / hash |
|---|---|---|
| default | 55808 / `FDECE5DD4DBB61C63442099DCB1E7AEBD9EA4A3D6903C631A74C3F7134E9A703` | 7947 / `0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1` |
| fixed | 324096 / `2024E25C5E541C9FFFE4D81701F41C562768E0B5E434EFDE5FC60C34953D3F6A` | 10205 / `C2A530084B94B3740DE917B037963E6E7DA242987DE7A476751A612C547E1A83` |
| mixedarray | 325632 / `DC9719E4104400ED367BAC065D05AA4D76FA4EC6FD808912E4FB1CF8F5567F67` | 8235 / `826ECFE4E13A9FB21EC7AB2B35E29EE03B02D67B33D7F1AB7ECB5F094CDEB251` |
| string | 328704 / `CC90B842EFF45843A1F17DAC7811844B6F9899F203ADC1B1697C387CE4914AAF` | 11208 / `623E5087C85615CE62E57AE8396B3839B185511872B9B360C99C07717BA570F6` |
| string-beforebind | 321536 / `CDF25E60922C0AC056AFFE7EFBAE15D18D8377660504819B5782F7DE4C080125` | 3870 / `03C1D50C9AD487173815FA93939A83BB8E135A037CEA826889BDD241F0DC702A` |
| string-exhaustion | 329728 / `863C1E6B7246F49B59B957BE92E9F6BCB628A4FFE8D72B35EDC23F54BE4B4350` | 10912 / `DC94A54E56E67344293B425ECE984B5FADD6E82ACD4670153C57B4B9C1CA1210` |
| string-invalidlength | 323584 / `D729C4B8C817291259E0A90A572B21EAA8A400D256C6F00F3BF3922CEE3B15B1` | 4238 / `A4552B80753A822E93882185A279ACA22253FF36AA28E81D5CD662E17830A5AC` |

The joint record supplies final bounded acceptance. Original capture and
individual provisional GuestAccepted=false records remain unchanged.

## Failures

No failure occurred in the accepted be2762f build, complete host run, seven
capture executions or disk commands. Earlier product/fixture attempts remain
failed and immutable in step963 and its source history; they are not relabeled.
Reviewer-only failures are preserved: a COFF schema diagnostic, an absent
invalid-length constructor lookup, the ABI extractor's before-bind Bind
assumption, and provenance enumeration including dotted directories. Narrow
reviewer corrections passed on immutable captured bytes without product edits
or guest reruns. Earlier allocator discovery was blocked by the default shell
execution policy before mutation; explicit WinPS Bypass succeeded. This step's
explicit reservation succeeded on its first attempt.

## Deferred work and evidence boundary

Parent review, commit/PR merge and exact tracker readbacks follow this evidence
transaction; this file does not claim those actions have occurred. #58's
separately owned native memory-map observation remains pending; the copied
map key is opaque here, with no stale-key/EBS retry proof. #529's state-only
live M02 milestone closure and read-only convergence preview remain post-merge
tasks. Future M04-010 barriers require their own declared scope and evidence.

Only the bounded noncollecting retained-page String/fixed/array experiment is
accepted. General String/BCL behavior, interning, GC/roots/write barriers,
refill, real ExitBootServices, hosted .NET/Kestrel, live storage/networking,
provider/cloud/production readiness and whole-filesystem conformance remain
unsupported. Gates3/4 remain **Fail**.
