# 0005 — 2026-05-16 — API docs that cannot drift

> Short tooling/process beat for the wiseowl.com session. Continues 0004.

Two shippable libraries (`WiseOwl.Casc`, `WiseOwl.Casc.Diablo4`) needed
complete API documentation. The interesting decision was *not* "write docs"
but "make docs that are structurally incapable of being wrong."

**Design:** the XML doc comments are already mandatory on every public
member (a binding rule since session 1). So they are the single source of
truth — the API reference is *generated* from them, never hand-authored.
Tooling is a pinned `xmldocmd` dotnet tool (`.config/dotnet-tools.json`,
`dotnet tool restore` in CI — reproducible, no machine-global install),
emitting one Markdown page per type + per member under `docs/api/<Package>/`
(301 pages), cross-linked and source-linked. A hand-written
`docs/api/README.md` carries the part a generator can't: the layered
reading guide (consumer-facing surface vs. the infrastructure namespaces
that are `public` only for cross-assembly use) and the pointer to the
byte-format spec.

The keystone: a CI `api-docs` job regenerates and `git diff --exit-code`s
`docs/api`. Change a public signature or a `<summary>` without
regenerating → red build. The committed reference therefore *cannot* drift
from the code. Article angle (a recurring through-line of this project):
the same discipline as "validate on meaning, not shape" and "never fake a
pass" — here, the docs can't lie because the build won't let them.

A small gotcha worth noting: `xmldocmd` couldn't load the `net10.0`
assembly's references; generating from the `netstandard2.0` build resolves
cleanly and is faithful — the public API is identical across all three
TFMs (nothing is TFM-conditional), so the lowest-common facade is the
right thing to reflect.

Net: `docs/casc-format.md` is the canonical *byte* spec; `docs/api/` is the
canonical *type* reference, generated and CI-guarded; `docs/devlog/` is the
*why*. Three cohesive doc surfaces, none able to rot silently.
