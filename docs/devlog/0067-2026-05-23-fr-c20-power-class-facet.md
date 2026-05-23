# 0067 — FR-C20 #32: Power → class facet from the name convention

2026-05-23 · CL-72 · branch `fr-c20-power-class-facet`

## Trigger

The owner's 2026-05-22 directive on `casc-fr#32` named three deferred
extras; CL-71 closed codec tail. This CL closes the second:
**Power → class**.

The relevant context line from CL-59's P2b doc (the "marked-A"
shipment):

> *`Power→class` has NO cheap source (`PowerDefinition` no class;
> `PlayerClass` no power list; names don't encode it) — RE question
> put to the Optimizer on #32.*

That note was a blanket claim. A quick `SnoScan find` on the obvious
class skill names disproved it:

```
Barbarian_Bash         (200765)
Barbarian_Whirlwind    (206435)
Necromancer_BloodLance (501629)
Sorcerer_Fireball      (...)
```

The engine's first-party convention for class-skill powers is
**`<ClassSnoName>_<SkillName>`** — the same `ClassSnoName` token §6.5
already exposes as the canonical class key (`Barbarian`, `Sorcerer`,
`Druid`, `Rogue`, `Necromancer`, `Paladin`, `Warlock`, `Spiritborn`).

A whole-roster tally:

```
Barbarian_*   on Power: 128 hits
Sorcerer_*    on Power: 153 hits
Druid_*       on Power: 295 hits
Rogue_*       on Power: 262 hits
Necromancer_* on Power: 153 hits
Paladin_*     on Power: 240 hits
Warlock_*     on Power: 200 hits
Spiritborn_*  on Power: 274 hits
                       ─────
                       ~1,705 powers (of ~2,500 in group 29)
```

The remaining ~800 are the cases the original "no cheap source"
note was thinking of: monster powers (`MorluCaster_Fireball`,
`demon_flyer_fireball_attack`), item-affix powers
(`1HAxe_Unique_Druid_100`, `1HFocus_Unique_Necro_100`), unnamed
debug stubs, and other non-class-prefix names. These genuinely
have no class encoded in the name; the facet leaves them
unfaceted.

## What ships

A new `case AssetKind.Power` in `Catalog.Facets` that decodes the
name convention — **decode-free** (just CoreTOC name + an eight-way
prefix check). `FacetSource.NameConvention` flags the provenance.

```csharp
// In Catalog.Facets(asset):
case AssetKind.Power when TryGetPowerClassFromName(asset.Name) is { } cn:
    list.Add(new Facet("class", cn, FacetSource.NameConvention));
    break;
```

The dispatch is a memoised lookup against the §6.5 PlayerClass roster
(read once via `CoreToc.EntriesInGroup(PlayerClass)`, the `"Axe Bad
Data"` placeholder filtered out — the same data-driven approach §6.5
already uses, no hardcoded list).

```csharp
internal string? TryGetPowerClassFromName(string powerName)
{
    if (string.IsNullOrEmpty(powerName)) return null;
    var u = powerName.IndexOf('_');
    if (u <= 0) return null;
    var prefix = powerName.AsSpan(0, u);
    foreach (var cn in PlayerClassSnoNames())
        if (prefix.SequenceEqual(cn)) return cn;
    return null;
}
```

Why the `IndexOf('_')` test rather than just `StartsWith(cn + "_")`?
Disambiguation. `Necro` is a prefix of `Necromancer` and shows up
inside item-affix names (`1HMace_Unique_Necro_100`). Matching against
the **whole first token** prevents false positives — if the first
underscore-bounded token equals `Necro` exactly, that's not a
PlayerClass SnoName, so the dispatch correctly returns `null`. The
roster `Necromancer` entry only matches when the first token is
exactly `Necromancer`.

The Optimizer can now query:

```csharp
foreach (var p in catalog.FindByFacet(AssetKind.Power, "class", "Sorcerer"))
    // every active Sorcerer skill power
```

## Honesty

- **Partial coverage by design.** Powers whose names don't match the
  convention stay unfaceted. This is the right answer — the
  convention is what the engine authors, and forcing a class onto a
  monster power would be a fabrication.
- **Class-restricted item-affix powers** (`1HAxe_Unique_Druid_100`)
  are not class skills — the item's class restriction is enforced at
  the item level, not the power. The facet correctly leaves these
  unfaceted (they may still be relevant to a class via the item; that
  edge is for a different surface).
- **No `PowerDefinition` decode.** The facet derives entirely from
  the CoreTOC name + the cached PlayerClass SnoName roster — so
  enumerating every class's powers is essentially free
  (~2,500 string-prefix checks, eight comparisons each, no I/O).

## Tests

92/92 still green on `3.0.2.71886` (live data — the four well-known
class-skill powers resolve correctly; `MorluCaster_Fireball` /
`1HAxe_Unique_Druid_100` / empty / unparseable resolve to `null`;
`FindByFacet(Power, class, Sorcerer)` round-trips with every result
starting `"Sorcerer_"`).

## What's left on #32

- **Item `NameConvention` facets** — the third deferred extra. Items
  are named `<Type>_<Rarity>_<Class>_<NN>` (and friends), and the
  Optimizer asked for type/rarity/class surfaced as facets on
  `Find(Item)` results. Next CL candidate.
