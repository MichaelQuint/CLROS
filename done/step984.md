# Step 984 - Recognize two exact observed entropy firmware endings

## What changed and why

The first fresh entropy guest matrix at fdc0611 reached all 27 CLROS success
records with the real virtio RNG provider. The enclosing smoke wrapper then
failed because its firmware-envelope fixture required a fully drawn OVMF menu.
The actual entropy capture ended after the two complete BdsDxe UiApp startup
records. The simultaneous default lane still contained the complete menu.

The positive entropy validator admits exactly those two observed suffixes:
251 bytes with SHA256
011BC86409F7A01C1494F57146920B37184A02063B7E995B44A99C6B083E6615,
or the existing 6945 bytes with SHA256
BFBB6B92A5EEE7728E1AA54A2A4D262135215D7D2F267696E508A23BD514C61D.
It binds the corresponding stream line counts, while retaining the complete
owned protocol, prefix, status/length equations, exact summary reconstruction,
native-process result checks and missing-provider EOF contract.

The regression retains the original full-menu fixture and all registered
groups. It adds both complete frames to the actual SerialMarkerStateMachine,
crossed summary/count checks, and rejection of altered, extra, incomplete and
truncated suffixes. No kernel/native source or raw103 input changed.

## Files changed

- scripts/run-qemu-smoke.ps1: two exact positive firmware-envelope alternatives
  and their matching stream counts.
- tests/OS.HostTests/M03FirmwareEntropySeedEfiIntegrationTests.cs: literal raw,
  lifecycle, summary and real-streamer regression controls.
- This evidence step; canonical command/status documentation as applicable.

## Tests run

The original whole five-module focused run passed 201 START, 202 PASS,
zero FAIL and one host.tests terminal on 2026-10-04. Focused build, focused
execution and normal Release HostTests build returned native exit0 with empty
stderr. Both builds reported zero warnings and errors. The batch ran
20:26:09.329938-20:32:44.337081 UTC; the focused run itself ended at
20:32:24.538542 UTC. All original initializer groups were retained.

The strict raw/lifecycle child passed 329 controls. Actual marker-streamer
execution over both complete framed fixtures produced full32/207/2 and
short31/15/1 line/invalid-byte/invalid-line counts. The mapped core passed six
groups plus its terminal; 49 profile commands produced 47 expected rejections
and two passing DryRuns. Both actual default/enabled NativeAOT publications
passed. All 922 recorded inputs, 14 launch rows and 487 recorded child files
were unchanged on readback. git diff --check passed.

Actual result: framing-repair-v1/attempt1/result.json SHA256
381654377FE1C7E159EF06E50CDDEADE053DEEE8F5D7B907DA2E62E5D9A96B0E.
Focused stdout SHA256
5B5BFE4CA8536162D5E0722174B3E1300566292BDA7A0832EAD9D9EE5FC84899.
The final report and exact child/publication identities are retained under
framing-repair-v1/final. These checks executed no guest.

## Logs and evidence

The failed matrix is retained under root artifacts/orchestration-20261001/
issue541-guest-fdc0611-20261004T1955382194651Z. Its parent execution ran
19:55:37.991202-20:00:05.253273 UTC on 2026-10-04, native1, empty stderr.
Default capture completed; entropy runner observed 27 ordered and unique
records without ordering/duplicate failures, then the wrapper reported
m03_entropy_firmware_suffix. Missing-provider capture did not execute.

The entropy serial had 1508 bytes: the exact304-byte prefix,953-byte owned
protocol and251-byte suffix. The suffix equals the first251 bytes of the
default lane's6945-byte suffix, ending before its first ANSI drawing byte.
This identifies the observed framing; it does not establish its timing cause.

Source checkpoint: entropy worktree artifacts/issue60-firmware-entropy/
framing-repair-v1. Two-file diff SHA256
02EA6DD3388A3B068A10A58789C2EF8EAB53F1972E1BB9E5B3394FB5F192868D.
The raw103 aggregate remains
ef0707ee508c75be885ca097a159b7752af9ffc7eeb2d8ebd9ba9c41dfc07808.
Parent checked both changed files against their effective Git-filter bytes.

Step984 was reserved through scripts/new-done-step.ps1 -Reserve at
2026-10-04 20:21:18 UTC using an explicit coordination view containing committed
step983. Allocation records are in root artifacts/orchestration-20261001/
issue541-framing-integration; no existing done step was overwritten.

## Failures

The first guest matrix remains failed. Diagnostic parser replay, if performed,
cannot retroactively change its native exit or count as a fresh successful run.
The released five-module regression batch had no failing stage.

## Deferred work and evidence boundary

The final-head complete common gate and a new default/provider-present/
provider-missing guest matrix remain required. Preserve the separately accepted
native analysis for its exact source/artifact identities, and bind new actual
publications before accepting guest evidence. No entropy-quality guarantee,
secure-erasure claim, real ExitBootServices, hosted .NET, Kestrel, networking,
cloud or production-readiness gate follows from this framing correction.

Issues #60 and #541 remain incomplete; #61 remains blocked until their complete
evidence and merge are accepted. #544 source was held during this repair and then received the exact two-file correction before its next validation pass.
