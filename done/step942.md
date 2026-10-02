# Step 942 - Repair historical fixtures after page-fault integration

## Summary

The first complete host run at `05b2bdeccb08ddf48db8d3da5d7314548d492b05`
failed in an older M01 source selector. Review found two additional stale test
assumptions before a rerun. This milestone repairs those three fixtures without
changing production code, the EFI source closure, or the historical obligations.
The complete host suite and fresh default/page-fault guests remain required.

## Why

The instruction-publication fixture preserved an unknown page-fault guard, so
two unrelated serializer calls entered every historical profile. Its expected
0/2/2/4 historical call counts and five-call older terminal profile were correct.
The loaded-image fixture selected the first method named `Run` in the whole
file, now the page-fault adapter, instead of the intended M01 adapter. Finally,
the page-zero fixture still forbade the null-fault plan that #517 intentionally
added to the canonical EFI Compile list.

## Files changed

- `tests/OS.HostTests/M01InstructionPublicationEfiEntryTests.cs`: explicitly
  selects the page-fault symbol, false by default, and checks its separate
  six-call enabled profile and adapter presence/absence in the existing group.
- `tests/OS.HostTests/M01LoadedImageStackEfiEntryTests.cs`: bounds all three
  whole-source `Run` lookups to `M01StageOneAdapter`. Existing method-selector
  tests now cover sibling collisions, both selected branches, original-source
  mutation lookup, missing exact adapter and missing method without fallback.
  Unknown preprocessor branches remain preserved.
- `tests/OS.HostTests/PageZeroMappingPolicyTests.cs`: requires the exact
  `NullPageFaultProbePlan.cs` Include/Link and its evaluated presence in both
  workload profiles. Host-test source remains forbidden in EFI; the plan's
  no-execution and marker-ownership checks remain unchanged.
- `docs/completion-gates.md`: records the failed integrated gate and new rerun
  requirement. Gate status remains unchanged.
- This step, reserved with `scripts/new-done-step.ps1 -Reserve`.

## Tests run

The parent ran the complete three repaired modules plus their actual
`BuildEfiShellPreflightTests` dependency through the original module initializers:

```powershell
dotnet build artifacts/orchestration-20261001/issue517-failed-gate-investigation/focused/Focused.csproj -c Release --nologo -m:1
dotnet run --project artifacts/orchestration-20261001/issue517-failed-gate-investigation/focused/Focused.csproj -c Release --no-build
```

Build: native exit 0, zero warnings/errors, empty stderr. Run:
`2026-10-02T09:05:41.0908836Z` through `2026-10-02T09:22:37.9819573Z`,
native exit 0, 66 starts, 67 passes, no failures, empty stderr and one terminal
`TEST_PASS issue517.historical.fixtures.focused`. All four module-ready markers
appear once. No reflection subset or replaced dependency method is used.
This includes the original mutation suites and the new profile/collision cases.

`scripts/check-naming.ps1` and `git diff --check` also passed with native exit 0
and empty stderr. Naming includes the first-party source-language firewall.
The complete integrated host gate is still pending at the future repair commit.

## Evidence

Evidence is retained under `artifacts/orchestration-20261001/`.

| Record | SHA256 |
| --- | --- |
| Failed complete result, `runtime-next-wave-05b2bde/result.json` | `4F890B6702F15AEB132CB1EB4592D34D0588537C63739710CE906F1E3DD1999B` |
| Failed complete stdout | `950163D84039DE84F6F1E3813FCB154C117567568A70757B14C26B7003126114` |
| Failed complete stderr | `CD037897A3CE47D2B754A7B3932C176B8773E865BD6189471E22972C986107BA` |
| Independent M01 diagnosis | `FFE6DBC907BD2A8324C2AF0D2E47AB07F6DBB927357D8E7F3F4A3AB17A18C300` |
| Independent three-fixture source review | `76C1C9FC43031D4950796072E39DB11815284931C04FFA61C07635DA1201E8C1` |
| Reviewed three-fixture identities | `1F81520C71E46895EA22600C1AA51827E733C9B9611798A6BF729D265FE25EDB` |
| Independent remaining-fixture audit | `0D5B0FADCC2605D9C91CE8EA2C5FD1623CA7CB4453F4035357001CF4226C7E6C` |
| Focused capture-helper review | `86B31275FCD6133DE116C27D80137636D8770A9371485B83FF385D763802B4D3` |
| Focused result | `474A411D13D5C2820257E9A10AE92515F598B9E168808D05B579D3C8AA600389` |
| Focused run stdout | `E5769A3FC386DABEEBA3D328C01D22208702C999649B95716EE964445B270249` |
| Focused build stdout | `FA6B64763BDADBABB69955D5AD0728758D4020BA20EA5C2962A3A61D9AC7EB70` |
| Naming/whitespace result | `93C1D7D35AF97EF9D79A585738DBE7AF52D50F49B424FDC095F878378E8E29AC` |

The review records are under `issue517-failed-gate-investigation/review/`.
The original step941 source and focused artifact evidence are preserved as
historical evidence. No failed or canceled attempt is relabeled as passing.
The remaining-fixture audit inspected 366 in-scope files and found only the
page-zero exclusion described above; it is static evidence, not execution.

## Failures

The actual complete run started `2026-10-02T07:59:29.8123500Z` and ended
`2026-10-02T08:45:40.7744195Z`: native exit `-532462766`, 1210 starts,
1206 passes, seven failure lines, no terminal pass and 272 stderr bytes.
The seven failure lines represent three assertions, their three group failures
and the module failure. Later modules did not execute. The checkout remained
clean at the same HEAD/tree. The other two stale fixtures were found by review;
they were not observed failures in this terminated run.

The earlier nineteen-fixture mechanical audit omitted these historical
assumptions. The complete host gate exposed that coverage gap. The repairs
retain the original negative checks rather than weakening their expected counts.

## Deferred work

Commit the reviewed repair, rebuild the clean solution, then run the entire
host suite at that new HEAD. Only accepted full-suite evidence can release the
default/page-fault QEMU lanes. Their actual serial, native process disposition,
artifact/source identities and pristine/booted image accounting must still be
independently reviewed before #517/#54 can close. #76's export/guest work remains
separate and open.

## Evidence boundary

This is a host-fixture repair milestone. It does not prove a real page fault,
reusable descriptors, firmware shutdown, devices, hosted .NET/Kestrel or cloud
readiness. Gates 3/4 remain Fail. No new real guest was launched in this step.
