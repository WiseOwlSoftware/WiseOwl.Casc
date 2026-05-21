# Devlog 0047 — FR-C16 R10: are per-widget activation conditions in the scene data?

*2026-05-21*

## Context

The owner escalated FR-C16 to an absolute rule: the consumer draws **solely**
from the CASC recipe — it may compute runtime *facts* (selected, reachable,
socketed, rarity, kind, neighbour states) but the *predicates* mapping facts
to layers, the draw order, and all mutual-exclusion / substitution logic MUST
come from CASC, not consumer-authored dispatch. The Optimizer asked CASC to
decode and surface a per-layer typed **activation condition** from scene
657304, and pre-authorized the one bounding outcome: *if a widget's
activation is genuinely engine-code-side and not in any SNO/scene data, report
that explicitly per widget.*

## Foundational decode — the conditions are NOT in the scene data

Four independent lines of evidence (recon `build/SnoScan recdump` / `members`
/ `f32` against live `3.0.2.71886`):

1. **Full `DT_BINDABLEPROPERTY` record** (all 56 bytes, not just value@+0x08):
   `[tag 0x22][0][literal value][44 zero bytes]`. No condition / binding-source
   / expression after the value. The widget header's only extra structure is
   serialization array descriptors (ptr+size — the C++ object's dynamic-array
   headers).
2. **Field vocabulary** = 34 hashes, each one type: `DT_INT`/`DT_ENUM`/
   `DT_BYTE`/`UIImageHandleReference`/`DT_SNO`/`DT_RGBACOLOR`/`StringLabelHandleEx`/
   `DT_CSTRING`. No condition/visibility/predicate type. `bVisible`/
   `eVisibility`/`bShow`/`condition`/`predicate`/`eState`/`bEnabled` hash to
   none of the 34.
3. The only activation datum is the static `bActive` default (literal 0/1) —
   not a runtime condition (same filigree handle is `bActive=1` in Starter,
   `0` in Quest).
4. String table = widget names + hotkey/action ids only; no view-model
   binding-path strings.

So `DT_BINDABLEPROPERTY` = "bindable by the engine's C++ UI controller at
runtime"; the `.ui` asset serializes the view (widget tree + default values +
which props are bindable). The predicate/visibility logic is in the game
executable's UI controller code, not in any SNO field. Confirms FR-C16 R2 (no
structural predicate field) and FR-C7 R7/R8 (select brightness = engine
shader).

## Community-RE cross-check (intel discipline)

Public D4 RE projects do **not** model the `.ui` format: `Bindable` returns
nothing in `blizzhackers/d4data`; a `UIWindowStyle` code search surfaces only
unrelated projects + WiseOwl.Casc's own files. The community parsers
(`Dakota628/d4parse`, `DiabloTools/d4data`) ignore UI definitions (no gameplay
data) — consistent with the conditions not being in the asset. No external
offset map exists; FR-C7 is the only first-party RE. Verified against the raw
blob, nothing copied.

Sources:
- https://github.com/blizzhackers/d4data
- https://github.com/DiabloTools/d4data
- https://github.com/Dakota628/d4parse

## Brightness addendum

Node disc widgets bind no `rgbaTint` (the scene's 6 `rgbaTint` bindings —
field `0x09A3F17B`, cracked this round — are on chrome/text widgets). No
scene-side brighten op on the discs ⇒ the selected-state brightness is **baked
into the selected texture** (`0x72C29402`/`0x03EDABAB`/`0xBD27FB7C`); the
per-state disc swap (CL-47/50) is the complete visual.

## Proposal (handed back, awaiting:optimizer)

"From the CASC recipe" ≠ "from a scene field." CASC already carries engine
knowledge the scene omits (name→predicate framing, class-id cracks, the
`Common_Node_BG_Black → Multiply` mapping). Resolution: CASC ships a typed
`Activation` per layer over a closed fact vocabulary, populated from CASC's RE
+ owner oracle, **provenance-marked** `EngineBehavior` vs `SceneField`; plus
the structurally-backed variant-slot grouping (base-disc family + rarity
templates substituting `Node_IconBase`'s inset-7 slot). Consumer evaluates,
invents nothing. Open question handed back: rule out a separate UI-controller
SNO (R2) first? and is EXE binary RE in scope?

No library code this round — findings + proposal only. Recon: added
`build/SnoScan recdump`.
