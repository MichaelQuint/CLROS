# License and Provenance Audit

## Current Status

The project owner has authorized agents to inspect and use the reference
projects for CLROS implementation work. That direction does not erase
third-party copyright, license notice, attribution, source-disclosure, patent,
trademark, or asset-license obligations.

The 29 local reference directories are archive snapshots without `.git`
metadata. None has a top-level file named `LICENSE`, `LICENCE`, `COPYING`, or
`NOTICE`. Some READMEs state or link a project license; some trees contain
nested component licenses; some contain no local license evidence. An archive
name ending in `-main` or `-master` is not a verified revision.

Therefore:

- Architecture study and independently authored behavioral tests are allowed.
- Direct source reuse is blocked until the exact upstream, immutable revision,
  applicable license, source path, authorship, and required notices are
  recorded.
- A nested dependency license applies to that dependency, not automatically to
  the surrounding project.
- Missing license evidence means "unknown", not "public domain" or "cleared".
- Reference artifacts never satisfy a CLROS completion gate.

SharpOS has additional permission evidence that changes its default
disposition. On 2026-07-25 the project owner supplied a Telegram transcript in
which Daniil Vaino linked `https://github.com/daniilvaino/SharpOS`, identified
it as his repository, stated that it is CC0 and free to copy, and proposed
direct consolidation of its kernel, std, UEFI-exit, memory-management, and GC
work with CLROS. The supplied 7,775-byte transcript has SHA-256
`F244C8B21E90DA7627E2F12EA3E7F0CF7F2034F5417C20F0E6D49F0F5CC5E97D`;
the private transcript itself is not committed because it contains unrelated
personal conversation. A complete content comparison identifies the local
archive as upstream commit `b80ee3a4175e1522d1a12ba7fe371a49da3207f5`
except that the archive omits upstream's top-level `LICENSE`. The current
reviewed upstream `main`, `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`,
retains the top-level CC0-1.0 legal text and README license statement.

This is sufficient to treat SharpOS-authored source as eligible for direct,
selective adaptation rather than `reference:license-blocked`. It does not
relicense separately identified third-party/vendor code, the GPL ManagedDoom
fixture, WAD data, screenshots or other media with separate rights, or the
absent CoreCLR fork. Because the supplied archive still lacks `.git` metadata,
an adapting issue must record the identified commit, exact local source path,
SHA-256, and copied-versus-reimplemented boundary.

### Sanitized SharpOS permission attestation

This subsection is the durable, privacy-minimized record. The unrelated private
conversation is intentionally excluded.

| Field | Preserved value |
| --- | --- |
| Grantor identity | Daniil Vaino, who linked and described `https://github.com/daniilvaino/SharpOS` as his repository |
| Recipient/project | Michael, for consolidation into the CLROS project discussed in the same exchange |
| Transcript time evidence | The supplied export contains no timestamps; it was supplied to this repository review on 2026-07-25 |
| Explicit permission | “my repo is cc0” and “Completely free to copy/steal, no need to write from scratch” |
| Proposed reuse scope | Direct reuse from the SharpOS reference snapshot, including its kernel, std, UEFI exit, memory management, GC, and related source, while retaining CLROS project separation and cloud/Kestrel work |
| Original attachment fingerprint | 7,775 bytes; SHA-256 `F244C8B21E90DA7627E2F12EA3E7F0CF7F2034F5417C20F0E6D49F0F5CC5E97D` |
| Independent public corroboration | Identified archive commit `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` and reviewed tip `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` contain a top-level CC0-1.0 license and matching README license statement |
| Exclusions | File-local third-party/vendor terms, the GPL ManagedDoom subtree, WAD/media rights, and the separately versioned absent CoreCLR fork |

The attestation establishes permission and scope without requiring future
agents to access the private transcript. The attachment fingerprint exists
only to detect substitution if the owner elects to present the original again;
it is not a claim that the private attachment is stored in Git.

## Snapshot Ledger

Archive revision is `unknown` by default because the supplied snapshots contain
no `.git` metadata. SharpOS and nativeaot-patcher are documented exceptions:
independent complete content comparisons identify matching upstream commits
while preserving the archives' missing-metadata caveat.

| Snapshot | Local evidence | Default reuse disposition |
| --- | --- | --- |
| `abanu-master` | README badge/text identifies MIT and links an upstream license that is not present locally. | Test vectors or independently reimplemented algorithms; recover revision/license before copying. |
| `AtomOS-master` | README links a missing project `LICENSE.md`; `src/Compiler/License.txt` covers compiler-related material and README separately links Cosmos licensing. | Conceptual/test-only until file-level ownership and both license scopes are resolved. |
| `Aura-Operating-System-master` | No local top-level project license; `LICENSES/` contains third-party component licenses only. | Negative security tests only; no direct project-source reuse. |
| `Brovan-main` | README states GPL-2.0. | Host test corpus ideas only unless a deliberately GPL-compatible isolated tool is approved. |
| `CosmosELF-master` | README contains no usable local license evidence. | Malformed-input ideas only; no direct reuse. |
| `Cosmos-gen2` | README links Cosmos's upstream license, but no project license file is present locally; bundled tool licenses are separate. | Independent test vectors only unless exact upstream/revision/license is recovered. |
| `CSharpLLVM-master` | No local project license evidence. | Conceptual compiler/runtime comparison only. |
| `dotnet-aot-xc-main` | No local project license evidence. | Conceptual host-tooling reference only. |
| `EfiSharp-main` | No top-level project license; `Licenses/` contains .NET, mlibc, and RoseOS component licenses. | EFI ideas/tests only; determine authorship and applicable license per file before copying. |
| `elfsharp-master` | README says the license is in a missing `LICENSE` file and notes MIT test binaries. | Use only independently authored parser tests until the missing project and corpus licenses are restored. |
| `expressos-master` | README states UIUC BSD. | Recover immutable revision and full notice before any direct adaptation. |
| `FlingOS-develop` | README states GPLv2 and explicitly warns that copying imposes GPL obligations. | Concepts/documentation patterns only; no first-party implementation copy. |
| `GlieseOS-main` | No local project license evidence. | Ignore for implementation. |
| `Kernel-master` | No local project license evidence. | Negative examples only. |
| `KernelSharp-master` | No local project license evidence. | Independent allocator/paging regression tests only. |
| `libcs-master` | No local project license evidence. | Negative/conceptual use only. |
| `libvirt-csharp-main` | `src/libvirt/libvirt.csproj` declares `LGPL-2.1-only`; no top-level license text is present. | Host-only package/API comparison. Prefer `virsh`; recover the LGPL text and revision before distribution. |
| `MOOS-master` | README links an upstream license, but the license text is absent locally. | Concepts/negative ABI tests only until provenance is recovered. |
| `MOSA-Project-master` | README states New BSD and links a missing upstream license; `Source/Docs/license.rst` and bundled tool licenses require separate review. | Independent behavior tests are preferred; recover exact notices before copying. |
| `nativeaot-patcher-main` | All 888 ordinary files match `e33ed962dd97d8d9cfd7529d1c94941306a2aad9`, with upstream `LICENSE` omitted and gitlink dependencies absent. That revision is MIT; reviewed tip `f910382228d46e811f907c08c50ad860db7c1a74` uses BSD-3-Clause, with separate .NET MIT headers. | Named, pinned files are eligible for narrow attributed adaptation after the source-adaptation record below. This is not blanket clearance of dependencies or permission to import Cosmos's runtime architecture. |
| `Nitrocid-main` | Project files declare `GPL-3.0-or-later`; source headers repeat GPL terms. | Do not copy into first-party code; optional hosted/tool use would require explicit GPL handling. |
| `PatienceOS-main` | No local project license evidence. | Test-pattern ideas only. |
| `RoseOS-master` | No top-level license; EfiSharp carries a file called `RoseOS License`, but that copy is not provenance for this snapshot. | Negative UEFI/allocator tests only until exact RoseOS provenance is restored. |
| `Sharpen-master` | No local project license evidence. | Historical conceptual use only. |
| `SharpOS-main` | Content matches upstream `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` except for the omitted top-level `LICENSE`; the owner permission record and upstream CC0-1.0 text authorize SharpOS-authored reuse. Current reviewed tip is `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b`. `vendor/Iced/LICENSE.txt`, `vendor/PeNet/LICENSE`, and the ManagedDoom license retain separate scopes. | SharpOS-authored source is eligible for selective adaptation when the exact commit/path/hash and CLROS boundary are recorded. Keep every vendor notice and GPL/asset scope isolated; never commit a WAD. |
| `SharpOS-master` | No local README or license file; source text refers to GPL version 3 but is not a complete license record. | Historical test census only; independently recreate tests. |
| `smolsharp-main` | No top-level project license. Files under `src/BFlat.ZeroLib` carry explicit GNU Affero GPL version 3-or-later headers; the README also discusses external ZeroLib/bflat work whose complete provenance is not locally captured. | Size/security concepts only; no direct source import. The explicit AGPL source boundary is incompatible with copying those files into first-party CLROS kernel/std/app source. |
| `xOS-main` | `CosmosOS-License` contains BSD-3-Clause terms for CosmosOS/COSMOS-derived material; no separate top-level xOS license is established. | No implementation use; do not extend the Cosmos notice to unrelated xOS/dependency code. |
| `zerosharp-master` | No local project license file; README points to historical NativeAOT/runtimelab sources. | Selective build/EFI concepts only until source and binary provenance is recovered. |

## Upstream Candidates Recorded by Local Metadata

These URLs are present in local READMEs, badges, project metadata, or the
supplied project context. They identify a provenance investigation target but,
except for the independently content-identified SharpOS and nativeaot-patcher
snapshots documented here, do not identify the commit represented by an archive.

| Snapshot | Candidate upstream |
| --- | --- |
| `abanu-master` | `https://github.com/abanu-org/abanu` |
| `AtomOS-master` | `https://github.com/amaneureka/AtomOS` |
| `Aura-Operating-System-master` | `https://github.com/aura-systems/Aura-Operating-System` |
| `Brovan-main` | `https://github.com/AdvDebug/Brovan` |
| `Cosmos-gen2` | `https://github.com/CosmosOS/Cosmos` |
| `EfiSharp-main` | `https://github.com/JoshuaWierenga/EfiSharp` |
| `FlingOS-develop` | `https://github.com/FlingOS/FlingOS` |
| `GlieseOS-main` | `https://github.com/valentinbreiz/GlieseOS` |
| `libvirt-csharp-main` | `https://github.com/falox/libvirt-csharp` |
| `MOOS-master` | `https://github.com/nifanfa/MOOS` |
| `MOSA-Project-master` | `https://github.com/mosa/MOSA-Project` |
| `nativeaot-patcher-main` | `https://github.com/valentinbreiz/nativeaot-patcher` |
| `Nitrocid-main` | `https://github.com/Aptivi/Nitrocid` |
| `PatienceOS-main` | `https://github.com/FrankRay78/PatienceOS` |
| `SharpOS-main` | `https://github.com/daniilvaino/SharpOS`; the local content matches `b80ee3a4175e1522d1a12ba7fe371a49da3207f5` except for its omitted `LICENSE`; reviewed `main` tip `0c00476fa6d9cfebb6e83d5da34ab8fb1074163b` retains CC0-1.0. The README separately links `https://github.com/daniilvaino/dotnet-runtime-sharpos/tree/sharpos/coreclr-port` for its absent CoreCLR fork. |
| `smolsharp-main` | `https://github.com/ascpixi/smolsharp` |
| `zerosharp-master` | `https://github.com/MichalStrehovsky/zerosharp` |

For all omitted snapshots, the canonical upstream remains unrecorded until it
can be recovered from trustworthy metadata. Do not infer it solely from the
directory name.

## Source-Adaptation Record

Before a direct copy or material algorithm adaptation, the implementing issue
and `done/stepNN.md` must record:

1. Reference directory and exact source path.
2. Canonical upstream URL.
3. Immutable commit/tag and retrieval date.
4. License identifier and the local or upstream license text used for review.
5. Copyright holder and required notices.
6. Whether the CLROS result is copied, translated, adapted, or independently
   reimplemented from a specification/test.
7. Exact CLROS files affected.
8. Third-party dependencies, generated code, media, firmware, or test-corpus
   files involved.
9. Distribution obligations and where notices/source will be delivered.
10. The independent tests and known-reference weaknesses carried forward.

If any item is unknown, label the backlog item `reference:license-blocked` and
use only conceptual or independently authored test influence. The exception is
SharpOS-authored source covered by the owner grant and upstream CC0 evidence:
missing archive commit metadata is still a provenance gap, but it is not a
license blocker when the exact local file hash is recorded. File-local vendor,
GPL, asset, and contributor notices still take precedence over that default.

## SharpOS-Specific Boundary

The local SharpOS snapshot and the reviewed post-archive upstream delta mix
several distinct scopes:

- The owner-supplied permission record and current upstream CC0-1.0 license
  authorize selective reuse of SharpOS-authored kernel, std, boot, PAL, tools,
  and app SDK source. Content comparison identifies archive commit
  `b80ee3a4175e1522d1a12ba7fe371a49da3207f5`; adaptations still bind to exact
  source paths and hashes because the archive contains no Git metadata.
- `vendor/Iced/LICENSE.txt` applies to the Iced vendor tree.
- `vendor/PeNet/LICENSE` applies to the PeNet vendor tree.
- The ManagedDoom application is isolated below
  `apps_native/GPL_AHEAD_WARNING_DOOM_managed/` and carries its own GPL
  license. Its code must not enter kernel/std, and WAD data must not be
  committed.
- The linked `dotnet-runtime-sharpos` CoreCLR fork is not present. Its future
  use requires the upstream .NET runtime license, fork commit, patch ledger,
  dependency closure, build provenance, and attribution to be preserved in a
  separately versioned dependency.
- Post-archive XtermSharp material is MIT, Terminal.Gui v1.19.0 is MIT, and
  TurboXml is BSD-2-Clause. Preserve those notices and dependency boundaries;
  they are not relicensed by SharpOS CC0.
- `dotnet/runtime`-derived no-stdlib/SIMD material is MIT. Prefer adapting from
  an exact official runtime revision instead of copying SharpOS's intermediate
  derivative.
- Fami and TriCNES appear as external submodule gitlinks in the reviewed
  upstream history and were not provenance-cleared by this audit. They remain
  blocked from CLROS source or distribution.

The SharpOS CC0 evidence does not supersede a file-local third-party license,
and the absence of the external CoreCLR fork prevents reproducible
hosted-runtime provenance.

## nativeaot-patcher provenance refresh (2026-10-06)

The complete local comparison identifies matching content at
[`e33ed962dd97d8d9cfd7529d1c94941306a2aad9`](https://github.com/valentinbreiz/nativeaot-patcher/tree/e33ed962dd97d8d9cfd7529d1c94941306a2aad9):
888 matching files, no changed/extra local files, and only `LICENSE` missing
among upstream ordinary files. The two gitlink dependencies are absent from
the archive. The recovered [MIT license](https://github.com/valentinbreiz/nativeaot-patcher/blob/e33ed962dd97d8d9cfd7529d1c94941306a2aad9/LICENSE)
names Kaleb McGhie aka zarlo; its SHA-256 is
`06e55a337bd7d0289092ea4ace9c9bc0dbedd25a05d14edb2a7114fac82cc767`.

Commit [`d542234`](https://github.com/valentinbreiz/nativeaot-patcher/commit/d54223432f6c736582b9f9f54d50a8189b759bc1)
changed the project default and many Cosmos-authored headers to BSD-3-Clause
on August 23. The reviewed September 21 tip
`f910382228d46e811f907c08c50ad860db7c1a74` has the [CosmosOS/COSMOS project license](https://github.com/valentinbreiz/nativeaot-patcher/blob/f910382228d46e811f907c08c50ad860db7c1a74/LICENSE),
SHA-256 `29f118db54e6264efc3f28ba9ab456ca9cc69a8959c2dbb071c5620e0be04072`.
Retain its source/binary notices and non-endorsement condition for applicable
adaptations. Do not apply that change retroactively to the identified MIT
snapshot or treat a root license as replacing file-local terms.

`ReadyToRunHeader.cs`, `TypeManagerHandle.cs` and other .NET-derived files retain
MIT/.NET Foundation headers. Prefer the matching primary runtime source where
practical; the inspected runtime gitlink is
`baf4eb1e0b672338897fc20de49daffe5f84569c`. `ManagedModule.cs` and
`ModuleHelpers.cs` have no overriding license header and mirror NativeAOT
concepts: retain the pinned project notice and any traced .NET attribution
instead of asserting exclusive Cosmos authorship. LAI, media, fonts,
compression and other unreviewed dependencies receive no blanket clearance.
SharpOS's owner grant does not apply to this repository.

The previous unknown-revision/missing-license blocker is resolved for these
identified inputs. Every actual adaptation still needs the ten-field record
above, exact source hashes and the implementing issue's owned paths. Existing
behavior-only issue scopes are unchanged by this review. Full license texts,
the primary runner, and 20 selected input pins are retained as research under
`artifacts/references/nativeaot-patcher-20261006-primary` and
`artifacts/orchestration-20261001/nativeaot-patcher-refresh-20261006`.
No source was imported into production or the supplied reference archive.

## First-Party and Distribution Rules

- First-party kernel/std/app source remains C#; do not commit `.c`, `.cpp`,
  `.h`, `.asm`, or `.s` there.
- C/C++ required by the future modified CoreCLR remains in a separately
  versioned sibling repository and crosses CLROS only through `OSHost_*`.
- Host tools may consume normal .NET packages only with locked versions,
  compatible licenses, notices, and separation from the guest image.
- Generated firmware, QEMU/OVMF, WADs, fonts, screenshots, binary corpora, and
  cloud images each require their own redistribution review.
- Do not print or commit credentials, tokens, private package feeds, signing
  keys, or cloud identifiers.
- A project-owner instruction to use a reference is implementation
  authorization, not legal advice. Escalate unresolved copyleft, patent,
  trademark, asset, or distribution questions before release.

## Current Automation Boundary

The source firewall can detect forbidden first-party source and accidental
reference build inputs; it does not identify licenses, determine copyright,
recover commits, generate an SBOM, or prove notice compliance.

Until dedicated tooling exists, validate this document with:

```powershell
$references = Get-ChildItem .\references -Directory
if ($references.Count -ne 29) { throw "reference inventory changed" }

$repositoriesWithGitMetadata = $references |
  Where-Object { Test-Path (Join-Path $_.FullName ".git") }
if ($repositoriesWithGitMetadata.Count -ne 0) {
  throw "update provenance entries for snapshots with recoverable git metadata"
}

git diff --check
```

## Next Steps

1. Bind each SharpOS adaptation to the identified archive or reviewed upstream
   commit, exact source path/hash, and recorded CC0/owner permission evidence.
   Separately recover canonical upstream URLs, immutable revisions, retrieval
   dates, and complete license texts for
   `zerosharp-master` and `EfiSharp-main` before their first direct source
   adaptation. For nativeaot-patcher, use the recovered immutable MIT/BSD
   records above and review the particular source/dependency notices.
2. Add a machine-readable attribution/provenance ledger and SBOM generation
   before distributing binaries containing third-party material.
3. Treat every future reference update as a new immutable snapshot requiring a
   renewed source, license, and behavioral-diff review.
