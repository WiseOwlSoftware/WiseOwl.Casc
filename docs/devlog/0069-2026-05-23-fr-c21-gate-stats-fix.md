# 0069 — FR-C21 game-oracle correction: Gate nodes DO carry stats

2026-05-23 · CL-74 · branch `fr-c21-gate-stats-fix`

## Trigger

Owner in-game observation, relayed via the Optimizer on `casc-fr#33`:

> *"Gate nodes display in the game as having stats: typically, maybe
> always, +5 to the four basic stats."*

In-game tooltip on `Generic_Gate` (994337) verbatim:

```
+5 Strength
+5 Intelligence
+5 Willpower
+5 Dexterity
(Max board amount reached)
```

(The footer "Max board amount reached" is the allocation-cap UI on a
fully-attached board — content-independent, out of scope for the stat
projection.)

Engine's user-facing name for this node kind is **"Board Attachment
Gate"**, not "exit gate" / "gate" — I've been using the internal
term throughout. The class enum stays `ParagonNodeKind.Gate` for
back-compat; XML docs now cite the engine term.

## The defect

CL-69's `ParagonNodeInfoBuilder.Build` bucketed Gate with Start /
Socket as zero-stat:

```csharp
var stats = kind is ParagonNodeKind.Start
    or ParagonNodeKind.Socket
    or ParagonNodeKind.Gate            // <- wrong
    ? Array.Empty<ParagonNodeStat>()
    : BuildStats(catalog, node, name, formulas);
```

The Optimizer's earlier "minor confirms" line in their original
requirements reply read: *"I expect Stats empty for Socket/Gate
(sockets grant via the glyph; gates are attachment markers) with
HasSocket/IsGate flags carrying the meaning."* I built to that, and
they signed off — but the in-game observation disproves the Gate
half. Socket is still correct (the seated glyph grants the stat, not
the socket itself). Start is still correct (CL-66 verified all 7
class start boards have `ptAttributes.Count == 0`).

The raw `ParagonNodeDefinition` on `Generic_Gate` (994337) was
always correctly carrying the four attribute rows — CL-66's recon
even dumped `ptAttributes@32` with `dataSize == 352 == 4 * 88` — I
just chose to drop them at projection time. CL-74 reverses that
choice.

## What ships

One-character fix (the `or ParagonNodeKind.Gate` clause removed)
plus the XML-doc revision on `ParagonNodeKind.Gate` and
`ParagonNodeInfo.Stats` to record the engine's user-facing name and
the correct emptiness criterion:

```csharp
// In ParagonNodeInfoBuilder.Build:
var stats = kind is ParagonNodeKind.Start or ParagonNodeKind.Socket
    ? Array.Empty<ParagonNodeStat>()
    : BuildStats(catalog, node, name, formulas);
```

`IsGate` continues to carry the structural meaning (the
attachment-marker semantic is content-independent — the gate is
authored once per attachable board, the four stats are content that
just happens to sit on it).

## Tests

92/92 still green on `3.0.2.71886`. The acceptance matrix's existing
socket-empty assertion (`Generic_Socket` 681756 → `Stats.Empty`) is
unchanged; the new Gate assertion asserts:

- `GetNodeInfo(994337).Kind == Gate`
- `info.IsGate == true`
- `info.Stats.Count == 4`
- `info.Stats` keyed by `AttributeId` covers `{9, 10, 11, 12}`
- Each stat: `FlatValue == 5.0`, `Unit == Flat`

(Lazy bonus check that the live decode matches the in-game oracle
exactly — same magnitudes everywhere a Gate is reachable, per the
Optimizer's note.)

## Lesson — pattern memorialized

This is the same mistake `feedback_re-all-fields` was guarding
against, applied at the projection layer instead of the decode
layer: I had the raw bytes (CL-66 even decoded them), but the
projection layer dropped the field based on a "kind is structural,
so no stats" assumption that wasn't checked against the in-game
display. The earlier feedback applies upward: every field of every
data type we use — including in the projection-policy decisions —
needs to survive a game-oracle check before it's bucketed.

## Optimizer's terminology alignment

The Optimizer will rename internal references in its codebase /
memory from "exit gate" → "Board Attachment Gate" in the next
consume-verify after this lands. The library's `IsGate` /
`ParagonNodeKind.Gate` names stay (XML docs annotated with the
engine term for cross-reference).

## What's next on #33

Optimizer turn after this lands — re-verify the Gate stats projection
and bless `fr:consumed`. No other corrections flagged.
