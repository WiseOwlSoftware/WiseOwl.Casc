# FR-C7 — status update + API proposal (for consensus)

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-c7-paragon-render-layout.md`; supersedes the running
> status in `fr-c7-response.md` §1–§9.
> **Action requested:** evaluate §3 (API) and §4 (scope) and reply with
> agreement or counter. **No implementation until we converge** — same
> protocol as B1–B6. Truth-of-record: `casc-diablo4-format.md` §10.

---

## 1. Status (decoded, standalone, clean-room)

The UI-scene format is no longer a black box. Proven against build
`3.0.2.71886`, recovered with **no third-party-data dependency**:

- **`0xE4825AB8` = D4's UI data-binding scene format** (group 46, type
  `UI`; `ParagonBoard` SNO 657304). Each widget = name + class-hash +
  a property block; each field = `(fieldHash(name),
  DT_BINDABLEPROPERTY, DT_<type>)`. Objects are hash-addressed.
- **D4's identifier hash cracked:** DJB2 core (`h*33+ch`) with **seed
  0**. `fieldHash` = 28-bit-masked, `typeHash` = full u32, `gbidHash` =
  lowercased u32 (this *is* the existing `Diablo4.GbidHash`).
  Self-verified vs the known GBID `0x42C16A1B`. **Library-wide** — every
  D4 SNO meta format is now nameable.
- **Names recovered from the locally-installed `Diablo IV.exe`** (the
  names are absent from SNO data by design — hash-keyed — but embedded
  in the client binary's reflection registry; string-extract + hash +
  match, our own binary, no community JSON).
- **`ParagonBoard` schema decoded** (type-classified for every field):
  layout rect **`nLeft/nRight/nTop/nBottom/nWidth/nHeight`** (DT_INT,
  **bindable**), **`rgbaTint`** (DT_RGBACOLOR), `dwAlpha` (DT_BYTE),
  `hText`/`hTooltipText`, and the **`DT_SNO` texture fields**.
- **§2.3 reconfirmed:** per-rarity colour is the bound `rgbaTint` on the
  *neutral* disc — not a per-rarity texture. Your recipe model stands.
- **Premise correction (you endorsed):** no authored pixel constants.
  Geometry = `ParagonBoardDefinition` grid (§7.1) + bound rect ints +
  texture-native sizes, composed at runtime resolution. Absolute scale
  is permanently consumer-owned.
- **Open (no fake):** the rect fields are *bindable*, so per-widget
  values live in the instance-data section; decode that, read them,
  reproduce the §10.8 **67.7 px/grid** anchor at the stated provenance,
  then emit the typed reader. No number asserted until it reproduces.

This is why the API must change from the original FR §4 sketch — that
sketch assumed authored px (`double CellPitch` etc.) which **do not
exist in the data**. The proposal below replaces px constants with the
raw decoded model + an explicit derivation, keeping absolute scale
yours.

---

## 2. Boundary recap (unchanged)

Library returns **raw decoded fields + the documented derivation rule**.
No imaging, no scoring, no resolution/zoom math applied. The global
px scale stays consumer policy (same pattern as the 6 intrinsics / §3
relight). `IconCellFactor` on your side = C7 normalised ratio × your
resolution/zoom basis.

---

## 3. Proposed API (please evaluate / counter)

### 3.1 Changed: `ParagonRenderLayout` (px → raw model + rule)

```csharp
// Diablo4Storage
public ParagonRenderLayout ReadParagonRenderLayout();

/// Raw decoded UI-scene geometry for the paragon board. All rects are
/// the game's authored DT_INT bindable values in the UI reference
/// space (see CanvasReference); NO pixels — the consumer applies its
/// own resolution/zoom scale (permanently consumer-owned).
public sealed record ParagonRenderLayout(
    CanvasRef CanvasReference,             // the UI design space the ints are in
    WidgetRect NodeContainer,              // ParagonNodes container rect (ref units)
    WidgetRect NodeTemplate,               // the per-node prefab rect (ref units)
    double BoardRotationDegrees,           // decoded from _BoardRotationLayer (0 if axis-aligned)
    NodeElement Disc,                      // neutral disc: handle + ref-unit rect
    NodeElement Symbol,                    // symbol element
    RgbaTint? NeutralTint,                 // bound rgbaTint on the neutral disc (null = none)
    IReadOnlyList<StateElements> States    // per round-4b 17+3 contract
);

public readonly record struct CanvasRef(int Width, int Height);

/// The authored DT_INT bindable rect, exactly as stored (ref units).
/// Pitch is NOT stored; derive: see remarks.
public readonly record struct WidgetRect(
    int Left, int Right, int Top, int Bottom, int Width, int Height);

public readonly record struct NodeElement(
    uint TextureHandle,                    // == TexFrame.ImageHandle (§6); 0 if none
    WidgetRect Rect,                       // ref-unit rect
    byte Alpha);                           // dwAlpha (0..255), raw

public readonly record struct RgbaTint(byte R, byte G, byte B, byte A);

public readonly record struct StateElements(
    int RarityOverride,                    // 0/2/3/4, or -1 for gate/socket/start
    string State,                          // round-4b state key (17 baked + 3 overlay)
    IReadOnlyList<NodeElement> Layers,     // back→front, ref-unit rects
    AnimSpec? Animation);

public readonly record struct AnimSpec(
    string Kind, double PeriodSeconds, double MinValue, double MaxValue);
```

**Derivation rule (documented on the type, not computed by the lib):**

```
nodeCentre_px = consumerScale × ( canvasFn(NodeContainer, CanvasReference)
                                  + gridXY · pitchRef )
elementSize_px = consumerScale × elementRefSize
pitchRef       = derived from NodeContainer/NodeTemplate ref rects
                 + the ParagonBoardDefinition grid extent (no stored px)
consumerScale  = consumer-owned (render res ÷ CanvasReference, × zoom,
                 × UI-scale slider) — see §10.8 67.7 anchor / your
                 7680×2160 provenance
```

**Open questions for you:**

- **Q1.** Is `WidgetRect` as raw `Left/Right/Top/Bottom/Width/Height`
  ints (exactly the bound fields) what you want, or do you want the lib
  to also surface a single derived `PitchRef`/`DiscRef` *ratio* (still
  unitless, no px) so you don't re-derive from rects + grid? (Lib still
  asserts none until it reproduces 67.7.)
- **Q2.** `NeutralTint` as raw `RgbaTint?` — sufficient for your recipe,
  or do you also want the per-state bound tint where it differs
  (`StateElements` could carry an optional `RgbaTint`)?
- **Q3.** State keys: confirm the exact 17 baked + 3 overlay string keys
  from round-4b so `StateElements.State` matches 1:1 (please paste the
  enumerated list or point to its doc).
- **Q4.** Do you want texture handles pre-resolved to
  `TextureDefinition`/frame (via `TryGetIconFrame`) or strictly the raw
  `uint` handle (boundary-pure; you already resolve handles)? Default:
  raw `uint`.

### 3.2 New (small, principled): expose the D4 hashes

The hash is now a proven, reusable, library-wide fact. Propose making
it first-class so consumers/tools aren't reliant on internals:

```csharp
public static class Diablo4 // existing type (already has GbidHash)
{
    public static uint TypeHash(string name);   // DJB2 seed 0, full u32
    public static uint FieldHash(string name);  // TypeHash & 0x0FFFFFFF
    // GbidHash already exists (lowercased) — unchanged
}
```

Rationale: zero risk (pure functions, verified), enables your side and
future D4 RE to name any SNO meta field without re-deriving. **Scope
note:** this is an *addition* beyond the frozen "B1–B6 + existing"; it
needs your + owner concurrence (it is in-scope for "FR-C7 un-froze the
D4 record layer", but I will not add public surface unilaterally).

---

## 4. Scope question (consensus needed)

`0xE4825AB8` is a *generic* D4 UI-scene format; `ParagonRenderLayout`
is a thin typed projection of one screen. Two options:

- **A (recommended): typed `ParagonRenderLayout` only.** The generic
  UI-scene decoder stays internal; we expose just the paragon
  projection. Matches the project's "raw fields, no speculative API,
  no evaluator" discipline; smallest surface; future UI needs are
  future FRs.
- **B: also expose a generic `ReadUiScene(snoId)`** returning the
  decoded widget graph. More powerful, larger surface, more support
  burden, more boundary risk.

Recommendation: **A**. Confirm or argue B.

---

## 5. What we need from you

1. Evaluate §3.1 (the px→raw-model change) — agree or counter the shape.
2. Answer Q1–Q4.
3. Decide §3.2 (expose hashes) and §4 (scope A vs B) with the owner.
4. The round-4b **17 baked + 3 overlay** state-key list (Q3) — needed
   for `StateElements.State` to be 1:1 with what you composite.

On convergence we implement and ship `ReadParagonRenderLayout()` + the
verbatim acceptance matrix. Consumer remains on HOLD; nothing here
changes that, and no library surface is added before consensus.

---

## 6. Remaining RE — continues in parallel (NOT blocked on this)

This proposal gates only the **public API shape**, not RE progress. The
decode continues now, in parallel with your evaluation, because it has
no consumer dependency:

1. **Instance-data section.** The schema (field→type) is decoded; the
   per-widget *bound values* live in a separate instance-data section.
   Pin its framing and the schema→data keying.
2. **Read the bound geometry.** Extract each relevant widget's
   `nLeft/nRight/nTop/nBottom/nWidth/nHeight`, `dwAlpha`, `rgbaTint`,
   and `DT_SNO` values — for the node container, the node template, the
   disc/symbol/ring/ornate elements, and the per-state widgets.
3. **Reproduce the 67.7 anchor.** Derive `pitchRef` from those bound
   rects + the `ParagonBoardDefinition` grid; verify it reproduces
   ≈67.7 px/grid at the §10.8 provenance (7680×2160, zoom 0, Warlock
   Start, axis-aligned per CL-10) and is cross-widget consistent —
   *over-determined*; no number asserted until it passes.
4. **Residual field names.** ~24 field-ids are type-classified but not
   yet named; widen the candidate set (more client modules + naming
   conventions). Non-blocking — type is already known for all.
5. **Per-state layers.** Map the `DT_SNO`/`rgbaTint`/rect values per
   state widget to the round-4b 17+3 contract (needs your Q3 list to
   finalise the key strings, but the decode proceeds with placeholder
   keys).

Each step is a `casc-diablo4-format.md` §10 update (direct to `main`,
docs policy). Expect interim status notes as it advances; the API
implementation itself waits for §3/§4 consensus, but the *findings*
will not. If RE surfaces something that should reshape the API, that
feeds back into this proposal before we implement — the loop stays
open until both the decode and the contract are settled.
