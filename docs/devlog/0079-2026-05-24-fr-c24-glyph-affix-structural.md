# 0079 — FR-C24 slice 2b: glyph-affix structural decode (op-coupled layout)

2026-05-24 · CL-84 · branch `fr-c24-slice-2b`

## Trigger

CL-83 closed the 3 glyph-side fields and left the affix half (`casc-fr#36`)
explicitly open as slice 2b. The Optimizer asked for 4 affix-side
fields + an `AffectedRarity` refinement:
`DisplayFactor`, `AffectedAttributes`, `SkillTagSelector`, `Requirements`,
and rarity → `ParagonRarity?`.

## Recon

Initial single-affix recon in 0078 misread the data — `+72` was called
a "SkillTagSelector" candidate and `+120/+124` was wrongly read as
`(152, 12)` on a specific record but generalised across records.
Rebuilt the picture with a new `build/SnoScan glyphaffixscan` command
that dumps the candidate offsets across all 314 group-112 records and
clusters by `(operation, length, +72, +84, +88, va2_size)`. Six
distinct clusters fall out immediately:

| Op | Cluster |
|--:|---|
| 1  | length=152, +72=`0x97F7EB01`, +84=100, va2_size=0; 5 rows |
| 2  | length=180, +72=`0x16A2B4DF` (`_Side`) or `0x169F493F` (`_Main`), +84=500, va1 at `+64/+68`, va2 at `+120/+124`; ~110 rows |
| 4  | length=164–176, +72=`-1`, +84=100, va1 at `+104/+108`; ~42 rows |
| 5  | length=168–180, +72=`-1`, +84=1, snoPower at `+88` ≠ `-1`; ~50 rows in 25+ clusters keyed by their distinct linked Power SNO |

That made the structural shape obvious — each op uses a **different
descriptor slot** for its `ptAttributes` VLA, because the four ops
are four distinct schema fields (`Attribute` / `NodeAmplification` /
`AttributeConversion` / `Power`) sharing one base record. Op-5 doesn't
have a `ptAttributes` array at all — its magnitude lives in the linked
`PowerDefinition` (group 29) referenced from `snoPower@+88`. Multiple
Op-5 affixes resolve to the same Power (`DamageElite__{Dexterity,
Intelligence,Strength_Legendary,Willpower}` all link
`ParagonGlyph_DamageElite` = sno `2072755`).

## Op-coupled byte layout

| Offset    | Field                                          | Op-1 | Op-2 | Op-4 | Op-5 |
|--:        |---                                             |--:   |--:   |--:   |--:   |
| 0         | `snoId`                                        | y    | y    | y    | y    |
| 16, 20    | `ptAttributes` descriptor                      | **here** | —    | —    | —    |
| 24        | `eAffectedNodeRarity`                          | 0    | 0    | 0    | 0    |
| 48        | `eBonusOperation`                              | 1    | 2    | 4    | 5    |
| 56, 60    | first VLA (single u32 placeholder)             | —    | —    | —    | y    |
| 64, 68    | `ptAttributes` descriptor                      | —    | **here** | —    | —    |
| 72        | main/side GBID                                 | `0x97F7EB01` | `0x169F…/0x16A2…` | `-1` | `-1` |
| 76        | `flStartingBonusScalar` (`Base`)               | y    | y    | y    | 0    |
| 80        | `flAddedBonusScalarPerLevel` (`PerLevel`)      | y    | y    | y    | 0    |
| 84        | `flDisplayFactor`                              | 100  | 500  | 100  | 1    |
| 88        | `snoPower`                                     | `-1` | `-1` | `-1` | **PowerDefinition ref** |
| 104, 108  | `ptAttributes` descriptor                      | —    | —    | **here** | —    |
| 120, 124  | `Tags` GBID list descriptor                    | y    | y    | y    | y    |

`ptAttributes` element stride is 8 bytes — a packed
`(int AttributeId, uint ParamPlus12)` pair, mirroring the trimmed
shape of the `ParagonNodeDefinition` ptAttributes record (the full
node-attribute is 88 bytes; glyph affixes use only the first two
fields).

## What does `+84` actually mean?

The Optimizer's existing `/100` divisor "is wrong somewhere" was
right — but the per-op constant `+84` carries (`100/500/1`) isn't
the "per-affix DisplayFactor" they were hoping for. It's an
**operation-coupled engine constant**: every Op-1 / Op-4 affix has
`+84 = 100`, every Op-2 has `500`, every Op-5 has `1`. The display
math for a percent magnitude is:

```
Op-1, Op-4 :  (Base + (L−1)·PerLevel) × 100 / DisplayFactor   = percent
Op-2       :  (Base + (L−1)·PerLevel)                          = percent (no division)
Op-5       :  magnitude lives in the linked PowerDefinition
```

The Op-2 case is what tripped the consumer — Base for `AbyssDamage_
Willpower_Main` is `1.0` and the in-game tooltip shows `+1.0%` at L1,
so dividing by 100 would have shown `+0.01%`. The library surfaces
`DisplayFactor` as a `double` so the consumer can switch on op or
use whatever convention they prefer; the docs explain the per-op
relationship.

## What's NOT in the `.gaf` bytes

The FR's `Requirements` list (`(AttributeId, Magnitude, Scope)`
rows) is not encoded anywhere in the record. Both shapes the FR
mentions are runtime-bound on the same axis as the encrypted
controller code (memory `[[project_engine-controller-code-encrypted]]`):

- The per-class "+40 Willpower" / "+25 Intelligence" gate that
  shows on every Op-2 main affix — class-coupled (every Warlock
  glyph requires +40 W, every Sorcerer glyph requires +25 I).
- The "unlocks at Level 50" gate on Op-4 `Mult*_Legendary` —
  legendary slot is gated by glyph level, gate is engine-side.

Both are honest absences — the library boundary stops at what is
structurally encoded. The consumer hard-codes the class threshold
table per class and the legendary unlock level (50) once, the same
way [[project_engine-controller-code-encrypted]] said it would.

## What ships

```csharp
public sealed class ParagonGlyphAffixDefinition
{
    // existing identity / CL-79 fields kept
    public int SnoId { get; }
    public string Description { get; }   // CL-79 sibling StringList

    // CL-84 — slice 2b structural fields
    public int Operation { get; }                                  // raw int (1/2/4/5)
    public ParagonGlyphAffixOperation OperationKind { get; }       // typed enum
    public float Base { get; }
    public float PerLevel { get; }
    public double DisplayFactor { get; }                           // +84 verbatim
    public int? LinkedPowerSnoId { get; }                          // group 29 ref on Op-5
    public IReadOnlyList<GlyphAffixAttributeRef> AffectedAttributes { get; }
    public IReadOnlyList<uint> Tags { get; }                       // raw GBIDs
    public int AffectedRarity { get; }                             // raw byte (always 0)
    public ParagonRarity? AffectedRarityKind { get; }              // typed null
}

public readonly record struct GlyphAffixAttributeRef(int AttributeId, uint ParamPlus12)
{
    public const uint NoParam = 0xFFFFFFFF;
    public bool HasParam { get; }
}

public enum ParagonGlyphAffixOperation
{
    Unknown = 0, Attribute = 1, NodeAmplification = 2,
    AttributeConversion = 4, Power = 5,
}
```

## Tests

The existing synthetic `B4_glyph_affix_round_trips` is extended to
prove the Op-1 `+16/+20` descriptor walk + DisplayFactor +
ParagonRarityKind + 2 packed AttributeRef entries (one with no tag,
one with the Abyss tag GBID). A new `B4_glyph_affix_op5_surfaces_
linked_power` covers the Op-5 shape (no per-attribute VLA, linked
power SNO surfaced).

The live `Acceptance_matrix_against_live_install` exercises one
affix per op:

| Op | Anchor sno | Expectation |
|--:|---:|---|
| 1 | `Nodes_BonusToMinion` `1031882` | 27 packed (AttrId, Param) entries, DF=100 |
| 2 | `DamageWhileHealthy_Intelligence_Side` `1068542` | 2 attrs, 3 tags, DF=500 |
| 4 | `MultCritDmgPercent_Legendary` `2111927` | 1 attr, DF=100 |
| 5 | `DamageElite__Strength_Legendary` `2098405` | LinkedPowerSnoId=`2072755` `ParagonGlyph_DamageElite` |

127/127 tests green on `3.0.2.71886` (was 126).

## Recon tool committed

`build/SnoScan glyphaffixscan [substr] [max]` — tab-separated dump
of every group-112 record's slice-2b candidate offsets + VLA
contents. Same family as `attrmap` / `nodeinfo` / `formula`.
