# 0006 — 2026-05-16 — Splitting the byte-format spec into two

> Narrative source for the wiseowl.com session. Continues 0005.

The byte-format reference had grown by accretion: facts were split between
the numbered sections and the correction log (offsets stated as "see
CL-1/CL-4"), the StringList-vs-texture difference was repeated four times,
and the Diablo IV sections were in an illogical order (StringList before
the SNO wrapper, the DT primitives, and the `0x44CF00F5` container it
depends on). It read like a changelog, not a spec.

Two decisions:

1. **Restructure to a standard format reference.** Scope → Conventions →
   an Overview pipeline diagram → structures in *dependency/processing
   order*, each with a proper offset/size/type table and a byte-map
   diagram → the read algorithm → appendices (correction log as *errata*,
   provenance, boundary, source). The structure sections now state the
   corrected truth directly; the correction log became a historical errata
   appendix instead of being load-bearing. **Constraint honoured: not one
   byte fact was invented or changed** — every offset, magic, and stride
   is the same empirically-verified value, only reorganized and
   re-presented as tables/diagrams.

2. **One doc → two.** This reverses converged decision F.1. The earlier
   "one file, one CL log" concern was specifically about not letting the
   *frozen upstream* compete as a second source of truth — splitting
   *transport vs Diablo IV within this repo* is not "two truths"; it is
   two specs for two layers, each authoritative for its own. It mirrors
   the two shippable packages and the per-package API docs, and it scales:
   a future `casc-wow-format.md` slots in without bloating a mega-doc, and
   a WoW/Overwatch module author reads only the transport spec.
   - [`casc-format.md`](../casc-format.md) — game-agnostic
     CASC/TACT/TVFS/BLTE (`WiseOwl.Casc`).
   - [`casc-diablo4-format.md`](../casc-diablo4-format.md) — Diablo IV
     SNO/container/record (`WiseOwl.Casc.Diablo4`), carrying the
     provenance-&-migration map and the library-boundary appendices.

The split is auditable: the Diablo IV doc keeps the provenance table
mapping every upstream `d4-binary-formats.md §3–§8.15` item to its new
home (plus a row pointing the transport material at `casc-format.md`), so
nothing was dropped in the reorganization. Source XML doc comments and
every cross-reference (root README, API-docs README, resume-prompt,
feature-backlog, memory) were repointed; the consumer's "authoritative
byte spec" pointer should now target `casc-diablo4-format.md` for the
§5–§8 layouts.

Article angle: a recurring through-line of this project is that *form*
follows *ownership* — the library earned the spec (devlog 0004), and now
the spec's shape follows the package boundary. Documentation treated as
an engineered artifact, not an afterthought.
