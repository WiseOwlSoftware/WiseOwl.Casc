# FR-C13 — session handoff (start here in the next CASC session)

> Hand-off from the 2026-05-19 long session, paused at `needs:owner`
> on issue [#23](https://github.com/WiseOwlSoftware/casc-fr/issues/23).
> This doc is the **starting point** for the next CASC session that
> picks up FR-C13 — read it before re-doing any of the RE.

## State at handoff

- **FR**: [WiseOwlSoftware/casc-fr#23](https://github.com/WiseOwlSoftware/casc-fr/issues/23)
  — surface the Power record's structured numeric effect data
  (SF_N substitution slots) for Legendary node Powers; parallel to
  `ParagonGlyphAffix` / `ParagonNodeAttribute`.
- **Labels**: `fr:proposed`, `fr:accepted`, `needs:owner`.
- **Outstanding asks to the Optimizer** (posted as a `[CASC]`
  comment on #23, 2026-05-19): see [issue thread](https://github.com/WiseOwlSoftware/casc-fr/issues/23)
  for the two asks (model-framing answer + 3–5 verifiable anchors).
  Resume only once at least the model-framing answer is in hand.

## What the previous session settled

Comprehensive RE writeup in [`docs/devlog/0028-2026-05-19-fr-c13-r1-power-record-re-intel.md`](devlog/0028-2026-05-19-fr-c13-r1-power-record-re-intel.md).
Highlights:

- **Verified vs build `3.0.2.71886`** (the per-cost
  `DT_STRING_FORMULA` slot offsets and tail-data shape):
  - `tAttackSpeed @ 0x198`, `tCombatEffectChance @ 0x1d0`,
    `tHealthCost @ 0x230`, `tChargeCost @ 0x250`,
    `tMaxCharges @ 0x270`, `tRechargeTime @ 0x290`,
    `tCooldownTime @ 0x2b0`,
    `tResourceCostReductionCoefficient @ 0x2d0`,
    `tCooldownReductionCoefficient @ 0x2f0`,
    `tAttackRadius @ 0x420` — all carry valid `(offset, length)`
    pointers into the blob's tail-data region.
  - **Tail data @ `0xCE0`** for Dynamism begins with the ASCII
    identifier `Attacks_Per_Second_Total` followed by 16-byte
    repeating structures encoding `(0, NN, 6, float)` where `NN`
    is `48` / `49` / `54` (ASCII `'0'` / `'1'` / `'6'`) and the
    trailing `float` is the matching scalar (`0.0` / `1.0` / `6.0`).
    Looks like an inline expression-AST: type-marker + arity +
    scalar. **Not fully decoded** — needs anchor data (Ask 2).

- **Stale vs build `3.0.2.71886`** (schema-claimed but not
  verifying):
  - `nFormulaCount @ 0xb30` and `ptScriptFormulas @ 0xb38` don't
    decode cleanly. Dynamism: `count=0`, `ptr=0/0` — but its
    Description has `SF_0`/`SF_2`/`SF_3`. Active-skill blobs
    (Druid_Boulder, Druid_Trample): GBID-shaped values where the
    schema expects a count.
  - Either the struct has shifted in this build, or the FR conflated
    mechanisms (SF_N for Legendary passives may live on
    `ParagonNode.Effects[]`, which CASC already exposes typed via
    FR-D). Ask 1 settles this.

## Third-party intel — already consulted

Per memory `feedback_third-party-re-as-intel` (load it at session
start). Cited, not imported:

- [blizzhackers/d4data (archived, richest schemas)](https://github.com/blizzhackers/d4data) — relevant files:
  - `definitions/!PowerDefinition.14af0cb6.yml`
  - `definitions/!ScriptFormulaInfo.20e37537.yml`
  - `basic_definitions/!DT_STRING_FORMULA.920cd243.yml`
- [DiabloTools/d4data (active successor)](https://github.com/DiabloTools/d4data) — current-season checksums + parsed JSON.
- [Dakota628/d4parse (Go parser)](https://github.com/Dakota628/d4parse) — uses the d4data definitions.

**Not yet fetched** (cheap; do these early in the next session if
the model-framing answer is "Power record holds the effect data"):

- `definitions/!PowerBuffDefinition.3fd70b39.yml` — typed buff
  effects (likely the operator + base + scaling shape the FR wants).
- `definitions/!PowerBuffAttributeModifier.b6de0233.yml` —
  attribute-modifier-grade effect data.
- `definitions/!PowerPayloadDefinition.*.yml` — payload (damage /
  proc) effects.

These describe `arBuffs` / `arPayloads` / `arMods` arrays, likely
where the typed-effect data the FR wants actually lives (the same
shape as `ParagonGlyphAffix`).

## Tools / probes ready

- `e:/tmp/scene-probe/` — working .NET 10 probe project, references
  the local CASC builds. Currently configured with SixLabors.ImageSharp
  3.1.12 (no vulnerability warning, license-free 3.x line).
  Replace `Program.cs` to write a new probe; existing reads use
  `Diablo4Storage.ReadSno(SnoGroup.Power, snoId)`.
- Anchor blobs already inspected:
  - `Dynamism` SNO `2524312` (Legendary passive, ~6,808 B)
  - `Druid_Boulder` SNO `238345` (~20,704 B)
  - `Druid_Trample` SNO `258243` (~21,424 B)
  - `Druid_Resource_Charges_TEMP` SNO `238180` (~6,228 B)

## Suggested first steps when resuming

1. **Read this doc + devlog 0028 + memory `feedback_third-party-re-as-intel` + `feedback_casc-fr-loop`.**
2. **Check #23 for the Optimizer's answers.** If neither ask is
   answered, pause again — CASC's RE doesn't unblock without that
   direction.
3. **If Ask 1 answer is (b)** (`ParagonNode.Effects[]` already
   carries the SF_N data): re-frame to a documentation round on the
   existing `ParagonNodeAttribute` surface. Probably ~30 min of work.
4. **If Ask 1 answer is (a) or (c)** + Ask 2 anchors are in hand:
   start the RE pass against the anchors. Each anchor's known float
   value should appear in the tail-data 16-byte AST nodes — find it
   by byte-pattern search, then map back to the slot offset.
5. **Keep CASC first-party.** Cite third-party schemas in the new
   devlog as RE intel; never import their data/code.

## Don't redo in the next session

- The web-search for community schemas (already done; cited in
  devlog 0028).
- The Dynamism/Druid blob structural probes (already done; values in
  devlog 0028).
- The verified per-cost slot offsets (already documented above).
- Hashing texture-name candidates against rim handles — that was a
  separate FR-C11 R3 §1 R&D dead-end (devlog 0027), unrelated.

## Open Phase-2 from the prior session (unrelated to FR-C13)

[FR-C11 R3 §1](https://github.com/WiseOwlSoftware/casc-fr/issues/21)
is also at `needs:owner` — the non-icon-catalog rim-texture-handle
resolution subsystem. Don't pick that up unless the owner directs;
it's its own dedicated session.
