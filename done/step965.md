# Step 965 - SharpOS October 3 source-review addendum

## Summary and why

Apply the two already reviewed reference-document drafts after PR531 merged
at `7e64f4cfac99428df60f717bd51e34f4a382779b`. This records a bounded upstream source-review crosswalk and the
four already posted issue addenda. Historical step925's dfad/65-commit review
is preserved. No CLROS implementation, API contract or completion gate changes.

## Files changed

- `docs/reference-map.md`: exact accepted draft
  `066A29900E9F884DBF152C3A870632D460B77C1F07F7738B0B6462D72101F3C4`.
- `docs/fork-diff.md`: exact accepted draft
  `D68E28455BBB2BECD9C874B9BAB99284AC53607301DE3F18025956552A9F9E33`.
- `done/step965.md`.

Both original document hashes were checked before copying. Scripts, catalog,
product source and previous evidence steps are unchanged; the concurrent #529
read-only preview is separate from this documentation transaction.

## Source review and validation

The accepted research compares
`dfad516b816d29b971d23ef116d4dbebeedcd25e` with
`75c045963af300cc48072e12681eb90cd2c60e04`: five commits, 113 files,
17,698 insertions and 765 deletions. The ignored research clone's verified
origin was `https://github.com/daniilvaino/SharpOS.git`; only that public target
was fetched, with submodule recursion disabled. Its checkout remained clean
at dfad. No new fetch or research execution was needed for this application.

Pinned primary commits:

- [2c2318c](https://github.com/daniilvaino/SharpOS/commit/2c2318c91712f4ca1e339c272fd44a0312869c37)
- [3aa20d5](https://github.com/daniilvaino/SharpOS/commit/3aa20d58b313750ba68bf0b15d59f560b790aa07)
- [19cfb70](https://github.com/daniilvaino/SharpOS/commit/19cfb700cd580bca3681fe93d04eb871da4500ff)
- [82f8198](https://github.com/daniilvaino/SharpOS/commit/82f81981e90cdc8bbb7e941e988df49a1c97ef45)
- [75c0459](https://github.com/daniilvaino/SharpOS/commit/75c045963af300cc48072e12681eb90cd2c60e04)

The accepted report pins 38 source snapshots and exact line anchors. Source
conditions, rather than stale comments, govern its reference interpretation.
The four crosswalks concern checked reference helper/caller responsibility
(#79), optimized interface dispatch bypassing a resolver (#85), publication
and root/owner boundaries for a later collector (#82), and size/version/
capability/lifetime checks at the later flat ABI (#164). Upstream numeric
dispatch thresholds and execution claims are reported observations, not
.NET10 or CLROS guarantees. #79's bounded POD slot/bulk scope is not expanded
into covariance/type-barrier APIs. String observations stay corroboration only;
the delta supplies no new #58 firmware-map proof.

This application reuses those completed source-only reviews and exact drafts.
Step reservation used explicit WinPS ExecutionPolicy Bypass. No build, test,
upstream artifact, guest or API operation was executed for this step. Exact
scope, draft byte equality, UTF-8/LF/single EOF and whitespace checks are
recorded in the integration verification; parent review/commit follow.

## Evidence and existing issue comments

Paths are under `artifacts/orchestration-20261001/`:

- `sharpos-refresh-20261003/report.md`:
  `30D645DCC70084ADF4939F664E161893BB1C7B85A85B059E0D57B845A2AEA58C`.
  The report retains exact commit/file/line citations, full diff/stat and
  source identities. Parent independently rehashed87 package/eight root inputs
  and verified all38 selected snapshots against exact upstream Git bytes.
- `sharpos-refresh-docs-draft-20261003/identities.json`:
  `DC049F1669507C6D2CBFEAA610AB81DB18D6C76312F3DA9985EF4E5B8CD60F13`.
- `sharpos-refresh-comments-20261003/parent-execution/result.json`:
  `990A15A759FF9B26274B4479163193251816B8CF9C38A7A2837B593B311613EC`.
  The completed parent comment-only transaction used GET readback and preserved
  issue bodies, content versions, states, dependencies and other metadata:
  [#79 comment14520](http://localhost:3000/admin/CLROS/issues/79#issuecomment-14520),
  [#85 comment14523](http://localhost:3000/admin/CLROS/issues/85#issuecomment-14523),
  [#82 comment14525](http://localhost:3000/admin/CLROS/issues/82#issuecomment-14525),
  [#164 comment14527](http://localhost:3000/admin/CLROS/issues/164#issuecomment-14527).
  These comments grant no implementation release or closure.
- `sharpos-refresh-docs-integration-20261003/`: original snapshots, reservation
  command/result, exact final diff, protected-input readback and file identities.

## Failures

The original research's first packaging attempt incorrectly required byte
equality for historical step925: its working copy had180 CRLF pairs while the
committed version was LF. Both identities and the failed native1 record are
preserved; exact newline-equivalent text was then recorded without modifying
step925. Other preserved source-read diagnostics involved path/encoding/line
range errors, not product execution failures. The accepted drafts and research
packages remain unchanged; this step does not rerun or relabel those attempts.

## Deferred work and evidence boundary

Use these sources only as reviewed design/test-vector references. Upstream
targets net8.0/BCL8.0.27 and requires a separate CoreCLR fork that was not
fetched or reproduced. Root CC0 does not override per-file .NET MIT/vendor
notices. No code import or direct BCL-formatting reuse is proposed. The
ret-patched `RhpStackProbe` policy (single0xC3) is rejected for CLROS; it proves
neither stack extent nor page-touch/ABI safety. GC, EBS, hosted .NET/Kestrel,
provider/cloud/production readiness and all broader gates are unchanged.
Future work remains in the existing issues with their actual dependencies and
separately released ownership; no new issue or implementation is authorized.
