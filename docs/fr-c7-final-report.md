# FR-C7 — final delivery report (beginning → end)

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `e:\Paragon\docs\fr-c7-paragon-render-layout.md` (the FR),
> Round-11/12 consensus, `docs/fr-c7-api-proposal.md` §7 (contract).
> **Status: COMPLETE.** RE 100%; all §7 surfaces shipped & merged
> (PRs #5–#10); §7.5 gates 1/2/3 pass. Authoritative byte spec:
> `casc-diablo4-format.md` §10 (+ `CL-9..CL-14`).
>
> **⚠ Status update (2026-05-18):** the "not published / integrate the
> local `0.1.1-alpha` pack / await publish" notes below are
> **superseded** — `0.1.1-alpha` was never released; the FR-C7 surface
> shipped in **`0.2.0-alpha`, published & immutable on nuget.org**
> (`PackageReference` it; the §7 contract is now **frozen**). FR-C8
> (`fr-c8-response.md`) is a later correction, unreleased on `main`.
> The local pack is obsolete. The body below is the original delivery
> record, retained as history.

---

## 1. Executive summary

You asked for the authored paragon node render layout so the optimizer
could composite pixel-faithfully with **zero calibrated constants**.
The headline result is a **premise correction you endorsed**: the game
does **not** store authored pixel constants. It composes node visuals
at runtime from (a) the `ParagonBoardDefinition` grid, (b) bound
*reference-unit* rects, (c) texture-native sizes, scaled by the
runtime resolution/zoom. So the deliverable is the **decoded
reference-unit model + unitless ratios + the derivation rule**, with
the single absolute scale staying permanently consumer-owned
(`IconCellFactor` = our ratio × your resolution/zoom basis — the same
boundary pattern as the 6 power-budget intrinsics / the §3 relight).

Everything was recovered **standalone and clean-room** (no third-party
data; from the format bytes + your own legally-installed
`Diablo IV.exe`). The optimizer can integrate now.

## 2. The arc (what was asked → what was found)

1. **Located the format.** The render metric is not in the paragon
   record/art/atlas groups (all eliminated with evidence). It is a
   **UI-scene SNO**: group 46 (CoreTOC type `UI`), format hash
   `0xE4825AB8`, `ParagonBoard` SNO **657304** (+ `ParagonBoardSelect`
   964599).
2. **Premise correction.** A full 145 KB scan found no authored pixel
   cluster (no texture-native size, screen res, or node pitch). The
   rect fields are *bindable* — values live in a separate instance
   section. Node geometry = grid + bound ref-unit rects + native sizes
   at runtime scale. You endorsed this; it reframed the contract from
   "px constants" to "raw model + ratios + derivation".
3. **Cracked the D4 hash (library-wide).** All D4 serialization ids are
   DJB2 core `h = h*33 + ch` with **seed 0** (not the textbook 5381).
   `TypeHash` (case-sensitive, full u32), `FieldHash`
   (`& 0x0FFFFFFF`, 28-bit), `GbidHash` (lowercased). Self-verified:
   `GbidHash("ParagonNodeCoreStat_Normal") == 0x42C16A1B`.
4. **Recovered the schema standalone.** Field/type names are absent
   from SNO data (hash-keyed by design) but embedded in the client
   binary's reflection registry; string-extracted from your own
   `Diablo IV.exe`, hashed, matched. No `d4data`/community-JSON
   dependency.
5. **Pinned the format end-to-end** (the `CL-*` log records two
   self-corrections caught *before* shipping — see §8): container,
   record header, schema, instance encoding, type enum.
6. **Converged the API** (Round-11/12, owner + consumer): §7 contract,
   counters C-a/C-b/C-c, the 18-row state matrix (15 baked + 3 overlay
   — the original "17" was an arithmetic slip, corrected).
7. **Implemented, tested, shipped** all three surfaces; reproduced the
   67.7 anchor; completed refinements; packed the library.

## 3. The format (reference, for your audit)

`0xE4825AB8` is a reflection / data-binding widget graph. Per widget:

```
nameStart                                  : name, NUL-terminated ASCII
classOff = nameStart + alignUp8(strlen+1) + 0x10
classOff+0x00  u32  class id = TypeHash(widget-class)
classOff+0x08  u32  0xFFFFFFFF  (sentinel — validates the header)
schema  : packed 12-byte (FieldHash, TypeHash("DT_BINDABLEPROPERTY")
          =0x1332C78D, DT_<type>)  entries
instance: fixed 56-byte 0x22 records, bound value @ +0x08,
          positionally keyed to the schema field order
```

Type enum (selected): `DT_INT 0xA4C42E02`, `DT_FLOAT 0xE65047AD`,
`DT_BYTE 0x3D4646AB`, `DT_ENUM 0x3D47BD2C`, `DT_RGBACOLOR 0x8E266332`,
`DT_SNO 0xA4C45887`. Full detail: `casc-diablo4-format.md` §10.

## 4. API delivered (call these)

Package: `artifacts/fr-c7-pack/WiseOwl.Casc.Diablo4.0.1.1-alpha.nupkg`
(+ `WiseOwl.Casc`). **Not published** — release is the owner's gated
call; integrate the local pack or await the published version.

```csharp
// Game-wide D4 hashes (owner-approved §3.2; reusable for ANY D4 SNO)
uint Diablo4.TypeHash(string)   // DJB2 seed 0, full u32
uint Diablo4.FieldHash(string)  // TypeHash & 0x0FFFFFFF
uint Diablo4.GbidHash(string)   // lowercased (pre-existing)

// Generic raw widget graph — any 0xE4825AB8 UI-scene SNO (scope B)
UiScene Diablo4Storage.ReadUiScene(int snoId)
//   UiScene(SnoId, Widgets); UiWidget(Name, ClassId, Fields);
//   UiField(FieldHash, TypeHash, RawValue, HasValue)
//   raw graph only — no evaluator/imaging/policy

// Typed paragon projection (§7.1)
ParagonRenderLayout Diablo4Storage.ReadParagonRenderLayout()
```

`ParagonRenderLayout` = `Ratios` (`RenderRatios`), `CanvasReference`,
`NodeContainer`, `NodeTemplate` (raw `WidgetRect`s, audit), `int
BoardRotationQuadrant`, `Disc`, `Symbol` (`NodeElement`), `States`
(18 × `StateElements`). Texture handles are raw `uint`
(`== TexFrame.ImageHandle`, never pre-resolved — Q4).

## 5. Results — the decoded numbers (verified vs the live build `3.0.2.71886`)

| Quantity | Value | Source |
|---|---|---|
| Canvas reference | **1920 × 1200** | `ParagonBoard_main` root rect |
| Node element box (pitch) | **100 ref units** (square, uniform) | `Template_Node_Common` |
| Disc draw | **86 ref** (100 − 2×7 insets) | `Node_IconBase` |
| `RenderRatios.PitchRef` | **100/1200** ≈ 0.08333 | decode-true |
| `RenderRatios.DiscRef` | **86/1200** ≈ 0.07167 | decode-true |
| `Ornate/Symbol/SocketRing ÷ Disc` | **100/86** ≈ 1.163 | elements fill the node box |
| `BoardRotationQuadrant` | **0** (Warlock-Start, axis-aligned) | CL-10; int 0/1/2/3, 45° unrepresentable |
| `RenderRatios.Provisional` | **false** | over-determined check passed |

**Anchor reproduction (gate-2).** Your dual-validated oracle
(67.7 px/grid at {zoom 0, 7680×2160, *Warlock Start*, nothing
selected}) — autocorr 67.59(X)/67.81(Y) (square) + span gate(10,0)→
start(10,14)=951.5/14=67.96 — all ÷ the decode-true 100-ref pitch
converge to **≈0.677 px/ref (≤0.4 px)**. A single uniform 100-ref box
*predicts* a square uniform lattice at one scale; your two independent
measurements confirm it. That is proof by over-determination, not
inference. Your implied scale at that provenance ≈ **0.677 px/ref**;
`IconCellFactor = PitchRef × yourCanvasPx`, your zoom/resolution scalar
owned by you (≈ `renderH/1200 × zoom`).

**State matrix (gate-1).** Exactly the 18 §7.2 rows, verbatim keys.
Layers from decode-true bound elements: `Node_IconBase` base disc
`0x1D166DC7` (all rows); `NodeAvailableGlow` gold ornate `0x4A901508`
(Rare/Legendary); `GlyphNodeGlow_Revealed` pulse `0xBED4CF21`
(socket.unselected). Texture handles bind via field `0x0C152636`
(type `0x6B1C5D9C`, a texture-handle DT).

## 6. What is NOT in the data — keep your code for these (evidence-backed)

Critical so you do **not** wait for data that does not exist:

- **Per-rarity colour.** Scanning the whole scene, `rgbaTint` is bound
  only on non-node widgets (`BlackScreen`, `Usage_Slot_2`,
  `Template_GlyphAura_Tile`). **No per-rarity tint is authored
  anywhere.** §2.3 is definitively confirmed: per-rarity colour is a
  **fixed shader recipe** — permanently yours. `StateElements.Tint`/
  `LitTint` = `null` is the *decoded answer*, not a gap. Keep
  `IconPipeline.RarityRecipe`/`RarityGain` as the authoritative model.
- **Grey rim ring, selection ring, connector bars, pointer triangles.**
  Absent from `ParagonBoard` → **app-drawn / procedural** (matches your
  FR §2.5). `GreyRingOverDisc=0`; the 3 `overlay.*` rows have empty
  `Layers`. Keep your catalogued procedural handles
  (`77ECA3A8`/`288DE11F`; `6D3CB8DE`/`8EEAC178`/`B6D8C741`/`D51CAB25`).
- **Pulse / ornate animation.** No authored float anim fields on the
  pulse widgets → **engine-driven**. `AnimSpec=null` is correct; bake a
  representative static frame as you planned.

## 7. How to integrate

1. Reference the packed `WiseOwl.Casc.Diablo4 0.1.1-alpha` (or the
   published package once the owner cuts the gated release).
2. `var rl = d4.ReadParagonRenderLayout();`
3. Replace your interim *ratio/relationship* constants with
   `rl.Ratios` (`PitchRef`, `DiscRef`, `*OverDisc`) and the decoded
   `rl.CanvasReference` / rects. Keep your single resolution/zoom
   scalar (`IconCellFactor` basis) — it is yours by the boundary, not a
   stand-in we erase.
4. Node centre: `gridXY` (from `ParagonBoardDefinition`, group 108,
   already in the library) × `PitchRef` × yourCanvasPx; element px =
   ratio × disc px. `BoardRotationQuadrant` is 0 for Warlock-Start
   (decoded, never assume 45°).
5. For tint/overlays/anim: keep your existing recipe/procedural code —
   §6 explains why (not in the data, by design).
6. Need the raw graph for anything else? `ReadUiScene(snoId)` exposes
   every widget/field/value for any `0xE4825AB8` SNO.

## 8. Corrections relevant to you (`CL-*`, all caught before ship)

- **CL-9** format located + decoded; D4 hash cracked.
- **CL-10** board rotation is a 90° quadrant, never 45°
  (`BoardRotationQuadrant:int`, 0 at Warlock-Start).
- **CL-11** state contract is **15 baked + 3 overlay = 18** (round-4b
  "17" was the arithmetic slip `4×2+3+2+2=15`).
- **CL-12** `ReadUiScene` is an independent generic surface (scope B).
- **CL-13** record-header framing pinned
  (`classOff = nameStart + alignUp8(len+1) + 0x10`) — an earlier
  over-generalised model was caught and corrected.
- **CL-14** the exploratory recon tool over-attributed a rect by
  nearest-name (it claimed `ParagonNodes 450×1115`; the authoritative
  header-pinned `ReadUiScene` proves `450 = SidePanel_Content` and
  `ParagonNodes`'s own rect is runtime-bound). The shipped parser is
  authoritative; the recon tool is recon-only.

No fabricated value was ever shipped: where the data is silent the API
returns `0`/`null` with documented, evidence-backed reasoning.

## 9. Boundary & what is owed

Library = raw decoded fields + the documented derivation rule. No
evaluator, imaging, scoring, or resolution/zoom math (all yours).
**Nothing further is owed** unless: (a) the owner publishes
`0.1.1-alpha` via the gated release (then switch the package
reference from the local pack to nuget.org), or (b) a seasonal D4
build changes `.build.info` Build Key — then re-verify (Appendix D);
the decoded ratios are stable unless Blizzard re-authors the paragon
UI scene. The §7 contract is amendable via this loop until that
publish; reopen here if you need a shape change before then.
