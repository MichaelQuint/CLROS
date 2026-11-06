# RC3 and WHPX qualification contract

## Candidate results

The frozen candidate is `2e81ee3a`. [Step1788](../done/step1788.md) records
the integrated Windows/WHPX notifier repair and its qualification boundary.
Fresh unfiltered Full passed 7,789 checks in 996 fixture groups (884 Fast and
112 Native), with 6,503 selected cases, zero failures and zero reuse. Independent
readback confirmed all 7,970 captured source inputs, 48 producer binaries and
eight runtime pairs. Original worker processes joined, and source remained clean
at the same commit through completion.

The accepted host review is
`artifacts/refactor-r01/combined-full-compat-r01/accepted-review-r01.json`
in the producer checkout, SHA-256
`2C05A550EDADB6D07828A493EA31182AD9E67B2C6A9999F92D5116AB084BFC2A`.
It binds the original results, producer freeze, terminal seal and independent
review. Compiler guest execution has started. The complete guest/browser and
matched performance matrix remains pending for this host recipe.

### Retained results on the earlier host recipe

The following results retain source `3f95496e` and its recorded launch settings.
Keep these original receipts when qualifying the changed candidate. The original
failures and subsequent repairs are recorded in
[step1786](../done/step1786.md) and [step1787](../done/step1787.md).

| Check | Retained evidence on `3f95496e` |
| --- | --- |
| Host | Unfiltered Full passed 7,786 checks in 996 fixture groups with zero reuse. Source, inputs and all 48 producer binaries passed readback. |
| Compiler and application | All 29 compiler contexts passed their expected outcome, including the expected unhandled panic. Eight Linux-native application cases passed their own scope. |
| Ancestors and quota | 23 of 24 contexts passed. The remaining Smoke original encountered a QEMU/GLib invalid-handle warning before web readiness. |
| Disk, browser and lifecycle | Six of seven cases passed. Disk A1, Disk A2, Truncated A and the three M16 lifecycle cases have accepted originals. Truncated A verified 120 HTTP and 49 browser bodies (15,931,827 bytes), the empty 404 responses for the malformed target, and natural cleanup. Its earlier Edge-update failure is preserved. Disk B still requires an accepted original. |
| Matched performance | Three CLROS WHPX benchmark boots completed their first-plus-ten-warm HTTP sequences and browser checks. Boot 3 used Edge 157, and its browser timings are kept separate from the earlier Edge 156 results. Its outer coordinator failed a process census after the original owner and children had joined. The second Alpine WHPX boot passed its 88 HTTP samples, then recorded a refused browser connection. Seven distinct boots and the final comparison remain. |

The Truncated A acceptance record is `artifacts/refactor-r01/disk-read-TruncatedA-acceptance-r04.json`
in the producer checkout (SHA-256 `1915E8FA7AA698E607B71E0F0ABD8D45FE935CCD7759E5900E42CCB2C1F82CC8`).
The replacement held the Edge binary read-only through completion, passed the
independent disk replay and exited with code 0.

Three bounded Disk B controls with virtio host `ioeventfd` disabled completed
their declared 20-second observation without another GLib warning. They ended
by deliberate cancellation. The [host compatibility record](../docs/qemu.md#windows-virtio-notifier-compatibility)
preserves the unchanged guest image, altered diagnostic recipe and remaining
Smoke/Disk B qualification boundary.

The quiet timer/cache pair in [step1748](../done/step1748.md) compared the same
2,009,369-byte file at 167.45 and 7.17 seconds mean transfer time. Each image
had one boot and two requests. The final performance report requires the
matched populations described below. See [PR814](http://192.168.4.35:3000/admin/CLROS/pulls/814)
for the source-specific seals and subsequent updates.

[Step1787](../done/step1787.md) records source35b's accepted Full/compiler29,
then the failed Hello ancestor host inspection. Repair6b0f56a8 is host-only;
67focused passes and retained production re-analysis preserve the failed
original and do not renew final source qualification. Freeze/rebuild the repaired
candidate and complete the same29/24/7/Linux/browser/performance/delivery scope.
Unentered ready-child cancellation is distinct from missing evidence for a child
which entered user mode; original FIFO, generations, reaps, budgets and cleanup
assertions remain. Do not shorten TIME_WAIT or other original test intervals.

[Step1786](../done/step1786.md) preserves the first DiskA1 original as failed at
the host supervisor replay invariant. Repair 61d71bef passes 1,044 focused checks
across 180 groups and complete copied-value row/parent/sector re-analysis; it
changes no kernel/app/profile/budget. Original source 1eb Full/29/23 and eight
Linux-native app receipts remain evidence for those sources, not automatic
acceptance of the repair. Smoke, seven fresh disk contexts with healthy-A1
dependency, current CLROS populations and every final gate below remain mandatory.

[Step1785](../done/step1785.md) preserves the complete failed Full (996 groups,
unchanged inputs, zero reuse) and repairs six affected fixture groups with259
fresh focused passes. Runtime code is unchanged; remaining final qualification
and PR814 delivery take priority over preserved follow-up refactors.


[Step1783](../done/step1783.md) records the fresh909ef13d Full fast-phase failure
and unchanged-input/zero-reuse join. Two stale current producer mappings are
repaired; 52 focused checks and expanded contract audit pass. Freeze/rebuild before
another unfiltered Full; no acceptance transfers from that failed original.

[Step1781](../done/step1781.md) integrates the storage completion common tail,
passes58 focused checks and records current emitted facts. Bounded native review
finds no changed-code defect; the changed18-byte GCInfo record retains a separate
consumer/semantic condition. No new guest or final qualification follows.

[Step1779](../done/step1779.md) and[step1780](../done/step1780.md) simplify dynamic
runner discovery/grouping and separate explicit worker controls. Their focused
host checks and unchanged inventory do not replace a frozen unfiltered Full.
The decimal-output trial stays rejected/restored under[step1778](../done/step1778.md);
the failed first WHPX baseline remains failed with no completed paired population.

[Step1756](../done/step1756.md) repairs host-side result ownership and prepares
a current Timing+Summary EFI. Compilation and canonical structural verification
pass; own emitted code/data/stack and lifetime review remain prerequisites to
one controlled Short diagnostic. The failed artifact-owner verifier transport
is preserved separately. No historical stack margin, Full/browser acceptance or
final source-frozen gate transfers to this image.

[Step1748](../done/step1748.md) repairs timer quiescence/directory coverage and
records a matched quiet WHPX full-file pair:167.45→7.17 seconds mean, all four
complete bodies exact. One boot/image, combined attribution and deliberate joined
host stop are explicit. Own native/stack/source/client authorities precede both
launches; focused checks pass. Final frozen unfiltered Full/29/24/7, matched
populations and browser/natural-cleanup qualification remain mandatory.

[Step1746](../done/step1746.md) lands dynamic ownership for the seventeen
semantic negative vectors and rejects/restores a separately measured inlining
trial. All twelve Short raw gates pass; both speed criteria fail. Fresh r171
focused31/repo/contracts/diff pass without replacing the earlier entire-fixture
failure. Continue responsibility and causal performance work; final frozen
unfiltered Full/29/24/7, matched populations and browser gates remain mandatory.

[Step1745](../done/step1745.md) renews own-Full native proof and records two
unchanged-bound diagnostics. WHPX remains incomplete; TCG HTTP/browser delivery
is observed, with deliberate host stop followed by an erroneous summary capture
requirement. The host repair passes focused red/green checks; both original outer
failures stay failed. Final source-matched Full/29/24/7 and performance/browser
qualification below remain required after the comment/docs/producer refresh.

[Step1743](../done/step1743.md) verifies host body-deadline and receipt-failure
semantics before further qualification. Retain the real timer, expected red and
green originals and exact source boundaries. No native/guest/unfiltered full or
browser acceptance transfers; remaining source quality and all final gates below
stay mandatory.

[Step1742](../done/step1742.md) closes statement-layout drift and refreshes
current source pins, with full graph/62 fresh focused/repo/contracts/diff success.
It grants no native/guest acceptance; every final emitted-artifact and29/24/7
requirement below remains. Finish bounded host correctness and responsibility
work before freezing the final qualification candidate.

[Step1741](../done/step1741.md) renews QMP/native provenance and completes a
fresh twelve-boot paired Timing Short series, sixty responses and exact raw
joins. JSON/HEAD improve but initial WHPX health regresses and range overlaps;
no universal gain or causal QMP proof follows. All Full/browser/final frozen
host and 29/24/7 contexts below remain required. Failed originals stay failed.

[Step1740](../done/step1740.md) passes conditional Timing C native review, one
functional Short and four paired WHPX originals. The series then stops on a
baseline post-seal QEMU warning before storage/HTTP; three-pair/TCG acceptance is
absent. The QMP ownership repair passes 51 focused host checks but needs renewed
source/native/guest evidence. The cause remains unproven; no guard/budget is
relaxed and no final gate below is closed.

[Step1739](../done/step1739.md) records r148 cleanup and conditional current
Timing/Full native reviews, plus twelve separate local commits through c3b118f3.
The first actual Full018 warm=0 diagnostic fails the unchanged180-second full-asset
request deadline after1,490,944/2,009,369 bytes. Children join and after-guards pass;
browser/normal cleanup remain unqualified. Continue attribution and bounded
repairs. No final gate below is satisfied by this development checkpoint.

[Step1738](../done/step1738.md) passes the host-only explicit HTTP benchmark
owner and semantic timeout checks: 68 fresh checks, unchanged inputs, zero reuse,
with independent 5,874-input/31-producer readback. Actual quota fit and guest/
browser/performance measurements remain pending. The inherited browser error
masking finding needs bounded follow-up. Reviewable storage/FIFO maps are ready;
case-correct staging and every final qualification gate below remain required.

[Step1737](../done/step1737.md) completes remaining formatting and host source/
compiler-resource/EFI policy repairs. The r140 graph and57 fresh check markers
pass with unchanged5867-input/31-producer boundaries and zero reuse; independent
metadata/source/result review passes. Preserve r139 mixed128/129 and all original
failures, including unresolved full-Fast allocation7936. Eight index case aliases,
responsibility review, real extended benchmark ownership and all final gates
below remain. No native/guest/browser or combined acceptance is renewed.

[Step1736](../done/step1736.md) adds the host-only explicit first/ten-warm/
full-asset/concurrent HTTP observation API and21 NVMe formatting files. The r134
graph/49 focused checks pass. Actual extended guest/browser measurements and
every final acceptance gate below remain required; API report completion is
not original-process, native or browser qualification.

## Current diagnostic attribution checkpoint

[Step1732](../done/step1732.md) completes the current-image native Timing review
and 18 source-bound diagnostic boots: three per Original/Trace/Summary mode and
WHPX/TCG backend. All 90 HTTP responses and raw disk/packet/owner guards pass.
Trace-to-Summary WHPX 64 KiB median falls 22.7%; TCG falls 5.4% with overlapping
samples. These are first sequences; HEAD may warm metadata. Guest wall regions
are not CPU measurements. Summary cannot replace full-byte acceptance. Production
optimization and final Full/browser/matrix acceptance remain pending. Continue
the approved combined goal with the phase contract and remaining gates below.

[Step1733](../done/step1733.md) rejects the serial-copy candidate after six
functional boots with no repeatable benefit, restores its source/pin and
integrates the reviewed 22-file storage formatting batch. The r128 graph and127
focused checks pass. Production improvement and all final gates remain required.

[Step1734](../done/step1734.md) records the full-evidence FIFO candidate's host,
publication and bounded native checks. Complete native admission must precede
its original boots. Production improvement and final qualification stay open.

[Step1735](../done/step1735.md) supplies the conservative stack failure/repair,
complete conditional admission and six initial plus twelve quiet interleaved
originals. Quiet WHPX64KiB median improves10.8%, every candidate below baseline
minimum; TCG pairs remain mixed. Full evidence/limits/assertions and all input
guards remain. Keep the candidate for continued qualification. This first-sequence
comparison does not satisfy the ten-warm/full-asset/concurrent/Blazor requirement
or final frozen unfiltered Full and29/24/7 matrix below.

## November 1 combined-refactor authorization

The user approved implementation of one combined RC3, code-quality and measured
performance candidate. Chat `01a19907-2a86-7ef0-98f5-59142a70e49e` takes over PR814
and M04-026 integration from d47f8558, with exactly one storage subagent. The
original RC3 checkout remains a preserved producer/evidence baseline; development
uses `codex/refactor-combined-r01` and the isolated `codex/refactor-storage-r01`
lane. Only the primary runs shared builds and native/guest qualification.

Mechanical commits preserve type/API identity, field/initializer order, ownership
and conditional behavior. Update explicit source closures and independently
review provenance after moves; do not bulk-accept changed source hashes. The
root .editorconfig supplies IDE formatting/naming guidance; `repo style check`
and `repo check` enforce type/file structure in host tooling only. Intermediate
existing violations are reported, not suppressed to create a passing baseline.

Performance work first preserves original inputs and distinguishes trace cost,
validation, scheduling/readiness, storage and network delays. The reported
12.40-second WHPX observation is a 64KiB range from a 2,009,369-byte asset, not a
complete 2MB transfer. Controlled diagnostics and later source-matched acceptance
remain distinct. Neither trace suppression nor relaxed deadlines grants success.

Final acceptance remains the full matrix below on the combined stable source.
Existing full r06/compiler29 originals retain their source-specific scope; the
later WHPX FullA1 and same-image TCG browser failures remain failed. Preserve
all unique worktrees and raw evidence. Writes, SQLite, sustained-service and
provider qualification are outside this extension.

Approved 2026-10-31. Owner: this execution chat, isolated branch
`codex/rc3-whpx-qualification`, baseline master `a96d47f9` (PR812).
Canonical issue key: M04-026. The phase stops after verified merge and recovery;
#791, #775, writes, persistence and SQLite acceptance remain subsequent work.

## Completion and order

1. Verify exact SDK/runtime/ILC/trimmer RC3 packages, MSVC/linker and Linux musl
   builder, source inventories, outputs and complete sequential stage budgets.
2. Implement exact RC3/C#14 pins and explicit `-Accelerator whpx|tcg` /
   `--accelerator whpx|tcg`; Windows WHPX default, otherwise TCG, no fallback.
   WHPX uses the explicit hardware interrupt controller (`kernel-irqchip=on`).
   The software-controller candidate failed unchanged APIC readback and remains
   unaccepted. Exact machine/backend selection is emitted and required by capture
   admission. For the exact inspected QEMU binary only, authenticate reset-state
   prelaunch under `-S`, seal zero or one exact initialization-warning line before
   one acknowledged `cont`, and reject all later stderr bytes after the retained
   original job joins. Preserve raw bytes, original stdout child/digest anchors
   and sidecars. Missing/changed proof fails; initializer origin remains unknown.
   Earlier failed originals remain failed. New source-bound originals must qualify
   this host policy and backend; no old guest or native margin transfers.
3. Pass focused affected host checks and reviewed native ABI/instruction/stack/
   mapping/ownership admission before any RC3 guest.
4. Complete one short JSON/exact independently hashed 64KiB range/final-health/
   owned-stop original on WHPX and identical-image TCG diagnostic. No new full
   host run is required before this diagnostic. Timing is diagnostic only.
   The Timing owner supplies the five-response sequence, with v11 host receipts
   and explicit mode/image controls described in step1731:
   GET /api/status (exact DTO values/JSON media type), GET /healthz,
   HEAD CoreLib.wasm, GET CoreLib.wasm bytes0-65535, GET /healthz, followed by
   deliberate runner cancellation, confirmed join and endpoint refusal. Original
   evidence remains the default. Explicit Trace/Summary additionally require the
   complete independent BEGIN/END/image-record/final-file consumer; Summary is
   diagnostic only and cannot substitute for ordinary full byte evidence. Keep
   runner600s/peer480s, body180s and existing cleanup limits. Publish/review
   the actual selected Timing EFI before activation; Full-only native approval
   cannot substitute. Old v7 originals remain unchanged. This diagnostic grants
   no normal guest shutdown, browser or full-workload acceptance.
5. Freeze final source/producers, rebuild the complete HostTests graph, and run
   one unfiltered full lane with unchanged-input guards and zero reuse.
6. Renew the final matrix below with source-bound RC3 originals and complete
   native/guest/browser consumers. Deduplicate identical recipes, not coverage.
7. Update affected existing docs/README, milestone evidence and canonical issue;
   publish reviewed evidence before closure, scoped tracker preview/apply/readback,
   commit/push/review/merge and canonical remote master parity. Preserve unique
   changes and required ignored artifacts before cleanup. Leave primary clean on
   verified master when ownership permits, record exceptions, stop development,
   and keep the broader goal paused.

## Required final matrix

- Step1225 compiler-migration contexts: ordinary, module-unwind, dispatch,
  array-positive, string-positive, gc, constructors, eh-simple, eh-finally,
  eh-rethrow, eh-nested, eh-frame, eh-catalog, eh-filter,
  eh-constructor-failure, eh-normal-finally, eh-unhandled (dedicated terminal
  negative), cooperative, timer-r01/r02/r03, loaded-stack, live-allocator, acpi,
  first-exit, owned-survival, physical-mapping, pci and apic.
- Fresh RC3 Linux NativeAOT Hello, runtime and Minimal API publication/audit using
  the existing `nativeaot payload qualify --output <fresh-directory>` producer;
  affected current app candidates retain separate exact publish/ELF identities.
- Step1556 exact owned Smoke, Quota256, Quota512, positive V2/512 and its complete
  20-profile ancestor inventory, retaining each accepted positive/negative and
  restoration criterion. The artifact-ledger keys supply each concrete recipe.
- Step1672 Full018 DiskA1/DiskB/DiskA2/TruncatedA plus M16 Mechanical59,
  ExitPending and CloseReuse. Actual HTTP/image/sector/browser/cleanup joins,
  nonce variants and expected corruption semantics remain mandatory.

Historical commands and raw receipts remain read-only. New execution needs new
current-source bindings and empty outputs; an old source guard must never be
overridden or made to authenticate a new artifact. Native margins must be
remeasured; original transcript semantics and resource limits stay intact.

## Commands, inputs and budgets

Use SDK `11.0.100-rc.3.26522.148`, runtime/ILC/trimmer
`11.0.0-rc.3.26522.148`, C#14 and net11.0. Preflight discovers the explicit x64
linker with vswhere and records actual path/version/hash before freezing it.
Do not install from the previously checksum-failed VS component catalog.

Focused builds/tests use `dotnet build CLROS.slnx --nologo -m:1`, the complete
HostTests project build, and `--filter <affected-fixture> --results <fresh-root>`.
Native preflight is `dotnet run --project tools/OS.Tooling -- efi build
--preflight-only`. Dry-run both `qemu smoke -DryRun -Accelerator whpx` and `tcg`.
Final full is `dotnet run --project tests/OS.HostTests --no-build -- --lane full
--results artifacts/rc3-whpx-final-host-r01`. Also run repo/contracts/diff checks.

Discover QEMU/OVMF paths; current installed QEMU is 11.1.0. Preserve Nehalem,
one-vCPU and x87/SSE context contracts. Full018 retains outer7200s/browser3600s,
runtime6600s/realtime7200s/grace120s and its existing separate HTTP deadline;
RequestDelay1800s is listener-clock anchored, not a host startup timeout.
EFI default build300s/verifier60s and existing explicit profile bounds remain.
Linux payload publishes allow45min per app; other existing stages retain their
own bounds. An outer owner must cover the sum of sequential stages and cleanup.

## Failure and consultation rules

Classify production/harness/environment/evidence-processing failures before one
bounded repair. Preserve originals/raw bytes/failures. Auditable re-analysis is
allowed and is not a new execution. Justify expensive reruns by changed inputs
or inadequate evidence, reuse applicable reviews, and never weaken checks,
increase limits to obtain a pass or retry unchanged. Consult expert chat
`01a16aa9-c50d-7c90-8639-c34a81399caf` only for a specific difficult problem or
blocker with evidence and a bounded question; execution remains with this owner.

## Initial evidence

Original isolated RC2 tooling graph: build succeeds, zero warnings/errors,
25.64s. Atomic tooling reservation allocates step1673. No RC3 native/guest,
full-host, browser, persistence or wider gate acceptance is claimed here.

[Step1674](../done/step1674.md) records the active RC3 source/package admission,
eight end-to-end accelerator controls,158 affected focused checks, four fresh
structurally verified native publications and independent raw decoding. Full018
and M16 emitted PEs match their accepted predecessors; fresh native safety,
Linux candidates, original guests and final unfiltered full remain required.

[Step1677](../done/step1677.md) completes the early same-image WHPX/TCG diagnostic
after the bounded timer-entry repair in step1676. [Step1678](../done/step1678.md)
records the remaining actual RC3 candidate/receipt/package/source/index bindings
and retained failed originals. Freeze the final source and rebuilt producers
before the required unfiltered full and final native/guest/browser matrix.
