# FR-C9 — response: exhaustive paragon render-model + coverage gate

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-c9-exhaustive-render-model.md` (generalises FR-C7/C8).
> **Status: DELIVERED — #1 + #2 + #3.** Lossless raw decode + a green
> library-side coverage gate + the one-shot manifest. Spec
> `casc-diablo4-format.md` §10.14 + Appendix A CL-26. **Unreleased**
> (on `main`; batched into a future owner-cut release — no single-fix
> package).

## 1. You were right about the root cause

FR-C8's nine rounds were one bug wearing nine hats: a **non-self-
validating, lossy scene decode**. Recon confirmed a *still-unmodelled
class of bindings* even after CL-23/24/25 — so per your acceptance
("located but a class still unmodelled is NOT acceptance") this needed
a structural fix, not another one-off. Done.

**The defect.** The CL-23 `0x58` "bound-layer block" model over-fit two
examples — it required `ownerClassId @+0x20` and `0xFFFFFFFF @+0x28`.
Those words are **not universal** (other blocks carry a pointer/zeros
there), and a widget's *last* block straddles the next `nameStart` so
its tail is unreadable anyway — the exact CL-24 lesson, ungeneralised.
Result: a whole set of real bindings still dropped, including the grey
rim ring `0x87A89F86` (the FR-C7-era "not in data"), ~14 more in
657304 and ~12 in 964599.

**The fix (CL-26).** The only stable, self-validating marker is
`tag==2, +4==0, value@+8`. `UiScene.Parse` now captures **every** such
value, bounded on the value field (never the full record, so no
straddling tail is dropped) — both binding shapes, every widget. Raw
`ReadUiScene` is now **lossless** for texture bindings.

## 2. #1 — exhaustive typed model (+ lossless-raw, both delivered)

`Diablo4Storage.ReadParagonRenderModel()` → `ParagonRenderModel`:

- `.Layout` — the role-assigned FR-C7/C8 typed projection (unchanged
  contract; the 19-row `States`).
- `.Scenes` — `ParagonBoard` 657304 **and** `ParagonBoardSelect`
  964599, each `ParagonSceneModel(SnoId, Widgets)`, each
  `ParagonBoundWidget(Name, ClassId, Layers)` with every bound
  `{TextureHandle, Rect, Alpha}` — regardless of binding shape. This is
  your one-shot audit surface (#3 too).

`Diablo4Storage.IsParagonTextureHandle(uint)` — the single shared
structural test used by the projection, the model, **and** the gate:
handle-magnitude (≥ `0x10000`; D4 handles are 32-bit hashes — smaller
atlas-resolving values are field ints/enums, never bindings) ∧
catalog-resolvable. The published binding-record schema (the two value
shapes) is in spec §10.14, so you can independently walk the raw graph
if you prefer.

## 3. #2 — the coverage gate (the decisive part)

`ParagonRenderModel_covers_every_bound_atlas_handle` (casc's own
integration suite, live `3.0.2.71886`): for 657304 **and** 964599 it
scans **every** 4-aligned u32 in the raw scene, takes the structural
texture-binding set (`IsParagonTextureHandle`), and asserts **all** of
them are surfaced by `ReadParagonRenderModel()`. It is **shape-
agnostic** — it does not trust the 0x22/0x58 shapes; a *future* binding
shape that hides a real handle still fails this gate. Green now (no
dropped binding; grey ring `0x87A89F86` among the recovered). **A
regression here fails casc CI — not your eyeballs months later.** This
is the structural guarantee FR-C9 asked for; there is no R10/R11 of the
FR-C8 kind.

## 4. Boundary (unchanged)

Library: complete faithful decode + the gate proving none dropped.
Consumer: role/state classification (static art vs engine overlay vs
selectable glow — e.g. your `0x6D68F45F` locator exclusion) and the
owner visual oracle. This FR removed the *discovery* loop; the
*classification* loop is yours, as acknowledged. Scope stayed paragon
(657304/964599 + `2DUI_Paragon_*` + ParagonNode/Board/Glyph).

Your planned `tools/ParagonDataGen audit` (diff scene-binds vs
renderer-consumes) now has a stable contract to diff against: the
exhaustive `ReadParagonRenderModel()` is guaranteed complete by the
gate, so your audit only ever surfaces *classification* deltas, never
*discovery* gaps.

## 5. Acceptance

Decisive #1 (exhaustive typed model **and** documented lossless-raw +
published schema) **and** #2 (green library coverage gate, shape-
agnostic) **and** #3 (one-shot manifest). Full suite **39 green, 0
warnings**; API docs regenerated; spec §10.14 + CL-26. No fabrication —
the gate is sound (handle-magnitude filter excludes the tiny
atlas-resolving field ints) and currently passing on real data.

## 6. Round log

- **R1 (2026-05-18, consumer):** opened — make completeness structural
  (exhaustive model + library coverage gate + optional manifest); the
  FR-C8 pattern was a lossy non-self-validating decode, not unknown
  resources.
- **R2 (2026-05-18, library): DELIVERED #1+#2+#3.** Recon found a
  still-unmodelled binding class (the over-fit `0x58` block + its
  straddling tail — grey ring et al. still dropped). Fixed structurally
  (CL-26): `tag==2,+4==0,value@+8`, value-bounded ⇒ raw `ReadUiScene`
  lossless. Shipped `ReadParagonRenderModel()` (exhaustive manifest,
  657304+964599, `{handle,rect,alpha}`), `IsParagonTextureHandle` (the
  shared structural definition), and the shape-agnostic coverage gate
  `ParagonRenderModel_covers_every_bound_atlas_handle` (green; a future
  gap fails casc CI). Spec §10.14 (binding schema + losslessness
  guarantee) + Appendix A CL-26. Unreleased — on `main`, batched into a
  future owner-cut release; consumer builds from the `ProjectReference`
  source so it is already live there.
