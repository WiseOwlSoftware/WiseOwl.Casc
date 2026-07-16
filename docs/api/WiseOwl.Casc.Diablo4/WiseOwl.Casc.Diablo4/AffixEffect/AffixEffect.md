# AffixEffect constructor

A single stat effect of an item/aspect [`AffixDefinition`](../AffixDefinition.md) — the `(AttributeId, ParamPlus12)` pair identifying which attribute the affix modifies, plus the resolved localized [`AttributeName`](./AttributeName.md). One affix carries one [`AffixEffect`](../AffixEffect.md) per modifier (a single-stat affix has one; a dual affix — e.g. a two-element resistance — has one per element).

```csharp
public AffixEffect(int AttributeId, uint ParamPlus12, uint FormulaGbid, string AttributeName)
```

| parameter | description |
| --- | --- |
| AttributeId | The modified attribute id (slot `idx4`). Two namespaces, selected by the high bit (verified CL-92): a positive id is a runtime engine `eAttribute` — feed it to [`GetAttributeName`](../Diablo4Storage/GetAttributeName.md) for the localized display name (e.g. `482 → "Armor"`). A negative id (high bit `0x80000000` set) is a reference into the data-defined `DataAttributes` designer table (SNO `1907204`) by ordinal`AttributeId & 0x7FFFFFFF` — these are the conditional/seasonal/per-power attributes (e.g. ordinal `84 = Barb_Berserking_AttackSpeed`); see [`IsDataDefinedAttribute`](./IsDataDefinedAttribute.md). The two namespaces are disjoint — never take the absolute value (negative-208 is a different attribute from positive-208). Data-defined ids do not resolve through [`GetAttributeName`](../Diablo4Storage/GetAttributeName.md) (a different table); their [`AttributeName`](./AttributeName.md) is left empty in this slice — the raw token is available on the affix's own [`Description`](../AffixDefinition/Description.md) placeholder, and full DataAttributes name resolution is the FR-C27 registry frontier. |
| ParamPlus12 | The attribute parameter (slot `idx7`): [`NoParam`](./NoParam.md) (`0xFFFFFFFF`) when the attribute is parameter-agnostic; a small enum for parametric attributes (e.g. the element on a single-resistance modifier — cold/lightning/poison); or a skill-tag GBID on tag-conditional attributes (e.g. `AttributeId 259 = Damage per Skill Tag`). Resolve the tag-specific name via [`GetAttributeName`](../Diablo4Storage/GetAttributeName.md) and filter unset slots with [`HasParam`](./HasParam.md). |
| FormulaGbid | CL-94 — the `GBID` of this modifier's value formula (slot `idx16`, byte `+64`): the key into the `AttributeFormulas` table (SNO `201912`) that defines the affix's rolled magnitude by item power. Resolve it with [`TryGetByGbid`](../AttributeFormulaTable/TryGetByGbid.md) (from [`ReadAttributeFormulas`](../Diablo4Storage/ReadAttributeFormulas.md)) to get the per-`ItemPowerRangeStart`[`Ranges`](../AttributeFormula/Ranges.md) — each carries the `DT_STRING_FORMULA` source text the game rolls the value from (e.g. `GearAffix_CritChance → "FloatRandomRangeWithInterval(1,3,3.5)/100"` at high item power). The library exposes the raw formula text; evaluating it (and thus the min/max a UI prints) stays the consumer's, matching the paragon magnitude boundary. [`NoFormula`](./NoFormula.md) (`0`) when the modifier carries no value formula (e.g. set/unique power modifiers whose numbers are on [`StaticValues`](../AffixDefinition/StaticValues.md) instead), or an id that isn't an `AttributeFormulas` entry — filter with [`TryGetByGbid`](../AttributeFormulaTable/TryGetByGbid.md). |
| AttributeName | The resolved attribute display name — the localized engine name (via [`GetAttributeName`](../Diablo4Storage/GetAttributeName.md)) for a positive id, or the `DataAttributes` designer token (via [`TryGetDataAttributeName`](../Diablo4Storage/TryGetDataAttributeName.md), flagged by [`IsDataDefinedAttribute`](./IsDataDefinedAttribute.md)) for a negative id — or Empty when unresolved or the affix was decoded byte-only via [`Parse`](../AffixDefinition/Parse.md). |

## Remarks

Decoded from the affix's `arModifiers``DT_VARIABLEARRAY` at payload `+0xB0` (descriptor `dataOff@+0xB0` / `byteSize@+0xB4`), which is an array of fixed 104-byte modifier records (`count = byteSize / 104`). Within each record the modified attribute is at slot `idx4` (byte `+16`) and its parameter at slot `idx7` (byte `+28`); the remaining slots (the `~472..640` ids at `idx10/14/20/24` with their `2/4/12` params and the family-shared GBID at `idx16`) are the engine's magnitude-formula slots, shared across every affix of a family and therefore not stat identity — see `casc-diablo4-format.md §11.5` (Appendix A CL-92).

AttributeId space. The [`AttributeId`](./AttributeId.md) is the same runtime `eAttribute` id resolved by [`GetAttributeName`](../Diablo4Storage/GetAttributeName.md) — e.g. `275 → "Critical Strike Chance"`, `482 → "Armor"`, `142 → "Maximum Life"`. Unlike the coarser power-budget category on a [`ParagonNodeDefinition`](../ParagonNodeDefinition.md), the affix id is the specific stat (`482 = Armor%` and `1125 = Damage Reduction` are distinct affix ids even though nodes lump both under one budget category).

Magnitude / operation. The rolled magnitude (min/max value range) and the additive-vs-multiplicative operation are not literal fields of the affix record for the bulk of stat affixes — they are item-power-curve driven by the engine (the operation is implied by the attribute identity, e.g. a `Multiplicative_*` / `_Percent` attribute). Surfacing those stays with the consumer per the durable library boundary (`casc-diablo4-format.md` Appendix C); this type answers the "which attribute(s)" question (LIB-3 slice 1).

## See Also

* struct [AffixEffect](../AffixEffect.md)
* namespace [WiseOwl.Casc.Diablo4](../../WiseOwl.Casc.Diablo4.md)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
