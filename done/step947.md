# Step 947 - Narrow POD warning declarations and strict build acceptance

## Summary

The first clean integrated solution build at `0ff011f00179ae66ae8a48cc5c9d0d2ba4f640e8`
returned native0 with zero errors and ten CS0649 warnings. This follow-up scopes
that diagnostic suppression to the ten deliberately indirect/profile-written
fields. Field order, types, POD layout and all executable text remain unchanged.
Three existing tests receive the new exact raw-source aggregate.

The ignored full-host acceptance helper also needed an independent correction:
substring `0 Warning(s)` matches `10 Warning(s)`. Its replacement requires exact,
unique zero-warning/error summary lines. No result was accepted through the
defective check. A fresh integrated build and complete run remain required.

## Why

`ConstructorCount` starts at zero through whole-POD initialization and is
incremented only in the enabled EFI fixture. `Object1` through `Object9` are
written and read by pointer indexing from `Object0`. The compiler cannot infer
these deliberate uses in the default profile. Explicit field-scoped comments
and CS0649 disable/restore pairs preserve behavior without global suppression,
runtime initializers or changes to the no-stdlib build settings.

## Files changed

- `src/OS/Runtime/RhpNewFastAllocation.cs`: two field-scoped diagnostic blocks;
  `Object0` and unrelated declarations retain normal warnings.
- `tests/OS.HostTests/M02IdtrContinuedExecutionEfiIntegrationTests.cs`,
  `M02OwnedIdtGatePopulationEfiIntegrationTests.cs` and
  `M02Ud2VectorStateEfiIntegrationTests.cs`: only the current raw-source hash.
- `docs/efi-build.md`: explain the narrow compiler diagnostic boundary.
- This step, reserved with `scripts/new-done-step.ps1 -Reserve`.

Acceptance helpers and preserved logs are ignored artifacts, not product files.

## Tests run

The initial parent `dotnet build .\CLROS.slnx --nologo -m:1` ran at
16:38:23.6504571-16:38:48.4487289 UTC on 2026-10-02. Native0, ten warnings,
zero errors and empty stderr establish successful compilation with warnings,
not accepted zero-warning validation.

The initial complete host invocation started at16:40:38.8012554 UTC. Parent
deliberately terminated only its identified dotnet14564 process tree after
finding the warning/checker issue. Wrapper35112 recorded native1 at
16:41:56.2277926 UTC:384 starts,382 passes, zero TEST_FAIL and no final
host.tests marker. This incomplete run is not counted as a passing suite.

The author ran `dotnet build src/OS/OS.csproj --nologo -m:1 -t:Rebuild
-nodeReuse:false` against the repaired Runtime at16:47:01.2291410 through
16:47:03.1700605 UTC: native0, zero warnings/errors, empty stderr. Before/after
Runtime hashes match. This default managed compile does not publish NativeAOT
or execute tests. The three subsequent aggregate-only test edits await the
fresh parent complete build/run.

Pure ignored-helper controls exercise the actual predicate extracted from both
the completed-run checker and binding writer:32 cases each,64 passes, native0,
empty stderr. They include the actual ten-warning build rejection and prior
accepted zero-warning build, nonzero errors, duplicates and malformed or
substring summaries. Neither full acceptance helper has been invoked on a
completed new run.

## Evidence

Initial parent logs are under `artifacts/orchestration-20261001/`:

- `issue519-build-0ff011f/result.json`:
  `6AA3A9A932D7716A5B456BB79DBA2ECFBA3D038FC4E55ECE541B43228A7DC849`.
- Build stdout:
  `512C2F808218DD74868802C23E6CD9A9882595651A095485394DC9CF36A95095`.
- `runtime-next-wave-0ff011f/host-tests.stdout.log`:
  `D0E2E81B48F8143E1BE987021A7DCCDA08B6B34AF9A8F18B71B02CD1B9D168DE`.

The repaired Runtime source is
`BC101EE6851127C64A9542BC56E94AFE434C3DE5F89FAA9D1B2B7F0653E3CB69`.
The exact raw90-input aggregate becomes
`77c2779c669b7f3491344570418e9d1dcb27061f35041e60e7c3bd66e929d1cd`;
the ordered77-item Compile hash remains
`37b56feaae9f232dd547a34d4db872c5e998b2eb7d501eaf422c0c5f97034a79`.
Step946's source/artifact hashes remain historical and are not rewritten.

Author additive inventories are under `artifacts/issue519/warning-repair/`:
owned35 SHA256 `7062CFD1B8B13FD4D221D4DD37DFA5DC8460E475CBE2C544AC7942DCB4D4EDEA`,
source90 `88A8E3A7C648635FA75147B2F44198B253E0805B9EEB84AA61639BB2DA9C3710`.
Default rebuild result SHA256 is
`A14B5947C3664F0E7DBC1C1D695A74962EBD714BF3567B0AA6A1774F5E1B9EF4`;
its raw stdout is
`33BDC4FE211BB710781D754C2044533FD16BC4B8768C74C28E8FD906E846471F`.
Independent declaration-only review is
`8F281F451BF384A17BEAD4003E1C5FCD7F2AD1A531031142A91759888EE9DAAB`.

## Failures

The warning build and canceled partial full run remain preserved. The ignored
checker defect was discovered before any acceptance. The original checker,
inventory and uninvoked binding writer remain in their v1 directories; revised
helpers and genuine nonzero/duplicate/malformed-summary controls use a new v2
directory. No earlier artifact is reclassified as having the repaired source.

## Deferred work

Commit the reviewed narrow repair, run a fresh clean solution build and the
complete host suite, bind its actual launch and binary versions, and require
independent completion acceptance. Then run the five fresh QEMU profiles and
all native/metadata/transcript/lifecycle/disk checks from step946. No guest
was launched for the interrupted run.

## Evidence boundary

#55/#76/#519 remain open; PR520 remains draft and Gates3/4 remain Fail. This
diagnostic-only source change establishes no new allocator or guest behavior,
general runtime support, ExitBootServices, hosted .NET/Kestrel or readiness.
