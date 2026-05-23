# ParagonNodeDefinition class

A decoded Diablo IV `ParagonNodeDefinition` (`.pgn`, SNO group 106). Raw fields only — no rarity scaling, no formula evaluation, no scoring (that interpretation is permanently the consumer's).

```csharp
public sealed class ParagonNodeDefinition
```

## Public Members

| name | description |
| --- | --- |
| static [Parse](ParagonNodeDefinition/Parse.md)(…) | Decode a ParagonNode from its raw SNO blob. |
| [Attributes](ParagonNodeDefinition/Attributes.md) { get; } | The node's attribute grants (raw [`NodeAttribute`](./NodeAttribute.md) specifiers). |
| [BonusPassivePowerSno](ParagonNodeDefinition/BonusPassivePowerSno.md) { get; } | The single SNO slot decoded from the `DT_VARIABLEARRAY[DT_SNO]` descriptor at payload `+48`. Rare nodes carry the descriptor with one slot; all other observed kinds (Common/Magic/Start/Gate/Socket) leave the descriptor empty. Across every rare node sampled so far the slot itself holds `0` (no bonus passive power authored), so the population case is unobserved — the value is surfaced raw rather than left undecoded: `0` means "rare-shape descriptor with no power", and `-1` means "no descriptor / not a rare node". The canonical engine field name has not yet been recovered. |
| [BonusStatTagSnoIds](ParagonNodeDefinition/BonusStatTagSnoIds.md) { get; } | The bonus stat-threshold tag SNO ids from the `DT_VARIABLEARRAY[DT_SNO]` descriptor at payload `+64` (group StatTag=124). Populated only on Rare nodes — every other observed node kind (Common/Magic/Start/Gate/Socket) returns an empty list. Each tag references a [`StatTagDefinition`](./StatTagDefinition.md) whose formula text evaluates to the stat threshold the player must meet for the node's "bonus when threshold met" effect to activate. Class-generic rares list 2–3 tags (alternative stats keyed to the player's class — e.g. `[Barb_Strength+Dexterity, DexteritySide2, StrengthSide2]`); class-specific rares list one (`Warlock_Rare_006` → `WillpowerMain2`). The canonical engine field name has not yet been recovered. |
| [HasSocket](ParagonNodeDefinition/HasSocket.md) { get; } | `bHasSocket` — a glyph-socket node. |
| [HIcon](ParagonNodeDefinition/HIcon.md) { get; } | `hIcon` (DT_UINT, `+8`). Not a SNO id; the first-party icon link — equals a [`ImageHandle`](./TexFrame/ImageHandle.md) (usually 0 here; the symbol handle is normally [`HIconMask`](./ParagonNodeDefinition/HIconMask.md)). |
| [HIconMask](ParagonNodeDefinition/HIconMask.md) { get; } | `hIconMask` (DT_UINT, `+12`). The symbol icon handle; equals a [`ImageHandle`](./TexFrame/ImageHandle.md) in a paragon atlas (resolve via [`TryGetIconFrame`](./Diablo4Storage/TryGetIconFrame.md)). |
| [IsGate](ParagonNodeDefinition/IsGate.md) { get; } | `bIsGate` — a board-attachment gate node. |
| [IsStart](ParagonNodeDefinition/IsStart.md) { get; } | True when this is a board start node ([`NodeTypeRaw`](./ParagonNodeDefinition/NodeTypeRaw.md) == 5) — verified on all seven class start boards. |
| [NodeType](ParagonNodeDefinition/NodeType.md) { get; } | [`NodeTypeRaw`](./ParagonNodeDefinition/NodeTypeRaw.md) as the verified enum (convenience; the raw int remains authoritative). A distinct axis from [`Rarity`](./ParagonNodeDefinition/Rarity.md). |
| [NodeTypeRaw](ParagonNodeDefinition/NodeTypeRaw.md) { get; } | Raw `eNodeType` (payload `+16`): 0=Normal/ structural/gate/rare, 3=Magic, 5=Start. This exact int is the serialized contract — kept raw deliberately; see [`NodeType`](./ParagonNodeDefinition/NodeType.md) for the named enum. |
| [Rarity](ParagonNodeDefinition/Rarity.md) { get; } | [`RarityOverride`](./ParagonNodeDefinition/RarityOverride.md) as the verified enum (convenience; the raw int remains authoritative). |
| [RarityOverride](ParagonNodeDefinition/RarityOverride.md) { get; } | Raw `eRarityOverride` (0=Common/structural, 2=Magic, 3=Rare, 4=Legendary). This exact int is the serialized contract — kept raw deliberately. |
| [SnoId](ParagonNodeDefinition/SnoId.md) { get; } | The node's own SNO id. |
| [SnoPassivePower](ParagonNodeDefinition/SnoPassivePower.md) { get; } | `snoPassivePower` (DT_SNO, group 29 Power, `+24`) — the node's granted passive power SNO id (0 / `0xFFFFFFFF` when none). Exposed raw for future node→power character modeling. |

## Remarks

Byte layout per the canonical reference (`docs/casc-diablo4-format.md §7.2`, migrated/verified from the upstream `d4-binary-formats.md §5`): payload base `0x10`; `snoId@0`; `hIcon@8` (DT_UINT); `hIconMask@12` (DT_UINT); `eNodeType@16` (0/3/5; see [`ParagonNodeType`](./ParagonNodeType.md)); `eRarityOverride@20` (0/2/3/4); `snoPassivePower@24` (DT_SNO, group 29 Power); `ptAttributes``DT_VARIABLEARRAY[AttributeSpecifier]` descriptor `@32` (`dataOffset` payload-relative `@+8`, `dataSize@+12`; element stride 88); a `DT_VARIABLEARRAY[DT_SNO]` descriptor `@48` — the bonus-passive-power slot (size-1 on rares; empty otherwise; see [`BonusPassivePowerSno`](./ParagonNodeDefinition/BonusPassivePowerSno.md)); a `DT_VARIABLEARRAY[DT_SNO]` descriptor `@64` — the bonus stat-threshold tag array, populated only on rare nodes (see [`BonusStatTagSnoIds`](./ParagonNodeDefinition/BonusStatTagSnoIds.md)); `bHasSocket@80`; `bIsGate@84`; a `DT_VARIABLEARRAY[DT_UINT]` descriptor `@88` — one per-attribute GBID, parallel to `ptAttributes` (see [`AttributeGbid`](./NodeAttribute/AttributeGbid.md)).

## See Also

* namespace [WiseOwl.Casc.Diablo4](../WiseOwl.Casc.Diablo4.md)
* [ParagonNodeDefinition.cs](https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/ParagonNodeDefinition.cs)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
