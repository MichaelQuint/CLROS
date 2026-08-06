# Step 908 - Emit the bounded ready marker through the owned COM1 writer

## Summary

Implemented managed backlog issue M02-005 (#45) as real C# production code.
`OwnedCom1ReadyExclusivelyOwnedWriter` accepts only the checked
`Com1OwnedPortIoBackend`, repeats the accepted seven-write 115200 8N1 COM1
configuration, polls THRE through `In8(0x3FD)`, and writes exactly
`OWNED_COM1_READY\r\n` through `Out8(0x3F8)`. It returns value-only,
status-coded evidence for configuration, written bytes, line-status polls, the
failed byte index, owned-backend use, and absence of firmware fallback.

The source is allocation-free on the production path, contains no managed
reference fields, strings, arrays, exceptions, reflection, or firmware serial
fallback, and preserves all `OS.*`, `src/OS`, `src/OS.Kernel`, and `OSHost_*`
identities.

## Why

M02-004 proved seven COM1 configuration writes under QEMU but deliberately did
not execute `In8` or route payload bytes through the owned backend. M02-005 is
the next dependency-ordered slice: it makes one bounded marker transport real
without pretending that firmware has exited or that panic diagnostics are
installed.

Managed issue #45 does not own the shared EFI project, entry point, smoke
scripts, README, or central EFI/QEMU docs. Those paths are handled only by the
separately claimed supplemental issue #450; the two bounded QEMU-runner paths
needed for the required 259-character evidence marker are separately owned by
#451.

## Files changed

Issue #45 owns:

- `src/OS.Kernel/Diagnostics/OWNEDCOM1READYExclusivelyOwnedWriter.cs`
- `tests/OS.HostTests/OWNEDCOM1READYExclusivelyOwnedWriterTests.cs`
- `docs/diagnostics.md`
- `docs/boot-order.md`
- this globally reserved `done/step908.md`

No manifest, synchronizer, `BootSequence.cs`, `Probes.cs`, completion-gate,
reference, app, generated artifact, unrelated source/test/doc, or existing done
ledger is part of this managed slice.

## Tests run

Focused development checks:

```powershell
dotnet build .\CLROS.slnx --nologo -m:1
dotnet run --project .\artifacts\focused-m02-005\FocusedM02005.csproj --nologo
git diff --check
```

The focused harness linked the production writer, its checked binding/backend
dependencies, the eight writer cases, the four EFI-integration cases, and the
serial-marker boundary group. The eight managed-writer cases passed:

- exact seven configuration writes and 18-byte marker/CRLF payload order;
- delayed THRE readiness and preserved poll count;
- bounded transmit timeout;
- invalid poll limit rejected before I/O;
- unready binding rejected before I/O;
- exact caller-readable 16-byte marker;
- default result fails closed and contains no managed references;
- source contains no firmware fallback or host-only production dependency.

The managed host marker was:

```text
CLROS_M02_005_READY marker=OWNED_COM1_READY markerBytes=16 lineEndingBytes=2 writer=ownedCom1 hostCallbacks=true guestPortIo=false
```

The fresh complete pre-push HostTests run then passed with 3,399
line-leading starts, 3,400 passes, zero failures, exactly one terminal
`TEST_PASS host.tests`, and process exit 0. Its 872,320-byte log is
`artifacts/logs/m02-005-hosttests-wave-r4.log` with SHA-256
`8D40BCBF2A68E57200506D21CA698FE7F827C60D08F11815F5DF4FD1634E4F20`.
The managed eight-case group and four-case EFI integration group both passed
inside that complete run.

The complete solution/HostTests/EFI/QEMU acceptance gate is recorded by the
combined integration ledger and must pass again at the exact pushed review head
before #45 can move to done.

## Evidence

The managed source by itself uses safe host callbacks and therefore reports
`guestPortIo=false`. The separately claimed #450 development integration has
already linked this exact writer into the no-stdlib EFI closure and observed the
standalone `OWNED_COM1_READY` line under real QEMU/OVMF before the distinct
UEFI-serial detail marker. That combined evidence is recorded in
`done/step909.md`; it does not change the narrower ownership of this file.

## Failures

- The first host-test compile lacked the explicit
  `System.Runtime.CompilerServices` import required for `RuntimeHelpers`; the
  import was added to the host-only test. No production dependency was added.
- After the reboot, the ignored focused harness initially resolved its linked
  project from the wrong parent directory. The disposable harness path was
  corrected under `artifacts/`; no tracked source or evidence from the failed
  attempt was accepted.

## Deferred work

- Run and preserve the complete final-head gate, post exact evidence to #45,
  #450, and #451, and move the combined wave to `agent:review`.
- Obtain independent `admin` approval whose `commit_id` equals the current PR
  head before merge.
- After the implementation merge, managed #45 still requires a separate
  reviewed manifest-closure PR, canonical synchronizer `-Apply`, and an
  idempotent no-write preview before `agent:done` and closure.
- Live device I/O beyond the recorded QEMU profile, packet loss, runtime unwind,
  and cloud cleanup are outside this slice. This step does not prove real
  `ExitBootServices`, post-firmware/panic diagnostics, physical/provider
  support, hosted .NET, Minimal API, Kestrel, CloudReady status, production
  readiness, or numbered completion-gate advancement.
