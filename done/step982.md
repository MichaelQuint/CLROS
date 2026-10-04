# Step 982 - Bind entropy fixtures to committed LF source bytes

## What changed and why

After step980 was committed as 67dbe7e, independent review found that one of
918 collected source/catalog inputs differed from its committed blob. The new
FirmwareEntropySeedCaptureProbeAuthority.cs had six CRLF sequences in the
working file; Git stored LF. Because the EFI source manifest hashes raw bytes,
the earlier focused/native evidence did not establish the committed input
identity. A clean Git status alone was insufficient.

The authority now has the exact 135-byte committed LF content. Three legacy
EFI fixtures now expect the raw aggregate derived from that content. C# text,
89 ordered Compile items, all other 102 raw inputs and every test comparison
remain unchanged. This is a reproducibility correction, not guest acceptance.

## Files changed

- Physical line endings in src/OS.Kernel/Diagnostics/
  FirmwareEntropySeedCaptureProbeAuthority.cs; its Git blob already has LF.
- One aggregate literal each in M02IdtrContinuedExecutionEfiIntegrationTests,
  M02OwnedIdtGatePopulationEfiIntegrationTests and M02Ud2VectorStateEfiIntegrationTests.
- This evidence step. No catalog dependency, issue state or gate is promoted.

Step982 was reserved through scripts/new-done-step.ps1 -Reserve at
2026-10-04 17:19:40.042617 UTC using an explicit coordination view containing
committed step980 and already reserved step981. Allocation evidence is under
root artifacts/orchestration-20261001/issue541-eol-correction/allocation-view.

## Tests run and evidence

The frozen four-path diff and before/after files are in the entropy worktree's
artifacts/issue60-firmware-entropy/source-checkpoint4. Author report SHA256:
57E356A9F15BEF1173E506C95692AA2514D6E6F077B188E653AF59553E511EC1.
Independent derivation reads the actual 89 Compile items and 14 fixed inputs:
103 raw inputs, with only the authority changed. git diff --check passed.
This correction has not yet completed a fresh build or full suite.

- Previous authority: 141 bytes,
  DBAD1416F401C79E50281A449962670B78871D7956C5B2980AF7E2B4DF77C241.
- Committed/current authority: 135 bytes,
  D12A2489A0254EF040A8B67C383D8D4A4F3DA941FEAA02717A824AABB9F4AB96.
- Correct raw103 aggregate:
  ef0707ee508c75be885ca097a159b7752af9ffc7eeb2d8ebd9ba9c41dfc07808.
- The original audit is root artifacts/orchestration-20261001/
  issue541-committed-source-byte-review/review.md, SHA256
  CBE3E356AA54EF77A49012DCFCBB265B60AAE23EE380A608E3958E7D07DDEA00.

## Failures

The full 67dbe7e run started at 16:51:19.014685 UTC and was deliberately stopped
at 17:13:02.056164 after the raw-byte mismatch was found. Only its verified
owned process tree was terminated. The collector records native exit 1,
empty stderr and unchanged inputs/binaries; it is an interrupted run, not a
passing common gate. Its streams and result remain under root
artifacts/orchestration-20261001/issue541-common-gate-67dbe7e-wt/run.

Earlier focused/native evidence remains attributed to its actual CRLF inputs.
No old manifest is rewritten. A stale inventory pointer in checkpoint4's
held capture association is corrected additively; source hashes are unchanged.

## Deferred work and evidence boundary

Before the next run, verify every collected source/catalog byte against the
staged and then committed blobs, compile the corrected commit and bind all five
launched DLL versions/hashes. Run the complete unfiltered suite; expected
3713 starts/3714 passes/zero failures/16 READY are expectations only. Fresh
publications must be bound to the LF manifest, followed by actual default,
provider and missing-provider guests with independent native, serial,
lifecycle and disk review. Held helpers require explicit new commit/source
bindings before release. #541/#60 remain open; #61 remains blocked. No real
ExitBootServices, entropy quality, hosted .NET/Kestrel or cloud gate is proved.
