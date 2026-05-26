# `references` Override

This directory contains external reference repositories. Root `AGENTS.md` applies, with one critical local rule:

Do not edit reference repositories as part of first-party OS implementation.

## Purpose

References are used for:

- Architecture comparison.
- NativeAOT/UEFI/no-runtime patterns.
- Driver and network stack concepts.
- Host tooling ideas.
- License and implementation research.

They are not replacement architectures and are not first-party source.

## Allowed Work

Allowed:

- Read files.
- Search files.
- Compare patterns.
- Quote small snippets in issue/docs when license-appropriate.
- Document how a reference influenced an implementation.

Not allowed unless the user explicitly asks:

- Editing files under `references/`.
- Running broad formatting or cleanup in `references/`.
- Adding reference projects to `OS.slnx`.
- Replacing first-party architecture with a reference architecture.
- Treating reference build failures as first-party build failures.

## Copying And Licensing

The user has authorized source-code use from specific references, but agents must still keep provenance clear.

When adapting code or design:

- Prefer reimplementation in first-party style.
- Keep kernel/std/app source C# only.
- Document reference influence in docs or `done/stepNN.md`.
- Keep host-only third-party libraries out of OS image code.
- Update `docs/license-audit.md` when third-party code or package use changes.

## Project Naming

Some references use the name SharpOS. Do not rename this project or its docs to SharpOS. The first-party project name remains `OS`.
