# ParagonGlyphAffixDefinition class

A decoded Diablo IV `ParagonGlyphAffixDefinition` (`.gaf`, SNO group 112). Raw fields plus the projected slice (formula identity, affected-attribute pairs, tag GBID list, linked-power ref); magnitude interpretation (operation semantics, level scaling, the per-class threshold gate, legendary unlock level) stays with the consumer per the durable library boundary in `casc-diablo4-format.md` Appendix C.

```csharp
public sealed class ParagonGlyphAffixDefinition
```

## Public Members

| name | description |
| --- | --- |
| static [Parse](ParagonGlyphAffixDefinition/Parse.md)(…) | Decode a ParagonGlyphAffix from its raw SNO blob. |
| [AffectedAttributes](ParagonGlyphAffixDefinition/AffectedAttributes.md) { get; } | FR-C24 (CL-84) — The AttributeIds this affix grants / modifies, paired with their `ParamPlus12` skill-tag GBIDs (the `ptAttributes` array — same shape as on a [`ParagonNodeDefinition`](./ParagonNodeDefinition.md), but with the trimmed 8-byte-per-entry encoding the `.gaf` record uses). Decoded from the op-specific descriptor slot (see remarks on the class). Empty on Op-5 affixes (the magnitude lives in the linked [`LinkedPowerSnoId`](./ParagonGlyphAffixDefinition/LinkedPowerSnoId.md) Power record, not in attribute grants). |
| [AffectedRarity](ParagonGlyphAffixDefinition/AffectedRarity.md) { get; } | `eAffectedNodeRarity` (payload `+24`) — the raw rarity gate int. Universally `0` across every live affix in `3.0.2.71886` (the implicit "any" case); see [`AffectedRarityKind`](./ParagonGlyphAffixDefinition/AffectedRarityKind.md) for the typed view. |
| [AffectedRarityKind](ParagonGlyphAffixDefinition/AffectedRarityKind.md) { get; } | FR-C24 (CL-84) — [`AffectedRarity`](./ParagonGlyphAffixDefinition/AffectedRarity.md) as a typed nullable: `null` when the raw value is `0` (the "any rarity" sentinel), otherwise the corresponding [`ParagonRarity`](./ParagonRarity.md). The current live build only emits `0`; the typed projection is forward-compat for any future season that authors a rarity-specific affix. |
| [Base](ParagonGlyphAffixDefinition/Base.md) { get; } | `flStartingBonusScalar` (payload `+76`) — the level-1 magnitude scalar. Zero on Op-5 (the magnitude lives in the [`LinkedPowerSnoId`](./ParagonGlyphAffixDefinition/LinkedPowerSnoId.md) Power record). |
| [Description](ParagonGlyphAffixDefinition/Description.md) { get; } | FR-C24 (CL-79) — the affix's localized description template (e.g. `"For every 5 Intelligence purchased within range, you deal {c_number}+[{GlyphAffixScalar}|1%|]{/c} increased damage while {c_important}{u}Healthy{/u}{/c}."`) resolved via the §6.7 sibling-StringList convention (`ParagonGlyphAffix_<AffixSnoName>`, label `Desc`). Returned as the raw template — color tags (`{c_…}{/c}`), underline tags (`{u}{/u}`), value placeholders (`[{GlyphAffixScalar}|1%|]`), and the markup tokens the consumer renders (`[x]`, `[+]`, `<Keyword>`) all preserved. Empty when the sibling table is missing or the affix was decoded via the byte-only [`Parse`](./ParagonGlyphAffixDefinition/Parse.md). Populated by [`ReadParagonGlyphAffix`](./Diablo4Storage/ReadParagonGlyphAffix.md). |
| [DisplayFactor](ParagonGlyphAffixDefinition/DisplayFactor.md) { get; } | FR-C24 (CL-84) — `flDisplayFactor` (payload `+84`). Surfaced verbatim as the engine encodes it. Across the live build this is a per-op constant (Op-1/Op-4=`100`, Op-2=`500`, Op-5=`1`) rather than a per-affix value; the consumer determines how it participates in the display formula (the existing assumption of "always 100" is correct only for Op-1 and Op-4). Decoded verbatim because the field is an engine-authored scalar and forward-compat: a future season can author per-affix values without breaking this surface. |
| [LinkedPowerSnoId](ParagonGlyphAffixDefinition/LinkedPowerSnoId.md) { get; } | FR-C24 (CL-84) — On [`OperationKind`](./ParagonGlyphAffixDefinition/OperationKind.md) == Power, the SNO id of the linked `PowerDefinition` (group Power=29) that defines the threshold chain / power-cast behavior the affix triggers. `null` for non-Op-5 affixes (where the underlying field carries the sentinel `-1`). The threshold magnitude itself (e.g. the per-class `+40 Willpower` gate printed on every Warlock glyph) is engine-coupled and not encoded in the `.gaf` record — see Appendix C for the boundary principle. |
| [Operation](ParagonGlyphAffixDefinition/Operation.md) { get; } | `eBonusOperation` (payload `+48`) — the raw op int (1/2/4/5). See [`OperationKind`](./ParagonGlyphAffixDefinition/OperationKind.md) for the named enum. |
| [OperationKind](ParagonGlyphAffixDefinition/OperationKind.md) { get; } | FR-C24 (CL-84) — [`Operation`](./ParagonGlyphAffixDefinition/Operation.md) as a typed enum. |
| [PerLevel](ParagonGlyphAffixDefinition/PerLevel.md) { get; } | `flAddedBonusScalarPerLevel` (payload `+80`) — per-level magnitude increment. Zero on Op-5 (see [`Base`](./ParagonGlyphAffixDefinition/Base.md)). |
| [SnoId](ParagonGlyphAffixDefinition/SnoId.md) { get; } | The affix's own SNO id. |
| [Tags](ParagonGlyphAffixDefinition/Tags.md) { get; } | FR-C24 (CL-84) — Raw GBID list from the `+120/+124``DT_VARIABLEARRAY[DT_UINT]` descriptor. The list contains the affix's classification anchors (an always-present ParagonGlyphAffix-root GBID `0xD4A1BC54` on every Op-2 record; a class-attribute anchor; the per-skill-tag selector — Abyss / Archfiend / Demonology / Hellfire / Occult / etc.). The skill-tag selector is the non-anchor entry; cracked names land in `docs/d4-hash-dictionary.md` as they're recovered. Consumers can call [`FormatFieldHash`](./Diablo4/FormatFieldHash.md) on each entry to render the raw `0xNNNNNNNN` when uncracked. |

## Remarks

Byte layout per the canonical reference (`docs/casc-diablo4-format.md §7.4`, formatHash `0xB460195F`; see Appendix A CL-84 for the slice-2b RE):

* payload base `0x10`; `snoId@0`.
* `eAffectedNodeRarity@24` ([`AffectedRarity`](./ParagonGlyphAffixDefinition/AffectedRarity.md)): universally `0` across all 314 live affixes (the "any rarity" sentinel — see [`AffectedRarityKind`](./ParagonGlyphAffixDefinition/AffectedRarityKind.md) for the typed view).
* `eBonusOperation@48` ([`Operation`](./ParagonGlyphAffixDefinition/Operation.md) / typed [`OperationKind`](./ParagonGlyphAffixDefinition/OperationKind.md)): `1`=Attribute, `2`=NodeAmplification, `4`=AttributeConversion, `5`=Power.
* `flStartingBonusScalar@76` ([`Base`](./ParagonGlyphAffixDefinition/Base.md)), `flAddedBonusScalarPerLevel@80` ([`PerLevel`](./ParagonGlyphAffixDefinition/PerLevel.md)): the per-level magnitude (zero on Op-5, whose magnitude lives in the [`LinkedPowerSnoId`](./ParagonGlyphAffixDefinition/LinkedPowerSnoId.md) Power record).
* `flDisplayFactor@84` ([`DisplayFactor`](./ParagonGlyphAffixDefinition/DisplayFactor.md)): per-op engine constant (Op-1=100, Op-2=500, Op-4=100, Op-5=1) — surfaced verbatim; the consumer's display formula determines how to apply it.
* `snoPower@88` ([`LinkedPowerSnoId`](./ParagonGlyphAffixDefinition/LinkedPowerSnoId.md)): group-29 PowerDefinition ref on Op-5 affixes; the sentinel `-1` on every other op.
* The [`AffectedAttributes`](./ParagonGlyphAffixDefinition/AffectedAttributes.md)`DT_VARIABLEARRAY` descriptor lives at an op-dependent payload offset: `+16/+20` for Op-1, `+64/+68` for Op-2, `+104/+108` for Op-4 (Op-5 has no per-attribute scaling). Element stride is `8` bytes — a packed `(int AttributeId, uint ParamPlus12)` pair, mirrored in [`GlyphAffixAttributeRef`](./GlyphAffixAttributeRef.md).
* The [`Tags`](./ParagonGlyphAffixDefinition/Tags.md)`DT_VARIABLEARRAY[DT_UINT]` descriptor lives at `+120/+124`; element stride is `4` bytes (raw GBIDs).

## See Also

* namespace [WiseOwl.Casc.Diablo4](../WiseOwl.Casc.Diablo4.md)
* [ParagonGlyphAffixDefinition.cs](https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/ParagonGlyphAffixDefinition.cs)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
