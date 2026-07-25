# Step 845 - M00-021 Complete Host Baseline

## Summary

The complete CLROS host baseline passed from clean, pushed implementation
commit `32a8f3b91e17e1fc7e4b296730a5ea6565c38369`. The evidence runner executed
all 11 baseline steps, validated exactly one `TEST_PASS host.tests`, rejected
all host failure terminals, round-tripped its summary, and ended with
`TEST_PASS baseline.evidence`.

## Why

The original harness aborted on stale `OS.slnx` locators; every M00 host slice
had to be integrated and proven from an immutable source state before Gate 2
could be reverified.

## Files changed

- `scripts/run-baseline-evidence.ps1`
- `docs/repo-audit.md`
- `docs/completion-gates.md`
- `artifacts/logs/m00-final/32a8f3b91e17-20260725T213702Z/host/`
  (generated, not committed)

## Tests run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-baseline-evidence.ps1 `
  -LogRoot "C:\Projects\CLROS\artifacts\logs\m00-final\32a8f3b91e17-20260725T213702Z\host" `
  -Qemu "C:\Program Files\qemu\qemu-system-x86_64.exe" `
  -QemuImg "C:\Program Files\qemu\qemu-img.exe" `
  -OvmfCode "C:\Program Files\qemu\share\edk2-x86_64-code.fd"
```

The runner executed the exact common-gate build, host-build, host-run,
naming/source-firewall, QEMU dry-run, diagnostics dry-run, qcow2 intent,
libvirt intent, `git diff --check`, and `git status --short` steps recorded in
its JSON summary. Every step exited zero.

## Evidence

- Source commit:
  `32a8f3b91e17e1fc7e4b296730a5ea6565c38369`.
- Selected SDK: `10.0.302`, selected from the `10.0.300` feature band by
  `global.json` `latestPatch` roll-forward.
- Summary: schema `os.baseline.evidence.summary.v0`, mode `execute`, status
  `passed`, workload profile `SingleMinimalApi`, 11 steps.
- Host terminal cardinality: pass count `1`, fail count `0`.
- Terminal markers:
  `TEST_PASS host.tests`, `TEST_PASS baseline.host.tests.terminal`,
  `TEST_PASS baseline.artifact_summary`, and
  `TEST_PASS baseline.evidence`.
- Evidence root:
  `artifacts/logs/m00-final/32a8f3b91e17-20260725T213702Z/`.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `host/summary.json` | 14,308 | `C6835020F4DF780806C8DEFBDE43F91853299E03E08FBBEE46E5AAC523F26783` |
| `host/host-tests.log` | 364,064 | `E8692E465B082432225080854CBE190429FD44DEDD49991B72992E86FCEA880C` |
| `baseline-process.stdout.log` | 372,664 | `8D4B2445B11E8835D93636EBAD03078DA4A4A480A445370F570B33B6E64A257E` |

## Failures

Pre-commit integration runs exposed and then fixed two runner-test defects:
an over-tight drain-window timing assertion and redirected-output reads that
could deadlock after pipe saturation. The last pre-commit run also caught a
stale static assertion for the strengthened host-failure regex; that assertion
and the aggregate self-test marker check were corrected before the immutable
implementation commit. None of those superseded failures is accepted as
M00-021 evidence. The source-frozen baseline above had no failed step.

## Deferred work

Host tests do not substitute for QEMU, libvirt, or cloud evidence.

## Evidence boundary

This step closes only the M00 complete-host-baseline slice. It proves host
contracts and repository guards from the stated source commit. It does not
prove real QEMU boot, real `ExitBootServices`, live hardware, Kestrel, or any
cloud-provider capability.
