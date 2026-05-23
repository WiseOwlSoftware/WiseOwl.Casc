# 0064 — FR-C21 build (2/N): `ParagonNodeInfo` projection + `Catalog.GetNodeInfo` + cache

2026-05-23 · CL-69 · branch `fr-c21-node-info-projection`

## Trigger

CL-68 shipped the math (budget multipliers + magnitude evaluator).
This CL ships the **public surface the Optimizer renders from**: a
display-ready projection of one node, served by
`Catalog.GetNodeInfo(int sno)`.

## What ships

```
public sealed record ParagonNodeInfo(
    int Sno, string Name,
    ParagonNodeKind Kind,                 // Normal/Magic/Rare/Legendary/Start/Socket/Gate
    ParagonRarity   Rarity,               // raw eRarityOverride
    AssetRef? Icon, AssetRef? IconMask,   // TextureAtlas refs
    AssetRef? PassivePower,
    string?   PassivePowerName,           // sibling-StringList localized
    IReadOnlyList<ParagonNodeStat> Stats, // empty for Start/Socket/Gate
    bool HasSocket, bool IsGate);

public sealed record ParagonNodeStat(
    int    AttributeId, string  StatName,
    int    Variant,     string? VariantName,
    double? FlatValue,  StatUnit Unit,
    AssetRef? Formula,  string? InlineFormula);

public enum ParagonNodeKind { Normal, Magic, Rare, Legendary, Start, Socket, Gate }
public enum StatUnit        { Flat, Percent, Multiplier }

// On d4.Catalog:
public ParagonNodeInfo? GetNodeInfo(int sno);    // cached + memoized
```

## Design decisions

**`Sno` as the canonical stat key, not `(AttributeId, Variant)`.** The
Optimizer's original proposal aggregated on the (id, variant) tuple;
CASC's correction (sat on #33 awaiting their sign-off through CL-67)
showed three nodes — `Generic_Magic_Armor`,
`Generic_Magic_ArmorPercent`,
`Generic_Magic_DamageReductionFromElite` — that decode to **identical**
attribute fields (`AttributeId 481`, `NParam 0`, same formula GBID,
same parallel-array GBID) but display three distinct stats. The
Optimizer signed off 2026-05-23: *"Sno as the canonical aggregation
key, accepted. The evidence is conclusive: three nodes with identical
AttributeId/NParam/formula but distinct in-game stats can only be
disambiguated by Sno."* `AttributeId` and `NParam` stay on the
projection as raw/informational, just not the dedup key.

**`StatName` resolution from the node-name convention.** For
`Generic_<Rarity>_<Token>` nodes, extract the trailing token and
humanize via CamelCase split + small abbreviation table:

| Node                                        | Resolved `StatName`                |
|---                                          |---                                 |
| `Generic_Magic_Armor`                       | `Armor`                            |
| `Generic_Magic_DamageToElite`               | `Damage to Elite`                  |
| `Generic_Magic_ResistanceCold`              | `Resistance Cold`                  |
| `Generic_Magic_DamageReductionFromVulnerable` | `Damage Reduction from Vulnerable` |
| `Generic_Magic_DamageReductionWhileHealthy` | `Damage Reduction while Healthy`   |
| `Generic_Magic_Str`                         | `Strength`                         |
| `Generic_Magic_HPFlat`                      | `Max Life (Flat)`                  |
| `Generic_Magic_HPPercent`                   | `Max Life`                         |
| `Generic_Magic_CDR`                         | `Cooldown Reduction`               |
| `Generic_Magic_AttackSpeedBasic`            | `Attack Speed (Basic Skills)`      |

Class-specific names like `Warlock_Rare_006` carry no encoded stat
token — fall back to `"Attribute <id>"`. Localized labels via
`AttributeDescriptions` (sno `4080`) are deferred to a follow-on if
the Optimizer asks.

**`StatUnit` heuristic.** Token-driven dispatch with a precedence
fix mid-implementation: `ResistanceMax*` (the cap, `+%` display)
must short-circuit before the generic `Resistance*` (`+ flat
points`) check. Pure-stat tokens (`Str`/`Int`/`Will`/`Dex`),
`HPFlat`, `Thorns`, `Essence`, `MaximumWrath`, `MaximumDominance`,
`HealingBonus`, `BonusFortify` are Flat; bare-constant formulas
(Normal-rarity nodes whose text has no identifier) are Flat;
everything else is Percent. When the node name carries no
`Generic_*` token, dispatch falls back to a handful of player-stat
/ resistance / HP-flat / resource-max / Thorns `AttributeId`s; the
default is Percent.

The unit is a **hint** — `FlatValue` is the numeric truth.

**`ParagonNodeKind` is a distinct axis from `Rarity`.** The visual
archetype (`Normal | Magic | Rare | Legendary | Start | Socket |
Gate`) is what tooltips key on; the raw `eRarityOverride` stays
exposed for completeness. Precedence: `IsStart` ⇒ `Start`,
`IsGate` ⇒ `Gate`, `HasSocket` ⇒ `Socket`, otherwise by
`Rarity`. `Stats` is empty for Start / Socket / Gate (consistent
with the Optimizer's clarifying note).

**Caching on `Catalog`.** Two `ConcurrentDictionary<int, …>`
caches — one for decoded `ParagonNodeDefinition`, one for resolved
`ParagonNodeInfo`. The shared `AttributeFormulaTable` (sno
`201912`, ~1 MB) is read once on first call and held under a
double-check lock. The optimizer hot path will re-query the same
boards repeatedly; each board carries ~17–21 distinct node defs
across ~441 cells. Missing/undecodable SNOs memoize as `null` so a
malformed repeat-query is just as cheap as a hit. Reference
equality on repeat lookups is asserted by the live tests.

## Library boundary

Still inside the FR-C21 carve-out from CL-68 — magnitude evaluation
+ unit / name inference + decode caching are now in-scope for the
node-info surface. The narrower "no formula evaluator" boundary
still holds elsewhere (power-script output, glyph rank/radius,
item/affix, general `AttributeFormulaTable` evaluation).

The builder (`ParagonNodeInfoBuilder`) is **internal** to the
assembly — consumers reach the projection through
`Catalog.GetNodeInfo`. The two resolvers (`ResolveStatName`,
`InferUnit`, `ExtractStatToken`) are also internal but exposed to
the test project via `InternalsVisibleTo` so the StatName /
StatUnit dispatch tables can be unit-tested directly without
spinning up a `Diablo4Storage` for each row.

## Tests

23 new (92/92 green on `3.0.2.71886`):

- `B9_stat_name_resolves_from_node_name_token` — 13 Theory cases
  covering the CamelCase split + every abbreviation in the
  small-table.
- `B9_stat_name_falls_back_to_attribute_id_for_non_generic_names` —
  `Warlock_Rare_006` ⇒ `"Attribute 259"`.
- `B9_stat_unit_inferred_from_token_and_attribute_id` — 9 Theory
  cases (the `ResistanceMax*` precedence carve-out included).
- **Live matrix:** `GetNodeInfo(671247)` for `Generic_Magic_Armor` —
  Kind `Magic`, StatName `"Armor"`, Unit `Percent`, FlatValue
  `7.5`. `GetNodeInfo(2451111)` for `Warlock_Rare_006` — Kind
  `Rare`, 2 stats with `Attribute <id>` fallback names.
  `GetNodeInfo(681756)` for `Generic_Socket` — Kind `Socket`,
  `Stats` empty. Cache-identity check (repeat lookup returns the
  same reference). Missing-SNO ⇒ `null` memoization.

## What's next

- **CL-70**: `Catalog.GetBoardNodes(int boardSno)` →
  `IReadOnlyList<(ParagonGridCell, ParagonNodeInfo)>` (the consumer
  hot path) + `EnumerateNodes(AssetQuery?)` for global queries.
- **CL-71 (stretch)**: localized labels via `AttributeDescriptions`
  (sno `4080`) if the Optimizer signals need after consuming.
