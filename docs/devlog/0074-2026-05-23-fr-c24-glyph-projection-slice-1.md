# 0074 — FR-C24: glyph projection — sibling-StringList slice (slice 1 of N)

2026-05-23 · CL-79 · branch `fr-c24-glyph-projection`

## Trigger

Optimizer's `casc-fr#36` — the consumer can't render glyph tooltips
without per-glyph display data, the Maxroll-stopgap is gone, and
the App has no glyph data at runtime. The FR asks for **11 new
fields** total — 5 on `ParagonGlyphDefinition`, 5 on
`ParagonGlyphAffixDefinition` + 1 refinement.

## Scope of this slice

Per protocol §3 — ship the sibling-StringList slice first, propose
a counter-round for the deeper byte-layout RE if the Optimizer
needs everything before they can run the consume-verify.

Three of the eleven fields:

| Field | On | Source |
|---|---|---|
| `LocalizedTitle` | `ParagonGlyphDefinition` | `Item_ParagonGlyph_<SnoName>` label `Name` (strip `"Glyph: "` prefix) |
| `Rarity` | `ParagonGlyphDefinition` | SnoName leading-token convention |
| `Description` | `ParagonGlyphAffixDefinition` | `ParagonGlyphAffix_<SnoName>` label `Desc` (raw template) |

The other eight fields (`BaseRadius`, `RadiusUpgradeLevels`,
`MaxLevel`, `DisplayFactor`, `AffectedAttributes`,
`SkillTagSelector`, `Requirements`, plus the `AffectedRarity`
refinement) require byte-layout RE — each is in the glyph or
affix's payload but not at any spec-documented offset yet.
Deferred to a follow-on CL.

## Sibling-StringList conventions found

The Optimizer's FR guessed `ParagonGlyph_<SnoName>` and
`ParagonAffix_<SnoName>`; the actual engine names differ slightly:

```
Item_ParagonGlyph_<SnoName>   → label "Name", value "Glyph: <Title>"
ParagonGlyphAffix_<SnoName>   → label "Desc", value <template>
```

The `"Item_"` prefix on the glyph table aligns with how the engine
classifies a glyph as an Item-style resource (the Item domain also
uses this prefix pattern for item names). The `"Glyph: "` prefix in
the *value* is stripped library-side — the Optimizer's example
tooltip headers (`"Attrition"`, `"Abyssal"`, `"Guzzler"`) confirm
the consumer wants the bare title, not the engine's display
prefix.

Affix descriptions stay raw — they're **templates** with engine
markup the consumer renders:

```
For every 5 Intelligence purchased within range, you deal
{c_number}+[{GlyphAffixScalar}|1%|]{/c} increased damage while
{c_important}{u}Healthy{/u}{/c}.
```

The color tags (`{c_*}{/c}`), underline (`{u}{/u}`), and the value
placeholder (`[{GlyphAffixScalar}|1%|]`) are all preserved per the
FR's explicit ask.

## What I deferred + why

| Field | Why deferred |
|---|---|
| `BaseRadius` / `RadiusUpgradeLevels` / `MaxLevel` | Need byte-layout RE of `nStartingSize` / `arSizeUpgradeLevels` / `nMaxLevel`. Glyph payload has values that *might* be these (`payload+20 == 1`, `payload+40 == 1`, etc.) but nothing reads cleanly as `3` / `[25, 50]` / `150` without further investigation. |
| `DisplayFactor` | `flDisplayFactor` is a `DT_FLOAT` somewhere on the affix payload that isn't at the existing `+76` (`Base`) or `+80` (`PerLevel`). Need to dump multiple affixes and correlate. |
| `AffectedAttributes` | `DT_VARIABLEARRAY[DT_INT]` somewhere on the affix payload; the existing decode hits `+24` (rarity), `+48` (op), `+76` (base), `+80` (perLevel) — the attribute-list descriptor lives elsewhere. |
| `SkillTagSelector` | The "Abyss Skills" / "Archfiend Skills" qualifier on per-skill-tag affixes. Probably a `DT_GBID` field referencing a skill-tag SNO. |
| `Requirements` | Variable-length `(AttributeId, Magnitude, ThresholdScope)` rows — the `Required (purchased in range): +40 Willpower` block. Likely a `DT_VARIABLEARRAY` of a sub-struct; needs the row stride identified. |

The Warlock-21 oracle in the FR is partial-blocked on these
fields — the consumer can show glyph titles + affix descriptions
today but can't render magnitudes correctly without
`DisplayFactor`, and can't render the threshold / scope without
`Requirements`.

## Surface

```csharp
// On ParagonGlyphDefinition (new fields):
public string LocalizedTitle { get; }    // "Guzzler"
public ParagonRarity Rarity { get; }     // ParagonRarity.Rare

// On ParagonGlyphAffixDefinition (new field):
public string Description { get; }       // raw template

// On Diablo4Storage (new locale overloads):
public ParagonGlyphDefinition ReadParagonGlyph(int id, string locale);
public ParagonGlyphAffixDefinition ReadParagonGlyphAffix(int id, string locale);
// (the no-locale overloads forward to DefaultLocale = "enUS")
```

## Acceptance

Live anchor on the Optimizer's Warlock-21 oracle (row 13):

```
ReadParagonGlyph(1023194)  ('Rare_011_Intelligence_Side')
  -> LocalizedTitle == "Guzzler"
  -> Rarity == ParagonRarity.Rare
  -> UsableByClassSnoIds still populated (CL-18 unchanged)

ReadParagonGlyphAffix(1068542)  (affix on Guzzler)
  -> Description carries the raw template with
     "[{GlyphAffixScalar}" placeholder marker preserved
```

126/126 tests green on `3.0.2.71886`.

## What's next

- **CL-80 candidate**: structural fields slice — pin
  `BaseRadius` / `RadiusUpgradeLevels` / `MaxLevel` on glyph +
  `DisplayFactor` / `AffectedAttributes` / `SkillTagSelector` on
  affix. Need a focused glyph-blob dump session to identify the
  byte offsets.
- **CL-81 candidate**: `Requirements` variable-length list — the
  most complex of the deferred fields; would likely close the
  Warlock-21 oracle acceptance.

If the Optimizer flags structural fields as blocking, the deferred
work becomes the next FR-C24 counter-round; otherwise it can split
into a fresh issue per §3.
