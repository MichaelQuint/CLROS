# CLROS documentation

## PR824 finite performance result

[Step1792](../done/step1792.md) records the finite PR824 phase qualified at `6a700fe1`
(runtime implementation `cdab7536`). Later documentation/metadata is separate.

Use the new step and [performance phase](../work/http-performance-phase.md) for
the current five-change implementation, complete qualification/source map and
matched quiet results. Use Step1790 and its phase record for historical PR814.
The phase ends after verified delivery and recoverable cleanup; no next phase
or PROD/DEV separation starts automatically.

Earlier entries below retain their original source, pending work at that
checkpoint, failures and qualification boundaries.

## Implementation contracts and earlier evidence

Start with the [repository README](../README.md) for the project overview,
supported workloads and build commands. The [documentation map](documentation-map.md)
lists the canonical page for each subsystem.

| Task | Start here |
| --- | --- |
| Build the pinned toolchain and EFI image | [Host requirements](host-requirements.md) and [EFI build](efi-build.md) |
| Run a guest or inspect a failure | [QEMU](qemu.md) and [diagnostics](diagnostics.md) |
| Check what has been qualified | [Completion gates](completion-gates.md), [workload profiles](workload-profiles.md) and the [evidence history](../done/) |
| Review PR814 and its stopping point | [Release scope and qualification record](../work/rc3-whpx-qualification.md#approved-closeout-scope) |
| Contribute a change | [Agent guide](../AGENTS.md) and [issue workflow](issue-labels.md) |
