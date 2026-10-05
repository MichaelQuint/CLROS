# Step 997 - Include the GC profile in EFI output-authority controls

The complete combined regression at `9cf1607e180212255b831c92e4f61d96266f9fb1`
failed in `m01.034 owns output, log, and verifier file authority`. The existing
fixture still expected the checked-reference switch alone in two literal
producer contracts. The actual producer correctly uses checked-reference OR
GC-static because both profiles require the same reference-helper object.

Update those two exact expectations and run the unchanged file-authority
controls for three explicit vectors: default (5 output leaves, 8 verifier
inputs), checked-reference (6/10), and GC-static (6/10). All overwrite, stale
output, held-read-lock, hardlink and reparse-path controls remain intact.
No runtime, native source, project closure, helper bytes or producer behavior
changes. Changed test: `tests/OS.HostTests/BuildEfiShellPreflightTests.cs`.

## Checks and evidence

A narrow diagnostic extracted the existing PowerShell wrapper verbatim from
the updated C# method and checked all 13 required producer text contracts. It
executed that actual wrapper for all three modes: exit 0, empty stderr, exact
5/8, 6/10 and 6/10 success records, then `M01_OUTPUT_AUTHORITY_PASS`.
Artifacts: `artifacts/issue548-output-authority/targeted-existing-wrapper.ps1`,
`source-check.json`, `targeted.result.json`, separate stdout/stderr. This is
focused diagnosis, not a substitute for the original complete initializer or
full HostTests suite; the subsequent full rerun includes every original case.
`git diff --check` passes. A search found no other remaining copies of the two
stale checked-reference-only producer expectations in HostTests.

The failed full run remains at `artifacts/issue548-efi/final-common-run/`:
08:35:42.393817..08:57:40.613995 UTC, exit 3762504530, unchanged sources and
launch binaries. Its output records one failed case plus the m01.020 aggregate
failure. Do not call that run a pass or discard its logs.

## Native acceptance and remaining work

The parent completed the GC first-party native review before this compatibility
fix: `artifacts/issue548-efi/native-stack/native-acceptance.json`, SHA-256
`BDB2EA16BD2F499458A8A2734F4DFFF747A1F7E110995EA133497C8AA42B4584`.
That review covers the exact image and all 112 raw publisher inputs at 9cf1607,
540 emitted method extents, genuine field/metadata/helper associations and the
132-method reachable graph. The first-party bound is 10,056 bytes, with GPR/
XMM6 preservation and matching PE unwind records. Original firmware console/
serial body depth remains excluded. NativeAccepted is true; CommonAccepted
and FinalGuestAccepted remain false. This fixture/doc-only correction leaves
those native inputs unchanged; verify that association on the next clean head.

Next: the corrected clean-head solution build and one complete original suite
for the joint EBS/GC merge wave, then fresh ordinary default, typed-EBS and
GC-enabled QEMU acceptance. Issues #61/#546/#81/#548 remain open; #62/#82 are
not yet promoted. Gates 3/4 remain Fail. No collection, real ExitBootServices,
general runtime, hosted .NET/Kestrel or cloud readiness is established.
