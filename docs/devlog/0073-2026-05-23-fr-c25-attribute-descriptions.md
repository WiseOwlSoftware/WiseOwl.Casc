# 0073 — FR-C25: `GetAttributeName` from AttributeDescriptions (sno 4080)

2026-05-23 · CL-78 · branch `fr-c25-attribute-descriptions`

## Trigger

Optimizer's `casc-fr#37` — the eventual canonical path the CL-69
honesty note flagged. CL-76 patched the multi-row `Generic_Gate`
defect with a hardcoded basic-four (9 / 10 / 11 / 12); every other
attribute id still fell through to the `"Attribute <id>"` honest
sentinel. The Optimizer needed real localized names for the long
tail (`481 → "Armor"`, `950 → "Damage to Elites"`,
`133 → "Maximum Life"`, …) before they could close out the
`Assets/Data/attributes.json` from a single first-party ParagonDataGen
pass.

## Recon

AttributeDescriptions (sno 4080) is the StringList the tooltip
renderer pulls from. Sampling its entries:

```
[Strength]                       = [{VALUE}|~|] Strength
[Hitpoints_Max_Bonus]            = [{VALUE}|~|] Maximum Life
[Armor_Bonus]                    = +[{VALUE}] Armor
[Damage_Percent_Bonus_Vs_Elites] = +[{VALUE}*100|1%|] Damage to Elites
[Crit_Percent_Bonus]             = +[{VALUE}*100|1%|] Critical Strike Chance
[Resistance]                     = +[{VALUE2}] {VALUE1} Resistance
```

The labels are **semantic keys**, not AttributeId ints. So sno 4080
gives us `label → template`; the **missing piece** is
`AttributeId → label`.

I dug into `DataAttributes` (GameBalance sno 1907204, `eGameBalanceType
84`, 278 entries × 360-byte stride) — that's the AttributeId
registry. Decoded the entry-array offset (`tEntries` descriptor at
`payload+80` → `dataOffset 88`, `dataSize 100080`) and confirmed
entry layout `szName[256]@+0`, `gbid@+256`, then ~104 bytes of
auxiliary fields including descriptor references to sub-arrays.
Couldn't immediately pin the **AttributeId field** offset within an
entry (the entries are heavily skill-keyed —
`Flurry_Consume_2`, `BSK_Bonus_Int`, etc. — and don't simply
correlate to the basic 9/10/11/12 by name). The full registry RE
would converge eventually but it's deeper than this FR's scope.

## What ships — clean-room curated map

Pragmatic alternative: bake a clean-room curated
`AttributeId → AttributeDescriptions-label` map covering every
AttributeId observed in the existing first-party `attrmap` SnoScan
output (the empirical `Generic_<Rarity>_<Token>` node-name
convention). ~40 entries — covers every Optimizer FR-C25 anchor
case + every `attrmap`-observed id used on a real paragon board.

```csharp
public static class AttributeNames
{
    public static IReadOnlyDictionary<int, string> LabelByAttributeId { get; }
        = new SortedDictionary<int, string>
        {
            { 9,  "Strength" },
            { 10, "Intelligence" },
            { 11, "Willpower" },
            { 12, "Dexterity" },
            { 79, "Resistance" },
            { 133, "Hitpoints_Max_Bonus" },
            // … 30+ more
            { 481, "Armor_Bonus" },
            { 950, "Damage_Percent_Bonus_Vs_Elites" },
        };

    public static string StripTemplate(string template);    // helper
}
```

`Diablo4Storage.GetAttributeName(int, locale)` chains the pipeline:
look up label → fetch template via `GetStrings(locale).TryGet(4080,
label, …)` → `StripTemplate`. Returns `null` on any failure
(unmapped id, missing locale bundle, missing label) — honest
sentinel.

## Template-strip pipeline

```
"+[{VALUE}*100|1%|] Damage to Elites"
  → strip [...] blocks    →  "+ Damage to Elites"
  → strip {…} tags        →  "+ Damage to Elites"
  → strip orphan + / -    →  "  Damage to Elites"
  → collapse whitespace   →  " Damage to Elites"
  → TrimStart('+','-',' ') / Trim()
                          →  "Damage to Elites"
```

Orphan-sign stripping (the `+` left over after `[{VALUE}*100|1%|]` is
removed) was the one piece I almost missed — caught it on the
"Lucky Hit: …" test case where the `+` sits mid-string. The rule:
strip a `+` / `-` only when **both** edges are whitespace (or
string edges) — that flags it as a placeholder leftover rather
than a meaningful operator.

## `ParagonNodeStat.StatName` rewiring

`ParagonNodeInfoBuilder.ResolveStatName` gained a storage overload
that routes through `GetAttributeName` first. The CL-76 hardcoded
basic-four is kept as a defensive offline fallback — same answers
(`Strength`/`Intelligence`/`Willpower`/`Dexterity`) for the same
ids, so behaviour is stable, but the live path now goes through the
canonical sno-4080 templates. For unmapped ids and missing locale
bundles the chain continues with the node-name token (covers
budget-category attrs like 481 where the stat identity lives in
the name) → honest `"Attribute <id>"`.

For `Warlock_Rare_006`'s 2 attribute rows:
- attr `259` — not in the curated map (budget category for tag-
  conditional damage; per-tag stat identity in the node name) →
  still surfaces as `"Attribute 259"` until the consumer wires the
  tag-keyed lookup (a future iteration).
- attr `288` — IN the map → resolves to `"Critical Strike Damage"`.

Same path used for Gate: attrs 9/10/11/12 resolve to
`Strength`/`Intelligence`/`Willpower`/`Dexterity` via the new
sno-4080 path (matches the CL-76 hardcoded answer; no behaviour
change for that case).

## Tests

22 new test cases (104 → 126 green on `3.0.2.71886`):

- 8 Theory rows on `AttributeNames.StripTemplate` (anchor
  templates from sno 4080 + the orphan-sign cleanup).
- 13 SkippableFact Theory rows on
  `Diablo4Storage.GetAttributeName` (Optimizer's anchor ids:
  basic-four + 133/481/950/275/288/208/221/237/373).
- 1 honest-null assertion for an unmapped id.

## Caveats — what's NOT covered

- **Long-tail attribute ids** beyond the `attrmap` set return
  `null`. Adding a new id is one-line — extend the curated map.
- **AttributeId 259 (tag-conditional damage)** — multiple stats
  share the id, keyed by `ParamPlus12` (the skill-tag GBID).
  Resolving these to localized names would need a parallel
  `(AttributeId, ParamPlus12) → label` map. Out of scope here;
  Optimizer can ask if the FR-C24 glyph-affix work needs it.
- **Full `DataAttributes` (sno 1907204) decode** — locating the
  AttributeId field within an entry would replace the curated map
  with a data-driven lookup covering every shipped id. Deferred
  to a future RE thread.

## Surface summary

| Public type | Use |
|---|---|
| `AttributeNames.LabelByAttributeId` | The curated map; inspectable. |
| `AttributeNames.StripTemplate(string)` | The template-strip helper (consumer can reuse on raw templates). |
| `Diablo4Storage.GetAttributeName(int, locale)` | The localized name lookup. |
| `Diablo4Storage.AttributeDescriptionsSno = 4080` | The canonical SNO id. |
| `ParagonNodeInfoBuilder.ResolveStatName(d4, token, id)` | The wired-up resolution chain (internal). |
