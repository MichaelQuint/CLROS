# Step 948 - Restore the exact M02-015 ownership note

## Summary

Restore one required implementation note in the M02-015/#55 catalog entry.
The step945 scope rewrite had removed the canonical sentence listing its exact
four owned paths. The sentence now agrees with the unchanged `ownedPaths` array;
all other catalog values and all product/test source remain unchanged.

## Why

The complete host run on `5cbf5d67afb7f2d72bc5a0c95fa64b3e661170b6`
failed `gitea backlog manifest is complete UTF-8 without BOM`. The specific
failure was the exact ownership-note predicate in
`GiteaBacklogManifestTests.TestManifestShape`, not an encoding defect.
Preserve the existing contract and restore its omitted note instead of changing
the tests or weakening the declared ownership boundary.

## Files changed

- `.gitea/backlog/clros-issues.json`: add only the exact M02-015 ownership note.
- `done/step948.md`: record the failed complete run and focused repair evidence.

The catalog still owns only the two double-fault host source/test files and
`docs/diagnostics.md` plus `docs/boot-order.md` for this slice. Later shared EFI
integration still needs its separately claimed issue. Dependencies, status,
completion criteria and every other issue entry are unchanged.

## Tests run

The previously used complete manifest-module project and Program were copied
unchanged into `artifacts/orchestration-20261001/issue519-manifest-focused`.
The project links the actual current `GiteaBacklogManifestTests.cs` and
`HostToolProcess.cs`; its module initializer runs the complete 17-test module.

```powershell
dotnet build artifacts/orchestration-20261001/issue519-manifest-focused/ManifestFocused.csproj --nologo -m:1
dotnet artifacts/orchestration-20261001/issue519-manifest-focused/bin/Debug/net10.0/ManifestFocused.dll
git diff --check
```

The build exited 0 with zero warnings/errors and empty stderr. The actual run
from `2026-10-02T17:57:22.6398752Z` to `17:57:48.6088023Z` exited 0:
17 starts, 18 passes, zero failures, empty stderr, and final
`TEST_PASS manifest.focused`. These include the original failed shape predicate,
offline preview, dependency/ownership and closure-policy rejection checks.

## Evidence

SHA256 identities under the focused directory:

- Build result: `EFE52F718C9FD98D7A5A7B1FED8ACC72711B91161AC9E240BAC48E15A7C78576`.
- Build stdout: `BE2E7A54408D2A1B063D001B380D3CDAA8F9CA7FBC5EAB00C716D501D07A0837`.
- Run result: `9F22846506ABCE4D7F71075B44E8E0B92FE759C9F2B25715477D9BF0043F7968`.
- Run stdout: `A7B862C8DE1D3D813D888BD465B6B0F43402D98F122B57EE99021296C4D5B4C9`.
- Repaired catalog: `6386FF925AC3CDDA4AF31DC216AF50DCA6E9016AB61EDD4F11A19CBFFC8A5A45`.
- Unchanged test source: `2E7E273EBE84C17838C915A2E070CD39B2AEC473F2132A458281CBFA6CBA5AA6`.

The failure and pending complete rerun were posted with GET-verified read-back
to #519 comment14011 and #55 comment14015. Generated logs and focused binaries
remain ignored local evidence.

## Failures

The original complete run is preserved under
`artifacts/orchestration-20261001/runtime-next-wave-5cbf5d6`:

- Started `2026-10-02T16:56:50.9210080Z`; ended `17:47:48.3307543Z`.
- Native exit `-532462766`; 898 starts, 897 passes, one failure; no terminal pass.
- Result SHA256 `34F59379E5CE0915846D52F889BE08002D34D0F34731EBA9BB830C7A032BEB14`.
- Stdout SHA256 `50CE623D65AFEFC85E04DA745CA6B7277F01950E736CABBBB8F3156AFCEACC43`.
- Stderr SHA256 `AEF63448A4926BCFCFE3A878EDF4973F5A006ABADA1F56AF6F8932A9102E5DD3`.

The 227-byte stderr reports the manifest module's one-failure exception.
The checkout remained clean throughout that run. The partial run is not
accepted, and the completed-gate checker and guest capture were not invoked.

## Deferred work

Commit the catalog repair, perform a fresh clean solution build and complete
host run on the resulting head, and bind fresh evidence to that head/tree.
The unchanged EFI source closure remains 77 compile items/90 raw inputs with
SHA256 `77c2779c669b7f3491344570418e9d1dcb27061f35041e60e7c3bd66e929d1cd`.
Rebind the capture inputs before the five fresh guest lanes; old head bindings
cannot be used for the new run. Independently review the complete host and
actual guest/native/metadata/transcript/lifecycle/disk evidence before merging
PR #520 and closing any applicable issue.

## Evidence boundary

This proves the catalog correction through the complete focused manifest
module only. #55/#76/#519 remain open, PR #520 remains draft, and Gates 3/4
remain Fail. No actual allocation guest, double-fault guest, general allocator,
GC, hosted .NET/Kestrel, post-ExitBootServices, cloud or production readiness
claim follows from this repair.
