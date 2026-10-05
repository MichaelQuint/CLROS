# Step 986 - Accept bounded EFI entropy retention and missing-provider evidence

## What changed and why

This step accepts the clean 8942ab7839985a4f0c39a5f790d38742bb0ffb01
source tuple (tree c4d8592ec1cae23779e1c361be7c22a0510a1823) after the
complete common suite and fresh default, provider-present and provider-missing
QEMU/OVMF captures. Steps978/980 supply the host contract and opt-in EFI
implementation; step984 preserves the failed capture and exact framing repair.

The live positive path discovers EFI_RNG_PROTOCOL and calls its original
GetRNG pointer once for 32 bytes. The private native path copies and verifies
retention, clears the requested scratch span, checks guards/tails and publishes
last. The same enabled EFI bytes with the missing-provider configuration
observe EFI_NOT_FOUND, zero acquisition calls, unchanged retained storage and
unpublished state. No seed bytes or derivative are logged.

## Files changed

- docs/boot.md, docs/efi-build.md and docs/completion-gates.md: accepted evidence
  and remaining firmware-exit/runtime boundaries.
- .gitea/backlog/clros-issues.json: request M03-003 done and M03-004 ready after
  reviewed merge and tracker readback, retaining their scoped dependencies.
- This reserved step. No product, test or build-script bytes change here.

## Tests and commands

The clean-head solution build returned 0 with no warnings/errors. The complete
`dotnet run --project ./tests/OS.HostTests/OS.HostTests.csproj --no-build`
ran 2026-10-04 20:38:27.206028-23:25:51.879271 UTC, native exit0, empty stderr,
3,713 TEST_START / 3,714 TEST_PASS / zero TEST_FAIL / one host.tests terminal /
16 READY lines. Independent review checked exact names, occurrence order,
918 source identities and all five launch DLLs. DLL hashes were recorded before
launch; ProductVersions were first observed after launch at 20:40:57.938079 UTC.
No prelaunch-version claim is made for this historical run.

The parent capture command and complete argv are retained in
issue541-parent-guest-execution-8942ab7/command.actual.json. It ran
23:43:40.079721-23:47:58.006172 UTC, returned 0 and had empty stderr. The
ordinary default and enabled wrappers build EFI and run the existing smoke
path with actual QEMU/OVMF identities. The enabled wrapper uses
EnableM03FirmwareEntropySeedProbe and FirmwareRngSource virtio. The missing
lane builds a distinct disk from the same enabled EFI and invokes the accepted
capture-only observer with the exact missing-provider argument vector.

Independent complete-transcript results:

| Lane | Raw bytes / owned records | Result |
| --- | --- | --- |
| default | 7,947 / original default envelope | ordinary smoke passes |
| entropy | 1,508 / 27 | one discovery/acquisition, 32 retained/cleared bytes |
| entropy-missing | 1,117 / 19 | EFI_NOT_FOUND, zero GetRNG calls, unchanged retention |

Positive framing uses the pinned 304-byte preamble and exact 251-byte suffix
through EOF. Missing framing ends at its unique failure record with no suffix.
The observer measures 764.127 ms quiet while the same QEMU process remains
live, then terminates it and verifies EOF/cleanup; exit is -1. The ordinary
streamer still rejects the negative TEST_FAIL. Ordinary positive/default
captures have configured 750 ms drain, without a measured-quiet claim.

All seven complete native/managed files per lane match accepted current-common
publications. All 103 source inputs, 89 Compile items and 39 flags match.
The only sidecar differences are exact output paths and publication timestamps.
The missing lane retains the positive publication lineage and 117 identical
archive files. This reuses the accepted 112 default/123 enabled method proof
and 2,336-byte local branch bound; arbitrary firmware depth is excluded.

The parent ran 12 original image-accounting commands at 23:54:20-23:54:32 UTC.
Independent read-only review passed 22,493 checks over 2,327 inputs. Complete
EFI payloads, GPT headers/arrays/CRCs, FAT copies/chains, FSInfo and every disk
change are accounted. Changed bytes are 887 / 1,876 / 887 respectively.
Observed NVVARS sizes are 1,393 / 11,437 / 1,393 bytes (3 / 23 / 3 clusters).
Ordinary pristine images were reconstructed after capture. The negative lane
also retains its actual prelaunch pristine disk, equal to reconstruction.

## Logs and evidence

All following paths are below root artifacts/orchestration-20261001:

- issue541-common-gate-8942ab7/run/result.json:
  E3D582788D5871D383611A7A6BD4F77D86D7498F49A71EDA86206E2A025C4AEC.
- issue541-common-independent-review-8942ab7/completed/final-join.json:
  9C53F7ABE2AC6129FD0D21BA3290126DC4C7F0FF36CDAABEA1FA3C28E5D6B489.
- issue541-guest-8942ab7-20261004T2343403091420Z/capture-result.json:
  FD4318629B114147BC39799A7722823021BB2822A7AD0F292FFDEB0E994148C5.
- issue541-captured-raw-independent-review-8942ab7/result.json:
  DBFD52339CC00AFB1181925338DB5581F39F29FFA4D319D4A09EB473B662F0E1.
- issue541-guest-native-association-8942ab7/result.json:
  4D07B344DBC59CBEB5576D9391B374CED5ABFDD63F4F891A4B18250AD09DDCF7.
- issue541-disk-independent-review-8942ab7/actual-review-results.json:
  65E07AC467C9347833AB076EBF2CDA8273B00618A2E3BEAC8176FB2E5529920D.

Default EFI SHA256 is
FDECE5DD4DBB61C63442099DCB1E7AEBD9EA4A3D6903C631A74C3F7134E9A703;
both enabled lanes use
EF001E7A00107530593CCF38D7A67CC91944EA1728373FE3B6CD5D92791D44AF.
Default, positive and negative serial hashes are respectively
0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1,
06DC24CE1B95B50C9837F10FF3EF8FD4600923C3342A8B1681DA34C0157BB385,
3CD26E4887B41A9FA787528FC5C38DF2BC76315B38F443CD57363B43CD2C63A2.

Step986 was reserved through scripts/new-done-step.ps1 -Reserve; allocation
records are retained in issue541-final-acceptance. Subsequent documentation
and catalog validation records remain separate from the source tuple above.

The final catalog-only offline validation passed at 2026-10-05
00:08:28.817109-00:08:39.017073 UTC: 345 issue entries, 16 milestones and
68 labels validated, zero failures and no API writes. Naming/source-firewall
checks passed at 00:09:47.153893-00:10:54.573094 UTC with empty stderr.
Git diff --check passes. Independent final documentation review confirms all
917 other common-source rows equal tested and committed bytes; only the five
intended catalog fields and five declared documentation/catalog paths change.
The parent's scoped evidence join is issue541-final-acceptance/parent-acceptance.json,
SHA256 4259854BD8D0F1B081C3CB4FC880EC5EEA86509302FB3097D24F9772EF5A1BDC.

## Failures

The original real capture failed framing and remains failed; step984 records
the exact repair. It is not retrospectively accepted by this fresh tuple.
The first provenance review failed an exact Windows/WINDOWS path spelling
expectation. Its original script/result/stderr are preserved. The second
attempt changes only that expected executable literal to the actual recorded
Get-Command path, retaining exact arguments and tool hashes; it passed.
Raw/lifecycle checks were not rerun. Earlier native-review schema corrections
remain preserved with their original failures and corrected association.

## Deferred work and evidence boundary

The combined common, native, raw, lifecycle, provenance and disk evidence
satisfies #541 and the bounded pre-exit retention criterion of #60. Closure
requires the reviewed merge and issue-specific evidence comments/readback.
The next slice is #61's one-attempt typed adapter, followed by #62 ordering,
#63 retry and #64's first real firmware exit. No actual ExitBootServices,
post-exit random service, entropy-quality guarantee, physical secure erasure,
collecting GC, hosted .NET/Kestrel, cloud or production readiness is inferred.
