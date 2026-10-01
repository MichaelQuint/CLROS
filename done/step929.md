# Step 929 - M04-004 host and fresh guest acceptance

## Summary

The opt-in M04-004 arithmetic profile executed the fixed 57-vector catalog in
a fresh NativeAOT EFI image under QEMU/OVMF. A separately rebuilt default image
emitted none of the three opt-in M04 records. The full host gate passed against
the same unchanged source. This completes the local evidence for #507 and
#73; live closure follows the reviewed PR #509 merge.

## Why

Host tests and a successful EFI link do not establish guest execution. This
step binds the guest result to its source, tools, image, sidecars and serial
transcript. It also records a rejected acceptance-helper attempt and the
reviewed correction, and fixes #74's unrelated generated networking criteria
before that issue becomes eligible for implementation.

## Files changed

This evidence commit changes `README.md`, `docs/efi-build.md`, `docs/qemu.md`,
`docs/nativeaot-nostdlib-limits.md`, `docs/completion-gates.md`,
`.gitea/backlog/clros-issues.json` and this step. Implementation remains in
commits `975440bbda0a8e1ff0dd30ed2c537eeefb8bd7ec` and
`07da84eb0305b680d43f72f4fd690d5ed6fe3e18`, described in steps 927 and 928.
No generated artifact or ignored acceptance helper is committed.

The manifest records M04-004's accepted evidence and desired closure. The
M04-005 correction changes only labels (`type:docs` to `type:implementation`),
implementation notes, acceptance criteria and evidence boundary. Its dependency,
open state and blocked label remain intact until #73 actually closes.

## Tests run

Settled implementation head: `07da84eb0305b680d43f72f4fd690d5ed6fe3e18`.
Tree: `2ae3f2e0948a6a14088de5b614ee5396fb974956`.

- `dotnet build .\CLROS.slnx --nologo -m:1`: passed, zero warnings/errors.
- Enabled EFI preflight: passed, 68 compile items and 81 source inputs.
- `dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build`:
  exit 0, 3,516 starts, 3,517 passes, zero failures, exactly one final
  `TEST_PASS host.tests`. Every start had a subsequent matching pass. The run
  began at 20:00:28.359 UTC and ended at 21:00:00.240 UTC on 2026-10-01;
  stderr was empty and HEAD/tree stayed unchanged and clean.
- Fresh default and arithmetic QEMU/OVMF matrix: both wrapper invocations
  exited 0 with `TEST_PASS qemu.smoke`. Each invoked
  `scripts/run-qemu-smoke.ps1` with `-BuildEfi`, a 30-second timeout and
  distinct EFI/image/serial/summary paths. The arithmetic lane explicitly
  enabled all three M04 properties. Exact argument arrays and timestamps are
  archived in each lane's `command.json`.
- Independent review accepted the complete host and guest tuples: all 24
  archived artifact hashes/lengths, both FAT image EFI payloads, PE/link
  identities, all 81 source hashes, 68-item compile aggregate, exact serial
  markers/order, default absence, tool identities and source cleanliness.
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File
  scripts/sync-gitea-backlog.ps1 -Offline` and `-SelfTest`: passed with all
  345 manifest entries valid. Logs are `step929-backlog-offline.log` and
  `step929-backlog-selftest.log` in the evidence root. Naming/source-firewall
  checks and `git diff --check` also passed.
- Independent final review found exactly seven intended manifest field
  changes across M04-004/005 and no other manifest changes. The M04-005 entry
  matches the reviewed correction. Only the six intended documentation and
  manifest files plus this step differ from the tested implementation head;
  no source, test or script changes occurred after the full gate. No
  substantive review findings remain.

The arithmetic runner enforced 17 required, five ordered, three unique and
five forbidden marker constraints. Independent acceptance additionally required
each complete M04 line exactly once, M04-002/003/004 before memory-map capture,
one `OS_SMOKE_PASS`, and no failure marker. The exact new record was:

```text
CLROS_M04_004_READY vectors=57 fingerprint=45FDFA24DD72EA28 referenceFilesRequired=false hostConformance=false guestIntegrated=true gate4=Fail
```

QEMU was terminated by the runner after successful markers and its 750 ms
drain. Native exit code -1 is not a guest-requested exit. The #54 host checks
are covered by this full gate; unintegrated #510 and #74 work is not.

## Evidence

Ignored evidence root: `artifacts/orchestration-20261001/`.
Host logs/result: `runtime-wave-07da84e/`. The 455,485-byte stdout SHA256 is
`D6D51D6FFECC8F47171BE20E53D99BCC3ADF8FED7F56009A2B74B45E44D3DD54`.
Fresh guest evidence: `guest-07da84e-20261001T210936Z/`, containing
`default/` and `arithmetic/` lanes. Each archives EFI, link JSON/map/response,
serializer object, build/wrapper logs, rebuilt 64 MiB image, raw serial,
summary, commands and acceptance hashes.

| Profile | Artifact | SHA256 |
| --- | --- | --- |
| Default | EFI, 55,808 bytes | EBAC4F464E5E9E21ECB8830D57DD15BEA4FC9705B5CD1B5AD1F8F67F1FFA77FD |
| Default | Serial, 7,947 bytes | 0F16CC0FB978B3AE9555AA978408D857CDEFA70F989549C9BCE5086DB73041F1 |
| Default | Summary | B42011C183E64890061C464210532B1475FCF2E929DB42E4FBA9E5D31F097175 |
| Default | Image | BFEED949B12D15AAB9E6D56F3AF9994F808DD7A12B36B5FECAD6B3813192B134 |
| Arithmetic | EFI, 89,088 bytes | 621969060496EF58853FAB6799014A7E1D00A8910C13FC56B0083ECA85020109 |
| Arithmetic | Serial, 8,515 bytes | 0BC55D0331B587FEBC333283C4EC0F7F8F211B4747675C705511CF59D713AFFC |
| Arithmetic | Summary | 17C755BFDB606D322A0952B0CE35B4E4133E64F4098477D031C2B392C1DE0D8E |
| Arithmetic | Image | 82E2B743908531AE31F510EBF43E26D0E6C59A526CEAD86C17130A9CA528D5FD |

Both schema-three sidecars record 68 compile items and 81 source inputs,
with source closure hash
`A9748677402078D5407B80B93779A03F82833DC639EAEBC4ABE8A7266CCB6454`.
All three M04 properties are false in the default image and true in the
arithmetic image. Tools: SDK 10.0.302, ILCompiler/runtime pack 10.0.8,
linker 14.51.36260.0, Windows SDK 10.0.28000.0 and
QEMU 11.0.50 (`v11.0.0-12631-g54e84cdc7a`). QEMU executable SHA256:
`B396EB9B669F6282EC60F0D46E6ADDCA8C669992E67A34365BE44CD3CB97C9A7`.
OVMF SHA256:
`33090CC07675BAA5190D9F1E84BF5176B33BCBFA9BACAC522961150CDB6DBB2A`.

## Failures

The earlier `guest-07da84e-20261001T210053Z/default/` runner passed, but the
ignored v1 acceptance helper rejected OVMF's ANSI/Unicode menu after CLROS
returned success. Its arithmetic lane was never attempted. The rejected
attempt, raw logs, available sidecars and original helper copies remain
preserved. No production code changed to repair that helper assumption.

Independent review confirmed that `EfiEntry` returns after `WriteBootMarkers`.
The corrected audit retains exact marker/failure scans over the whole
transcript, checks printable ASCII across the CLROS interval, and permits
only newline-only output or the exact reviewed 6,947-byte firmware suffix,
SHA256 `2C81EEB05A6AD42343CC28466D54371849D0D8113A9737099ABC4ABB79E0F3E1`.
Summary tails containing real ESC bytes must match an independent
last-eight/nonempty/trim/240-character reconstruction from the audited serial.
Metadata control characters remain rejected. Controlled checks reject 22
corrupt transcripts and 15 corrupt summaries. Independent review also checked
18 line-ending/preamble/suffix combinations and both PowerShell hosts.

The accepted matrix's generic runner still reports 207 invalid bytes and two
invalid lines from the identified OVMF display output. The raw transcript is
not ASCII-only; the CLROS marker interval is. The first attempt is not
relabeled as passing. The corrected helper was reviewed before both fresh
profiles ran. Remote CI remains queued without a registered repository runner
and is not claimed to pass.

## Deferred work

Merge PR #509 at its exact reviewed final head, verify the resulting tree,
post/read back merge evidence, then close #507 and #73. Issue #54 stays open
for its genuine null-read guest adapter. Issue #510 remains a reviewed but
unmerged host page-table prerequisite. Issue #74 becomes the next runtime
leaf only after #73 closes. Its inherited marker-only QEMU command is
explicitly a legacy placeholder; later separately owned integration must
supply exact build/profile/artifact arguments.

## Evidence boundary

This proves execution of one fixed integer catalog in the recorded
pre-`ExitBootServices` image. Gate 4 remains **Fail**. Conservative signed
minimum/-1 division/remainder rejection does not establish CLR exception
delivery. No allocation, GC, general runtime compatibility, live page fault,
firmware exit, hosted .NET/Kestrel or cloud readiness follows. The all-backlog
goal remains active.
