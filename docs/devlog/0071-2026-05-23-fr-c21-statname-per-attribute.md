# 0071 — FR-C21: `StatName` prefers AttributeId over node-token (Gate multi-row fix)

2026-05-23 · CL-76 · branch `fr-c21-statname-per-attribute`

## Trigger

Optimizer CL-74 consume-verify on `casc-fr#33` (running consumer SHA
`BrentRector/Paragon@97953c4` against `WiseOwl.Casc@0aa6f39`):

> *Stats[] decode correct, StatName humanization defect.*
>
> `cat.GetNodeInfo(994337).Stats` returned 4 entries with the right
> AttributeIds + magnitudes (the CL-74 Gate-stats fix took), but
> **`StatName == "Gate"` on all four rows** — the node-name token,
> not the per-attribute name.

CL-75's delivery comment claimed `StatName = "Strength"`/`"Intelligence"`
/`"Willpower"`/`"Dexterity"` on those rows — what the library actually
returned at `0aa6f39` was `"Gate"` × 4. That was a wishful copy from
my own test expectations rather than running the projection myself.
The Optimizer's probe caught the mismatch.

## Root cause

`ParagonNodeInfoBuilder.BuildStats` extracts a single token from the
node name **once**, then feeds the same token to every row's
`ResolveStatName`. For single-attribute Generic_*  nodes the token
IS the stat identity (`Generic_Magic_Armor` → token "Armor" → name
"Armor"); for multi-attribute nodes like Gate
(`Generic_Gate` → token "Gate" → 4 rows × name "Gate"), the per-row
identity can't come from the shared token — it has to come from the
per-row `AttributeId`.

## Fix

`ResolveStatName(token, attributeId)` is reorganised to prefer the
**canonical AttributeId map** when the id has a stable stat identity:

```csharp
internal static string ResolveStatName(string? token, int attributeId)
{
    // (1) Canonical AttributeId → name. Wins when set — the id is
    // a stable stat identity for these attrs.
    if (TryCanonicalNameByAttributeId(attributeId) is { } byId) return byId;

    // (2) Node-name token (covers the budget-category attrs where
    // the node name disambiguates the stat — e.g. AttributeId 481
    // is shared by Armor / ArmorPercent / DamageReductionFromElite).
    if (token is not null) return HumanizeStatToken(token);

    // (3) Honest fallback for class-specific names whose stat
    // identity isn't yet resolvable from either source.
    return $"Attribute {attributeId}";
}

private static string? TryCanonicalNameByAttributeId(int attributeId) =>
    attributeId switch
    {
        9  => "Strength",
        10 => "Intelligence",
        11 => "Willpower",
        12 => "Dexterity",
        _  => null,
    };
```

## Why this scope

The Optimizer's note explicitly accepts the basic-four as sufficient
for the immediate fix:

> *For the four basic stats this is trivial: 9 → "Strength",
> 10 → "Intelligence", 11 → "Willpower", 12 → "Dexterity" (already
> proven working for `Generic_Normal_{Str,Int,Will,Dex}`, so the
> AttributeId table exists somewhere in the projection — just needs
> to be consulted for multi-stat-row nodes too).*

The broader `AttributeDescriptions` (sno 4080) integration is the
eventual canonical path for every attribute id — it's deferred per
the FR-C22 / FR-C23 thread separation. Today's map covers Gate
(the only multi-stat node kind in current content) plus
Generic_Normal_{Str,Int,Will,Dex} (no behaviour change — the
canonical map yields the same name the token humanizer used to
produce).

## What stayed unchanged

Every previously-green case stays green:

| Node                          | Token   | AttributeId map | Before  | After   |
|---                            |---      |---              |---      |---      |
| `Generic_Normal_Str`          | `Str`   | `Strength`      | "Strength" | "Strength" ✓ |
| `Generic_Magic_Armor`         | `Armor` | (none)          | "Armor"    | "Armor" ✓    |
| `Generic_Magic_DamageToElite` | `DamageToElite` | (none) | "Damage to Elite" | "Damage to Elite" ✓ |
| `Generic_Magic_ResistanceCold`| `ResistanceCold` | (none) | "Resistance Cold" | "Resistance Cold" ✓ |
| `Generic_Magic_HPFlat`        | `HPFlat`| (none)          | "Max Life (Flat)" | "Max Life (Flat)" ✓ |

For the fixed case:

| Node          | Token  | AttributeId map | Before  | After   |
|---            |---     |---              |---      |---      |
| `Generic_Gate` row 0 (attr 9)  | `Gate` | `Strength`     | **"Gate"** | "Strength" ✓ |
| `Generic_Gate` row 1 (attr 10) | `Gate` | `Intelligence` | **"Gate"** | "Intelligence" ✓ |
| `Generic_Gate` row 2 (attr 11) | `Gate` | `Willpower`    | **"Gate"** | "Willpower" ✓ |
| `Generic_Gate` row 3 (attr 12) | `Gate` | `Dexterity`    | **"Gate"** | "Dexterity" ✓ |

## Tests

Twelve new Theory cases in `B9_stat_name_prefers_attribute_id_over_node_token`
cover:

- Basic-four × 2 token shapes (the Gate "Gate" shared token AND the
  Generic_Normal "Str/Int/Will/Dex" tokens) — both paths converge to
  the canonical name.
- Budget-category 481 (Armor + DamageReductionFromVulnerable) —
  token wins; previous behaviour preserved.
- Class-specific (null token) for AttributeIds 259 + 288 — honest
  "Attribute &lt;id&gt;" fallback (deferred to a future
  `AttributeDescriptions`-driven CL).

The live `Acceptance_matrix_against_live_install`'s Gate assertion
is tightened: it now asserts the per-row `StatName`, not just the
AttributeId set.

104/104 tests green on `3.0.2.71886` (was 92/92 — the 12 new Theory
cases).

## Lesson memorialized

This is the second feedback-validation defect in the FR-C21 thread
(CL-74 was the first, dropping Stats on Gate). Both came from
**building to a confirm rather than to a game-oracle**:

- CL-69 emptied Gate stats based on my reading of the Optimizer's
  earlier "Stats empty for Socket/Gate" line — owner oracle proved
  it wrong, CL-74 fixed it.
- CL-69 also wired StatName from a single per-node token — fine on
  the single-stat nodes the test covered, broken on the multi-row
  case I didn't probe. Owner oracle on Gate exposed it, CL-76 fixes.

Devlog 0069 + this devlog together memorialize the pattern:
**confirm lines are not game-oracles**. When the Optimizer
"confirms" a behaviour, it can still be subject to a future
in-game observation that contradicts. Build to the **game-side
truth**, treat consumer confirms as proposals, and run a live
acceptance probe before declaring done.

## What's next on #33

Optimizer turn — re-verify (StatName plus the unchanged Stats[]
decode + cache + hot-path + Start/Socket emptiness) and bless
`fr:consumed`. After that #33 closes.

**FR-C23** (tooltip render recipe — chrome + layout) is queued on
`#35 awaiting:casc` next.
