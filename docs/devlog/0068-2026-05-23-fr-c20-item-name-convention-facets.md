# 0068 — FR-C20 #32: item type/rarity/class facets from the name convention

2026-05-23 · CL-73 · branch `fr-c20-item-name-convention-facets`

## Trigger

Third (and last) of the FR-C20 #32 deferred extras. The Optimizer's
note: *"Item `NameConvention` facets — the localized-name composition
data missing from current `Find(Item)` results."*

## Recon

A token-frequency tally over every group-73 item name surfaced three
distinct patterns, not one:

| Pattern             | Example                          | Count band |
|---                  |---                               |---         |
| Weapon / armor      | `1HAxe_Unique_Druid_100`         | hundreds   |
| Cosmetic            | `Cosmetic_Barbarian_FooBar`      | ~3,000 (dominant) |
| Classless / generic | `1HAxe_Magic_Generic_001`        | hundreds   |
| Quest / seasonal    | `QST_Frac_Underworld_04`         | tens       |
| Charm / template / recipe | `Charm_Set_*` / `Template_Item_*` / `MSWK_Recipe_*` | hundreds |

So a strict `<Type>_<Rarity>_<Class>_<NN>` parser would miss the
biggest category (cosmetics, which dominate the corpus by ~5×).

A second wrinkle: items use **two class-token forms** for the same
class — abbreviated (`Barb`, `Sorc`, `Necro`) on weapons + armor,
full (`Barbarian`, `Sorcerer`, `Necromancer`) on cosmetics. From the
tally:

```
Cosmetic|Necro       410
Cosmetic|Necromancer  55
```

Both authored, same class. The facet must normalize so a consumer
asking for `class=Necromancer` finds both.

## Decode

The dispatch picks among three patterns then falls back to
type-only. The static helper is unit-testable directly:

```csharp
internal static (string? Type, string? Rarity, string? Class)
    ParseItemConvention(string itemName)
{
    if (string.IsNullOrEmpty(itemName)) return default;
    var tokens = itemName.Split('_');
    var type = tokens[0];

    // Pattern A: <Type>_<Rarity>_<Class>_…
    if (tokens.Length >= 3 && IsKnownItemRarity(tokens[1]))
        return (type, tokens[1], MapItemClassToken(tokens[2]));

    // Pattern B: <Type>_<Class>_… (Cosmetic_Barbarian_…)
    if (tokens.Length >= 2 && MapItemClassToken(tokens[1]) is { } c)
        return (type, null, c);

    // Fallback
    return (type, null, null);
}

private static bool IsKnownItemRarity(string t) =>
    t is "Normal" or "Magic" or "Rare" or "Legendary" or "Unique" or "Any";

private static string? MapItemClassToken(string t) => t switch
{
    "Barb" or "Barbarian"     => "Barbarian",
    "Sorc" or "Sorcerer"      => "Sorcerer",
    "Necro" or "Necromancer"  => "Necromancer",
    "Druid" or "Rogue" or "Paladin" or "Warlock" or "Spiritborn" => t,
    _ => null,    // "Generic" is the "no class" sentinel — no facet
};
```

Why gate rarity behind a closed set rather than accepting anything in
slot 2? `Cosmetic`, `Charm`, `Journey`, `Template`, `MSWK` all live in
slot 2 of their respective patterns — they're **types**, not
rarities, and surfacing them as rarity would mislead the consumer.
Keeping the rarity set tight to the canonical six (Normal / Magic /
Rare / Legendary / Unique / Any) makes the rarity facet exclusively
meaningful for weapon + armor + classless patterns.

Why is `Generic` not a class? It's the engine's "no class
restriction" sentinel. A consumer asking for *"all classless
items"* should filter by absence of class facet, not by
`class=Generic` — which would otherwise misleadingly look like the
class is "Generic". The dispatch returns `null` for it on purpose.

## What ships

The `case AssetKind.Item` in `Catalog.Facets` emits up to three
facets per item (`type`, `rarity`, `class`) with
`FacetSource.NameConvention`. The dispatch is **decode-free** —
no `ItemDefinition` parse needed, just the CoreTOC name + an
inline pattern match.

```csharp
foreach (var f in catalog.Facets(itemRef))
    Console.WriteLine($"  {f.Key}={f.Value} ({f.Source})");
// Prints, for 1HAxe_Unique_Druid_100:
//   type=1HAxe  (NameConvention)
//   rarity=Unique  (NameConvention)
//   class=Druid  (NameConvention)
```

And consumers query:

```csharp
catalog.FindByFacet(AssetKind.Item, "class", "Necromancer")
       // Finds both Cosmetic_Necro_* AND Cosmetic_Necromancer_*
catalog.FindByFacet(AssetKind.Item, "rarity", "Unique")
catalog.FindByFacet(AssetKind.Item, "type", "1HAxe")
```

## Tests

92/92 green on `3.0.2.71886`. The CL-73 assertions extend the live
matrix:

- 9 known item-name cases verify the three patterns + the
  `Generic` sentinel + the alias map + fallbacks (empty,
  no-underscore, unrecognized leading token).
- `FindByFacet(Item, "class", "Druid")` round-trip — every result
  name contains `_Druid` (sanity check that the inverse holds).

## FR-C20 #32 backlog: complete

All three owner-directed deferred extras delivered:

| CL | Extra | Status |
|---|---|---|
| CL-71 | Codec tail — `TexFrame` inner UV decode | ✓ merged (PR #60) |
| CL-72 | Power → class facet | ✓ merged (PR #61) |
| **CL-73** | **Item NameConvention facets** | **this PR** |

`casc-fr#32` ready for consumer verification after this lands.

## What's next

Both queued FR threads (`#33` FR-C21 + `#32` FR-C20) are now
`awaiting:optimizer`. Next inbound queue is whatever the Optimizer
verifies / requests next.
