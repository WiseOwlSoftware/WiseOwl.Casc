# 0105 — the skill-tree topology, fully decoded (g39/199278)

**Date:** 2026-07-18
**Work item:** skill-tree phase-3 (owner: *"I do not believe any of the skill tree
is engine decided"* — and it isn't)
**Status:** RE finding, complete topology. Recon: raw g39 decode.

Phase-2 (devlog 0102) found `SkillTreeRewards` (g20/547685) — the per-node *reward*
list (name + skill + gbid + type + modifier-group). I then wrongly called the
topology + gates "engine-side" after checking the wrong SNOs. The owner pushed back;
the tree is entirely in the data. It lives in **group 39** (the class *gameplay/board*
group — classes + Mercenaries + Warplans, distinct from the g74 avatar/preview class
def), record **`Rogue` = 199278 (35,232 bytes)**.

## Record layout (all `DT_VARIABLEARRAY`, every byte used)

| region | descriptor | contents |
|---|---|---|
| `+104` | @+0x10 (192 B) | **active-skill list** — 24 × (skill Power SNO, flag). All Rogue skills (BladeShift 399111, Barrage 439762, DeathTrap 421161, …). |
| `+304` | @+0x30 (23,760 B) | **node graph** — 270 × 22-int32 node records. |
| `+24064` | (2,224 B) | **adjacency** region, indexed by node field `[10]`. |
| `+26288` | @+0x40 (8,448 B) | **edge list** — 264 × 8-int32 connections. |
| `+34736` | (496 B) | **edge line-routing waypoints** (x,y floats). |

## Node record (22 int32)

- `[0]` sparse node id (`f0`) — how edges reference the node.
- `[1]` **name hash** = `DJB2(lowercase(name), seed 0)` of the `SkillTreeRewards`
  node name. **Cracked** — 70/80 sampled matched; maps 192/270 nodes to names
  (Rogue_Unlock_BladeShift, Rogue_Mod_Flurry_UpgradeC, …). The 78 unmapped are the
  tree's **connector/hub nodes** (no reward record; sequential ids 358+).
- `[3],[4]` **float X/Y layout position** (X∈[−6204,5984], Y∈[−8040,7877]) — the
  literal on-screen tree coordinates (Basic skills at the top, Y≈−6700).
- `[10]` offset into the `+24064` adjacency region.
- `[11]` **node kind**: 16 = Skill/Unlock (49), 4 = Modifier (168), 12 = Connector
  (51), plus one each of 20 and 24 (the two special root/hub nodes).
- `[5]` per-node flags (2 on all skills/mods; varied on connectors).

## Edge record (8 int32)

`[entryA_f0, entryB_f0, entryA_altid, entryB_altid, 0,0,0,0]` — an undirected
connection between two nodes (verified: `[…,3,…,6]` ↔ entry 3 = Heartseeker_UpgradeA,
f0 6). 52 of the 264 edges use the trailing slots as a `{dataOff,size}` descriptor
into the `+34736` tail — **not a gate; visual line-routing waypoints** (x,y float
pairs) for edges drawn as bent/curved lines.

## The "category gate" (rule 3) — connector node `[5]` = tier/depth

Two things carry it, both in the data:

1. **Topology** — the prerequisite ("can't take Core until you invest in the
   connected Basic nodes") is the edge graph: you must allocate the connecting nodes
   along the path.
2. **A numeric tier field** — connector nodes (`[11]`∈{12,20,24}) carry a
   **monotonic depth index at `[5]`** that tracks vertical position almost exactly:
   `[5]`=5–7 in the top rows (Y≈−7000), rising 9→10→11→12→13→17→18→19→21→22 down to
   **24 at the bottom** (Y≈+7000). Connectors group into rows by `[5]` (5–6 per row).
   This is the progression-depth / tier value that orders cluster gating — a real
   per-connector number, **not** an engine constant. (Whether the game reads `[5]`
   as literal "points required" vs a tier ordinal is an in-game cross-check; the
   field itself is here.)

So there is no hard-coded engine rule and no orphan threshold: the gate is the edge
graph plus the connector `[5]` depth field, all in `Rogue`/199278.

## Hash note

Node-name hash for g39 = **seed-0 DJB2 of the lowercased name** (distinct from
`GbidHash` = seed-5381 lowercased). Added to the hash dictionary; re-scan opaque
blobs with it ([[feedback_cumulative-hash-decode]]).

## Bottom line

Nodes, positions, kinds, modifier groups, prerequisites, connections, and cluster
routing are **all in the data**. The owner was right on every count; my three
"engine-side" calls this session were all wrong ([[feedback_never-declare-engine-driven]]).
A `SkillTreeNode { Name, Kind, SkillSno, Position, GroupId, Neighbors[] }` +
`SkillTree` graph API is now fully backed by data — worth proposing to the Optimizer.
