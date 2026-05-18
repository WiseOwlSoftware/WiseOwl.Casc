# FR-C8 — response: paragon start/gate composite layers

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-c8-paragon-start-gate-composite-layers.md` (extends FR-C7).
> **Status: DELIVERED — R2 #2 (start/gate located); R5/R6
> (arrows+connectors located w/ rect; start/gate per-layer rect
> definitively not-authored; glow animation engine-driven #3); R7
> (select/deselect brightness/colour definitively not authored —
> engine shader, `Tint`/`LitTint=null` is the decoded answer).** Not
> data-silent. Spec: `casc-diablo4-format.md` §10.12–§10.13 + Appendix
> A CL-23/CL-24. **Unreleased** (on `main`; batched into a future
> owner-cut release — no single-fix package).

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
- **R3 (2026-05-18, consumer): CONSUMED + verified** (incl. the
  decode-surfaced 4th gate layer `0x6D68F45F` the eyeball missed).
- **R4 (2026-05-18, consumer): owner-validated** (start ✓, exit
  "looks perfect"); `0x6D68F45F` excluded as consumer compositing
  policy (a locator, not static art) — flagged for the library only if
  its role gets pinned.
- **R5+R6 (2026-05-18, library): DELIVERED.**
  - **R6 — directional pointer is NOT procedural (an FR-C7 §6
    correction; CL-24).** `Arrow_{Top,Right,Bottom,Left}` bind the four
    pre-oriented arrow frames **and an authored rect** via the standard
    0x22 texture-handle field (not a 0x58 block — FR-C7 just hardcoded
    the `overlay.*` rows empty *and* its 0x22 scan dropped each
    widget's **last** record, which is exactly the texture handle).
    Cardinal map: Top `0xD51CAB25`, Right `0x6D3CB8DE`, Bottom
    `0x8EEAC178`, Left `0xB6D8C741` (4 distinct pre-oriented frames —
    W/H-swap confirms, not one rotated). **Bonus:** the same fix
    revealed `Connector_{...}` also bind art (`0x77ECA3A8`/
    `0x288DE11F`) — also not procedural. Shipped:
    `overlay.pointerTriangle.Layers` / `overlay.connectorBar.Layers`
    now carry these (handle + decoded `Rect`), T/R/B/L;
    `overlay.selectionRing` has no scene widget → genuinely
    engine-drawn (empty). So **draw the data-mine arrow art at its
    authored rect/native size — no polygon, no eyeball.** (R6 answer:
    binding *and* rect exist — neither art-only nor engine-fixed.)
  - **R5 — start/gate per-layer rect/scale/tint: DEFINITIVE "not
    authored" (engine/template-inherited).** The §10.12 0x58 layer
    blocks are **handle-only** — the whole 88-byte block is zero
    except tag/handle/ownerClassId/sentinel; the pointing descriptor
    references a Common node child, so the frame layers **inherit the
    `NodeTemplate` (100-ref) node box**. There is no per-layer authored
    rect/scale/tint to surface — so `NodeElement.Rect`/`Alpha` for
    `start.*`/`gate.*` stays `default` (the decoded answer, not a
    gap). **Size the start/gate frames to `NodeTemplate`** (drop
    `StartGateSymbolFrac`/`GateSymbolFrac`/`StartGateGain` for the
    *frames* — no authored fraction exists; the *symbol* is the node
    icon element). The **arrow/connector** rects, by contrast, **are**
    authored and are now surfaced (R6).
  - **Animation (legendary/socket) — DEFINITIVE #3: engine-driven, no
    authored timing.** The layer **order** is delivered
    (`States.Layers`, back→front, incl. the corrected start/gate/arrow/
    connector). The pulse **timing** is *not* in the scene: the glow
    widgets bind no period/min/max; the scene's `Storyboard_*` widgets
    are UI transitions (`Black_FadeIn/Out`, `Glyph_Expand/Collapse`,
    `Board_Rotate`, …), not a per-node pulse loop (48 DT_FLOAT fields,
    none bind glow timing). This **reaffirms FR-C7**: `AnimSpec=null`
    is the evidence-backed decoded answer — the pulse is an engine
    shader loop; bake a representative static frame (FR-C7 §6).
    Reopen with an in-game oracle if a build shows authored pulse
    timing (same protocol that cracked start/gate).
  Spec §10.13 + Appendix A CL-24. Parser tail-fix is surgical —
  full-record scan byte-identical (no FR-C7 regression); the
  `..._decodes_proven_structure` connectorBar assertion was corrected
  (connectors bind art). Suite 38 green, 0 warnings. **Still
  unreleased** — on `main`, not in published `0.2.0-alpha`; batched
  into a future owner-cut release per the owner (no single-fix
  packages); amendable until then. R4's `0x6D68F45F`: the decode lists
  it as a gate layer; whether it is static art vs a locator/overlay is
  consumer compositing policy (FR-C7 §6 boundary) — the library does
  not reclassify it; no new pin from the data this round.
- **R7 (2026-05-18, library — select/deselect brightness/colour:
  DEFINITIVE "not authored").** Asked: does the data say how a node's
  brightness/colour/shading changes on select↔deselect? **No.**
  Field-hash scan of ParagonBoard 657304: `rgbaTint` (`0x09A3F17B`,
  `DT_RGBACOLOR 0x8E266332`) is declared/bound **only on non-node
  widgets** (glyph grid, `CoreStatActive`, …) — never on
  `Common_Node_Revealed`/`Node_Purchasable`/`Node_Purchased`/
  `Node_IconBase`/`Node_Located`. No `rgbaTintSelected`/`rgbaTintLit`/
  `flBrightness` field exists at all (zero occurrences — those names
  are not in the scene). The only authored per-widget "brightness"
  number is `dwAlpha` (`0x0C2AFA21`, `DT_BYTE`) — already surfaced as
  `NodeElement.Alpha`. So the dim-unselected / bright-selected look is
  an **engine shader pass over the per-state widget set** (the data
  gives *which layers compose per state* — `States` — and the per-
  widget alpha; the colour/brightness delta is the fixed shader recipe
  §2.3/§10.7, consumer-owned — the same pass the consumer already
  applies to its "atlas frames darker than in-game"). No select-
  transition timing either (the `Storyboard_*` are UI transitions, not
  a per-node brighten-on-select). `StateElements.Tint`/`LitTint =
  null` is the **decoded answer, not a gap**. Definitive #3,
  consistent with CL-24; reaffirmed in spec §10.13 / Appendix A CL-24.
  Reopen only with an in-game oracle showing a node visibly
  *recolouring* (not just swapping the glow layer) on select.
