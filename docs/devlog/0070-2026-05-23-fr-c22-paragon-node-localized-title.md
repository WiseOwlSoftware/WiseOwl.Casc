# 0070 — FR-C22: `ParagonNodeInfo.LocalizedTitle` via the sibling StringList

2026-05-23 · CL-75 · branch `fr-c22-paragon-node-localized-title`

## Trigger

Optimizer opened `casc-fr#34` (2026-05-23) right after the Gate-stats
correction landed:

> *Add a localized-title field to `ParagonNodeInfo` carrying the
> engine's user-facing tooltip title — the bold line at the top of
> every node's hover tooltip in-game.*

Two anchors from owner game-oracle (2026-05-23):

- Warlock Start board start node → "Paragon Starting Node"
- Generic_Gate → "Board Attachment Gate"

The Optimizer's CatalogProbe confirmed no existing surface exposes
these strings (no `NameContains` hit; `Facets` / `Related` on a node
ref empty).

## Recon

The §6.7 sibling-StringList convention (CL-15 / CL-20 / CL-22) is the
obvious candidate — every other localized-text projection on the
library uses the same shape. A direct `SnoScan find` confirms:

```
ParagonNode_Generic_Gate     (StringList 1111635)
ParagonNode_StartNodeBarb    (StringList 1111433)
ParagonNode_Warlock_Rare_006 (StringList — class-specific rare)
ParagonNode_Generic_Magic_DamageToElite  → no hit (stat node)
```

`SnoScan stl` on the two anchor SNOs:

```
sno=1111635 ParagonNode_Generic_Gate    [Name] = Board Attachment Gate
sno=1111433 ParagonNode_StartNodeBarb   [Name] = Paragon Starting Node
```

Exact match against the owner's in-game oracle. The pattern is the
same as `ParagonBoard_<SnoName>` (CL-15) — same prefix-and-label
shape, same label name (`Name`).

## What ships

A six-line addition to `Diablo4Storage` (it delegates to the
already-shipped private `TryReadSiblingString` helper):

```csharp
public bool TryReadParagonNodeTitle(
    int nodeSnoId, out string name, string locale = DefaultLocale) =>
    TryReadSiblingString(
        SnoGroup.ParagonNode, nodeSnoId,
        ParagonNodeStringTablePrefix, ParagonBoardNameLabel,
        locale, out name);

private const string ParagonNodeStringTablePrefix = "ParagonNode_";
```

(Re-uses the existing `ParagonBoardNameLabel = "Name"` constant —
the engine label is the same across the convention.)

And one new positional field on `ParagonNodeInfo`:

```csharp
public sealed record ParagonNodeInfo(
    int Sno,
    string Name,
    string LocalizedTitle,             // <-- new (CL-75)
    ParagonNodeKind Kind,
    ParagonRarity Rarity,
    AssetRef? Icon, AssetRef? IconMask,
    AssetRef? PassivePower,
    string? PassivePowerName,
    IReadOnlyList<ParagonNodeStat> Stats,
    bool HasSocket, bool IsGate);
```

`ParagonNodeInfoBuilder.Build` populates it via the new low-level
surface — falls back to `string.Empty` when the sibling is absent.
This is the same (data-mine token, localized projection) pair the
Optimizer flagged in their precedent note: `ParagonBoard.Name` was
the first instance (CL-15), `ParagonNodeInfo.PassivePowerName` is
the second (CL-69), and `LocalizedTitle` is the third.

## What I almost got wrong

My first draft asserted `Warlock_Rare_006.LocalizedTitle ==
string.Empty` — the test failed loudly: `Actual: "Binding"`. Named
class-specific rare nodes DO have a sibling (it's their authored
display name: "Binding", "Fathomless", "Pyrosis", etc.) — only the
**generic** `Generic_<Rarity>_<Token>` stat-node family lacks one.
This is the "named rare" case I conflated with "named-token stat
node" in my mental model. Spec § and XML doc now spell out the
distinction explicitly.

## Acceptance

The live matrix carries four assertions covering every populated
case + the honest-empty case:

| Node                     | Kind  | LocalizedTitle           |
|---                       |---    |---                       |
| `Generic_Gate` (994337)  | Gate  | `"Board Attachment Gate"`|
| `StartNodeBarb` (830650) | Start | `"Paragon Starting Node"`|
| `Warlock_Rare_006` (2451111) | Rare | `"Binding"`           |
| `Generic_Magic_Armor` (671247) | Magic | `""` (honest empty) |

Plus the low-level `TryReadParagonNodeTitle` returns `(true,
"Board Attachment Gate")` for 994337 and `(false, "")` for 671247.

92/92 tests green on `3.0.2.71886`.

## What's next on #34

Optimizer turn — re-verify and bless `fr:consumed`. The
ParagonDataGen probe's negative-result test should flip to positive
on the next consume-verify.

## Coupling with FR-C23

The Optimizer flagged that FR-C23 (the tooltip render recipe) and
FR-C22 (this CL) could be unified if CASC preferred. Two reasons to
keep them separate, both pragmatic:

- **FR-C22 is a one-line projection over an existing convention** —
  shipping it now unblocks the Optimizer's tooltip-text rendering
  immediately, well before the chrome decode lands. The consumer
  can show titles + stats today and add chrome later.
- **FR-C23 is a multi-CL UI-scene RE** (locate the tooltip scene
  SNO; decode its widget graph; project chrome + layout + per-state
  bindings). That's the same shape as FR-C7 was — a separate
  thread with its own acceptance gates. Bundling would slow both.

So FR-C22 ships here; FR-C23 stays open as the next investigation
thread.
