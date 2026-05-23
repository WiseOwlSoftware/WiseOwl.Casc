# ParagonNodeInfo record

FR-C21 — the display-ready projection of one paragon node. The library evaluates magnitudes + infers units + resolves names so the consumer can render the in-game tooltip without re-walking the raw byte fields or owning the formula evaluator (per the Appendix C carve-out for this surface).

```csharp
public record ParagonNodeInfo
```

## Public Members

| name | description |
| --- | --- |
| [ParagonNodeInfo](ParagonNodeInfo/ParagonNodeInfo.md)(…) | FR-C21 — the display-ready projection of one paragon node. The library evaluates magnitudes + infers units + resolves names so the consumer can render the in-game tooltip without re-walking the raw byte fields or owning the formula evaluator (per the Appendix C carve-out for this surface). |
| [HasSocket](ParagonNodeInfo/HasSocket.md) { get; set; } | Raw [`HasSocket`](./ParagonNodeDefinition/HasSocket.md) for back-compat; equivalent to [`Kind`](./ParagonNodeInfo/Kind.md)=Socket. |
| [Icon](ParagonNodeInfo/Icon.md) { get; set; } | The atlas (TextureAtlas) containing the [`HIcon`](./ParagonNodeDefinition/HIcon.md) frame, when the node authors one (most nodes leave `HIcon = 0` and rely on [`IconMask`](./ParagonNodeInfo/IconMask.md)). Resolve the per-frame UVs via [`TryResolveFrame`](./Catalog/TryResolveFrame.md) against the raw handle. |
| [IconMask](ParagonNodeInfo/IconMask.md) { get; set; } | The atlas containing the [`HIconMask`](./ParagonNodeDefinition/HIconMask.md) frame — the symbol icon shown on the node's disc. |
| [IsGate](ParagonNodeInfo/IsGate.md) { get; set; } | Raw [`IsGate`](./ParagonNodeDefinition/IsGate.md) for back-compat; equivalent to [`Kind`](./ParagonNodeInfo/Kind.md)=Gate. |
| [Kind](ParagonNodeInfo/Kind.md) { get; set; } | The visual archetype — see [`ParagonNodeKind`](./ParagonNodeKind.md). |
| [LocalizedTitle](ParagonNodeInfo/LocalizedTitle.md) { get; set; } | The engine's user-facing tooltip header (FR-C22, CL-75) — `"Paragon Starting Node"` for every class start node, `"Board Attachment Gate"` for every Gate, authored display names on class-specific rare nodes (`Warlock_Rare_006` → `"Binding"`), and authored titles for named legendary nodes. Resolved via the §6.7 sibling- StringList convention (`ParagonNode_<SnoName>`, label `Name`). The generic `Generic_<Rarity>_<Token>` stat-node family (`Generic_Magic_DamageToElite` etc.) has no sibling StringList and surfaces as Empty here — the consumer composes their UI label from [`Stats`](./ParagonNodeInfo/Stats.md) / [`Kind`](./ParagonNodeInfo/Kind.md) / [`Rarity`](./ParagonNodeInfo/Rarity.md). Always non-null; Empty means "no engine-authored title for this node". |
| [Name](ParagonNodeInfo/Name.md) { get; set; } | The node's CoreTOC name (e.g. `Generic_Magic_Armor`, `Warlock_Rare_006`). The most patch-durable identity across builds. |
| [PassivePower](ParagonNodeInfo/PassivePower.md) { get; set; } | The Power SNO the node grants (when [`SnoPassivePower`](./ParagonNodeDefinition/SnoPassivePower.md) is set), pre-resolved as an asset reference; `null` when the node grants no passive power. |
| [PassivePowerName](ParagonNodeInfo/PassivePowerName.md) { get; set; } | The localized name of *PassivePower* from the sibling `Power_<Name>` StringList (`§6.7`); `null` when there is no passive power, or when the sibling string list is missing. |
| [Rarity](ParagonNodeInfo/Rarity.md) { get; set; } | The raw [`ParagonRarity`](./ParagonRarity.md) (`eRarityOverride`). Distinct from [`Kind`](./ParagonNodeInfo/Kind.md): a rare node has [`Kind`](./ParagonNodeInfo/Kind.md)=Rare AND [`Rarity`](./ParagonNodeInfo/Rarity.md)=Rare; a Start node has [`Kind`](./ParagonNodeInfo/Kind.md)=Start but [`Rarity`](./ParagonNodeInfo/Rarity.md)=Common. |
| [Sno](ParagonNodeInfo/Sno.md) { get; set; } | The node's SNO id (group ParagonNode) — the canonical stat-identity key. |
| [Stats](ParagonNodeInfo/Stats.md) { get; set; } | The node's stat grants — display-ready magnitudes, units, and names (see [`ParagonNodeStat`](./ParagonNodeStat.md)). Empty for Start and Socket (the engine authors zero attribute rows for both — Start is the class emblem; Socket's grant comes from the seated glyph). Gate — the engine's "Board Attachment Gate" — does carry stats (each Gate sampled grants `+5` to each basic stat [`AttributeId`](./NodeAttribute/AttributeId.md)`9`/`10`/`11`/`12` Strength/Intelligence/Willpower/Dexterity); the [`IsGate`](./ParagonNodeInfo/IsGate.md) flag still carries the structural meaning. |

## Remarks

Aggregation key. The canonical key for a stat (e.g. "+7.5% Total Armor") is [`Sno`](./ParagonNodeInfo/Sno.md), not `(AttributeId, NParam)`: three nodes (`Generic_Magic_Armor`, `Generic_Magic_ArmorPercent`, `Generic_Magic_DamageReductionFromElite`) decode to identical attribute fields (`AttributeId 481`, `NParam 0`, same formula GBID, same parallel-array GBID) but display three distinct stats. The Optimizer signed off on this correction (FR-C21 / `casc-fr#33`, 2026-05-23).

Boundary. The library returns ready-to-display values for this surface only (FR-C21 carve-out from Appendix C). Other formula domains (power-script output, glyph rank/radius scaling, item/affix value resolution) remain the consumer's.

## See Also

* namespace [WiseOwl.Casc.Diablo4](../WiseOwl.Casc.Diablo4.md)
* [ParagonNodeInfo.cs](https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/ParagonNodeInfo.cs)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
