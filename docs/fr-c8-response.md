# FR-C8 — response: paragon start/gate composite layers

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-c8-paragon-start-gate-composite-layers.md` (extends FR-C7).
> **Status: DELIVERED — verdict #2: LOCATED, with the data.** Not
> data-silent. Spec: `casc-diablo4-format.md` §10.12 + Appendix A
> CL-23.

## 1. Verdict — #2 located (and an FR-C7 correction)

The start/gate composite **is** in `ParagonBoard` SNO 657304. The
FR-C7-era conclusion *"no distinct gate/start texture is bound in
ParagonBoard"* (`ParagonRenderProjection`, and the FR-C7 §6 "not in
data" framing for start/gate) was **wrong** — logged as **CL-23**, the
disc-only `start.*`/`gate.*` States were a `Project()` gap, not the
scene content.

**Root cause.** The FR-C7 decode (§10.3) modelled only the 56-byte
`0x22` instance record. The start/gate node templates bind their layers
via a **distinct fixed 0x58-byte block** the `0x22` scan never matched:

```
+0x00 u32 tag (2 = bound layer value)   +0x04 u32 0
+0x08 u32 value (texture handle)
+0x20 u32 ownerClassId                  +0x28 u32 0xFFFFFFFF (sentinel)
```

So `Template_Node_Starter` / `Template_Node_Quest` surfaced as
near-empty (your "1 of 17 fields") and `start.*`/`gate.*` collapsed to
the neutral disc. The handles were physically in the scene the whole
time — your raw-`ReadUiScene`/`Project()` observation was correct; the
decode just didn't model this second binding shape.

## 2. Decoded — oracle-exact

Ordered scene handles (back→front), raw-byte verified in 657304 and
matching your owner-verified atlas oracle **exactly**:

| Node | Template | Scene layers (handles) |
|---|---|---|
| **Start** | `Template_Node_Starter` | `0xA0F996FE` (filigree, `2DUI_Paragon_transparentElements`), `0xF8312CA8` (grey hexagon, `2DUI_ParagonNodes`) |
| **Gate/Exit** | `Template_Node_Quest` | `0xA0F996FE` (filigree); ornate square **`0xC2DF4786` selected** / **`0x0E6B6249` unselected** |

- The **symbol on top** (`0x35B6E536` spider for Start node 2458702;
  `0xE1316816` portal for Gate node 994337) is the
  `ParagonNode.HIconMask` — per-node, **correctly not a scene layer**;
  already exposed via `ParagonNodeDefinition.HIconMask` /
  `TryGetIconFrame`. Start/gate use **no disc** (`0x1D166DC7` absent),
  matching your evidence.
- The red directional triangles are the standard "selectable in this
  direction" indicators — procedural, not part of the composite
  (acknowledged; consistent with the FR-C7 §6 overlay precedent).

## 3. Shipped (consume this)

- **Typed (primary):** `ReadParagonRenderLayout().States` —
  `start.unselected` / `start.selected` now carry
  `[0xA0F996FE, 0xF8312CA8]`; `gate.unselected` / `gate.selected` carry
  `[0xA0F996FE, 0xC2DF4786, 0x0E6B6249]` (back→front), exactly as
  r3/r4/socket already do. Catalog-validated — every emitted
  `NodeElement.TextureHandle` resolves to a real atlas frame (the 0x58
  blocks also carry small int params like `20`; those are excluded — no
  fabricated layers).
- **Raw (scope-B):** `UiWidget.ExtraLayerValues` — the lossless ordered
  0x58-block values per widget (`ReadUiScene(657304)` →
  `Template_Node_Starter`/`_Quest`), if you want the unfiltered stream.

## 4. Honest residual (located, not pinned — no fabrication)

Per FR-loop discipline, what is **not** decoded is left at default, not
guessed:

- **Per-layer rect/scale** and the **shader brightness/tint pass** —
  `NodeElement.Rect`/`Alpha` are `default`. This is the same boundary
  as FR-C7 §6 / the per-rarity disc tint: yours to own (you already
  have the shader-brightness recipe).
- **Exact unselected↔selected ornate-square binding** — the *handle
  identities* (`0x0E6B6249` unselected / `0xC2DF4786` selected) are
  your confirmed atlas RE; the data-side state→handle binding is
  located-but-not-pinned, so both `gate.*` States expose the full
  ordered set rather than a state split I cannot yet prove from bytes.
  Compose unselected vs selected using your confirmed identities; the
  library will tighten this if/when the state binding is pinned. FR-C8
  is **unreleased** (on `main`, not in the published `0.2.0-alpha` —
  which is frozen by NuGet immutability); its contract stays amendable
  until FR-C8 actually ships in a future owner-batched release.

The symbol layer: take it from `ParagonNodeDefinition.HIconMask` of the
specific Start/Gate node (2458702 / 994337), drawn small/centred over
the decoded frame stack — exactly your evidence.

## 5. Acceptance

`ReadParagonRenderLayout_decodes_start_gate_composites` (live
`3.0.2.71886`) asserts: start ⊇ {A0F996FE, F8312CA8}, gate ⊇
{A0F996FE, C2DF4786, 0E6B6249}, no disc in either, symbols are the
node HIconMask and not scene layers, and `ExtraLayerValues` surfaces
them raw. Full suite green (37 pass, 0 skipped, 0 warnings); API docs
regenerated.

## 6. Round log

- **R1 (2026-05-17, consumer):** opened with the typed-States table,
  oracle + confirmed handles, raw-scene-absence evidence.
- **R2 (2026-05-18, library): DELIVERED #2.** Raw-byte scan proved the
  four frame handles are in 657304 (Template_Node_Starter / _Quest);
  decoded the 0x58-block binding the FR-C7 `0x22` scan dropped (CL-23,
  an FR-C7 correction); extended `UiScene.ExtraLayerValues` (raw) +
  `ParagonRenderLayout` `start.*`/`gate.*` `States.Layers`
  (catalog-validated); oracle-exact, tested. Residual (per-layer
  rect/tint, exact selected/unselected state binding) honestly
  located-not-pinned, consumer-owned per the FR-C7 §6 precedent.
  FR-C8 is on `main` but **unreleased** — `0.2.0-alpha` (published,
  immutable) does not contain it; it ships in a later owner-batched
  release, amendable until then. Consumer may lift the provisional
  procedural start/gate path onto the authoritative decoded handles now
  by building from `main` (or wait for the package that carries FR-C8).
