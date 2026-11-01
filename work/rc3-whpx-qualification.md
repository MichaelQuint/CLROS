# RC3 and WHPX qualification contract

Approved 2026-10-31. Owner: this execution chat, isolated branch
`codex/rc3-whpx-qualification`, baseline master `a96d47f9` (PR812).
Canonical issue key: M04-026. The phase stops after verified merge and recovery;
#791, #775, writes, persistence and SQLite acceptance remain subsequent work.

## Completion and order

1. Verify exact SDK/runtime/ILC/trimmer RC3 packages, MSVC/linker and Linux musl
   builder, source inventories, outputs and complete sequential stage budgets.
2. Implement exact RC3/C#14 pins and explicit `-Accelerator whpx|tcg` /
   `--accelerator whpx|tcg`; Windows WHPX default, otherwise TCG, no fallback.
   WHPX uses the explicit software interrupt controller (`kernel-irqchip=off`)
   while retaining accelerated vCPUs. Exact machine/backend selection is emitted
   and required by capture admission; the default/on controller's warning remains
   a failed original. New source-bound originals must qualify this backend.
3. Pass focused affected host checks and reviewed native ABI/instruction/stack/
   mapping/ownership admission before any RC3 guest.
4. Complete one short JSON/exact independently hashed 64KiB range/final-health/
   owned-stop original on WHPX and identical-image TCG diagnostic. No new full
   host run is required before this diagnostic. Timing is diagnostic only.
   The existing Timing owner supplies a new v8 five-response sequence:
   GET /api/status (exact DTO values/JSON media type), GET /healthz,
   HEAD CoreLib.wasm, GET CoreLib.wasm bytes0-65535, GET /healthz, followed by
   deliberate runner cancellation, confirmed join and endpoint refusal. Keep
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
