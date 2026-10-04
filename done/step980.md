# Step 980 - Guarded EFI entropy capture and native review

## What changed and why

The M03-003 retention contract had controlled host-provider evidence only.
Supplement #541 adds a separate opt-in no-stdlib EFI adapter before firmware
exit. It discovers EFI_RNG_PROTOCOL through the original system table and
boot-services function, then requests exactly 32 bytes once with a null
algorithm selector. EFI returns a full-width status; no returned-count
parameter or entropy-quality test is invented.

Complete state and buffer spans are admitted before writing. Distinct guarded
64-byte payloads hold scratch and retention. Each requested byte is copied,
read back, cleared from scratch and checked again; full scratch, retained tail
and guard verification precedes publication. Pre-copy rejection preserves
retention; late verification failure leaves it unpublished without a rollback
promise. Diagnostics expose only public status/counts and integrity, never
seed bytes, digests or a predicate over uncleared requested scratch.

The default-false exclusive profile and finite runner choices preserve old
profiles and device order. Both provider choices mask RDRAND/RDSEED; only
virtio adds the RNG backend/device. Success continues ordinary boot. Missing
provider remains a real TEST_FAIL followed by a terminal C# loop. No real
firmware-exit call is added.

## Files changed

- EfiEntry, OS.csproj and the new FirmwareEntropySeedCaptureProbeAuthority:
  numeric adapter and explicit default-false no-stdlib admission.
- EFI build/verifier/smoke scripts and QemuRunner Program/CommandBuilder:
  exact closure/profile guards, bounded marker protocol and RNG options.
- New M03FirmwareEntropySeedEfiIntegrationTests and complete QemuRunnerTests:
  mapped ABI/storage cases, strict raw/lifecycle and real streamer controls.
- Thirty-two existing compatibility fixtures: mechanical source/project pins,
  reciprocal profile guards and exact diagnostics; old groups remain intact.
- Canonical boot/EFI/completion docs, three M03 catalog evidence strings and
  this step. Catalog dependencies, labels and issue states are unchanged.

The exact 42 source paths and bytes are in the final owned42 manifest below.
This step was allocated with scripts/new-done-step.ps1 -Reserve in the isolated
author worktree at 2026-10-04 16:24:46.2101222-16:24:46.5641089 UTC, native0.
The parent keeps the separately running root checkout unchanged.

## Tests run

Author checkout: C:/Users/Roger/.codex/worktrees/m03-003-efi-entropy/CLROS,
branch codex/m03-003-efi-entropy, baseline
5d894c873723282b7b6ff4a0fdae5af6730f31b1. Evidence paths here are relative to
that checkout unless explicitly under the root review directory.

The actual command arrays are in artifacts/issue60-firmware-entropy/
source-checkpoint2/command-plan.json. The focused project runs all original
host entropy, platform random, UEFI-gone and QemuRunner initializers plus the
complete five-group EFI entropy initializer. Builds use Release, one node,
disabled build servers and UseSharedCompilation=false; no group is filtered.

- Focused build: 15:42:45.035161-15:43:01.048437 UTC, native0.
- Complete run: 15:43:01.057447-15:50:09.958098 UTC, native0, 201 TEST_START,
  202 TEST_PASS, zero TEST_FAIL, one host terminal and empty stderr.
- Initial normal HostTests build: 15:50:09.974171-15:50:31.478196, native0.
- Final normal build on the six corrected expectations:
  15:52:05.394590-15:52:22.232411, native0.

All three builds report zero warnings/errors. The run preserves all 928 input
records and 14 launch files. Its mapped child passes six groups/one terminal,
measuring State112, Buffer96, Protocol16, payload+16 and GetRng+8. It covers
real host backing, controlled original callback arguments, zero/repeated
successful bytes, rejection preservation and provider-write/error cases.
The raw/lifecycle child covers 302 cases. Production streamer controls admit
the full protocol and reject missing, duplicated or reordered entropy rows.

Default publication 15:47:23.0847299-15:48:44.5086185 and enabled publication
15:48:44.6310476-15:50:09.8340482 both return0 with verified source/profile
metadata and zero AssemblyRefs. Final closure is 89 Compile / 103 raw /
39 flags, with only the entropy flag true in the enabled lane. These are
publication and controlled-host outcomes, not target-image execution.

## Logs, evidence and review

Author paths under artifacts/issue60-firmware-entropy:

- final/report.md:
  8B20B81380904737D09D89816DB911CF1CAC68FA7C7A4FA93BFF5694E8BAC76F.
- final/identities.json:
  1ADD5E2F7A63CCE29D3C9FE859E6CF1193CF7759398AB5EFA2F6D63427C74C7B.
- final/owned42.json:
  E460346212ABD56122A081EE8E7FEAB68ED40D8E8665DEBBB528245BF7BC5C90.
- attempt1/result.json:
  FAD4F8E2A63BF3EDB91FD429591BF8A2A6413FF4032A54C35481D47398BA85EF.
- final/evidence-identities.json binds 2,599 records:
  54EEE8570A5563F1ECCC71BFCFE2A6B6A9F2D9F8733246F9586F5EE35136A13F.

Independent review under root artifacts/orchestration-20261001:

- issue541-final-source-review/checkpoint3-addendum.md:
  AEE358D1BD20D5351DD5A4CC0D99672281E0DE454D4C6CFC6193043E514DF744.
- issue541-native-independent-review/review.md:
  62A6647721554957F74D124AAF67C9D2B8C4EDE1CB2340381956603565A4A64F.
- Its result.json:
  CC6E21EA00FAEF052BA87B67598CB32D11601E16386EE682F69570C17BA506EB.
- Its 93-output manifest and 1,389-input manifest:
  E1D2447C7984D753C4581F0E3ABEBA03C20F60429C5476A0D3B6655EC33EBE69,
  FBE544B853ACE37C616EBD2069704CDA2B357C2048E160004DD7679BD2C45DD2.

Every relocated COFF method matches its actual PE body: 112 default and
123 enabled methods. Eleven entropy methods are absent from default. The
enabled EFI SHA256 is
EF001E7A00107530593CCF38D7A67CC91944EA1728373FE3B6CD5D92791D44AF;
default is FDECE5DD4DBB61C63442099DCB1E7AEBD9EA4A3D6903C631A74C3F7134E9A703.
Native review verifies original protocol pointers/GUID/arguments, full64
statuses, actual writable zero-fill state, non-inlined reads after stores and
clears, publication last, and the emitted EBFE failure loop. Fifteen local
methods and seventeen unwind records establish a conservative local depth of
1,936 bytes below Run entry, or 2,336 with its actual caller prefix. Firmware
callee depth, stack capacity, interrupt safety and guest outcomes are excluded.

Parent integration checks in this isolated checkout all returned native0 with
empty stderr on 2026-10-04: naming/source firewall (16:29:29.093320-
16:30:58.755639 UTC), ordinary QEMU dry-run (16:29:29.131770-16:29:33.702043),
offline catalog validation (16:29:29.077213-16:29:47.829212), and catalog
SelfTest (16:29:29.153851-16:29:48.575083). The catalog retains all 345 issues;
SelfTest passes 28 policy cases with zero actual API calls. Exact commands and
streams are under root artifacts/orchestration-20261001/issue541-integration.
The dry-run provides command construction only. No product source changed
after the focused/native review; only documentation/catalog evidence was added.

## Failures and corrections

No product command failed in the final focused/publication intervals. Early
source review corrected failure logging that classified uncleared scratch,
the controlled Locate callback's output-slot guard, explicit empty-option
rejection and device ordering before the frozen run.

After that run, five legacy canonical project hashes and one heap-conflict
diagnostic were corrected as six exact expectation literals in checkpoint3.
All 103 native inputs and all five executed modules stayed byte-identical;
the original passing results remain attributed to checkpoint2. Final normal
compilation covers the corrected bytes, but their six complete initializers
still require the full common suite. No assertion or group was removed.

Preserved reviewer/preparation failures include an auxiliary Compile selector,
a stale manual token, a missed balanced tail jump, a final-build container
shape assumption and a nonexistent standalone RunnerOptions filename. The
corrected readers and all original failed records remain in ignored evidence.
None is a failed product result or proof of a guest.

## Deferred work and evidence boundary

The final committed-head solution build and complete common suite remain
pending. Proposed expectations are 3,713 starts/3,714 passes and the original
16 READY records; those are not actual completed results. Fresh default,
virtio-provider and missing-provider guests must follow, with independent
source/native, strict raw serial, lifecycle and full disk accounting. The
missing lane must reuse the exact enabled image and preserve a real failure;
only its direct observer can prove measured live quiet and owned termination.

The native reviewer accepted bounded offline evidence only. All numbered
completion gates retain their existing status. #541/#60 remain open and
#61 remains blocked. No entropy quality, PRNG/TLS, physical secure erasure,
post-firmware ownership, real ExitBootServices, hosted .NET/Kestrel, cloud
support or production readiness is established.
