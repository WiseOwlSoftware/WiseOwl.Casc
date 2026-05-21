# Devlog 0049 — FR-C16 R12: EXE RE of the binding mechanism + brute-force evaluation

*2026-05-21*

Owner directives: build the naming-convention surface (done, CL-51), then
EXE-RE the binding mechanism, and evaluate a brute-force symbol-ID process
(GPU optional) — preferring EXE symbols as the cracking source if present.

## EXE symbol extraction (`Diablo IV.exe`, 60 MB)

Extracted 285,462 distinct identifier runs (`[A-Za-z_][A-Za-z0-9_]{2,}`),
hashed each with D4 `TypeHash`/`FieldHash`. Findings:

**1. D4 field names are NOT in the EXE as plain ASCII.** `rgbaTint`,
`bActive`, `hImageFrame`, `snoTiledStyle`, `nTop` — all absent (the few
hits like `nLeft` are coincidental common tokens). Field names are hashed at
compile time and the strings stripped from the release build. So
string-extract+match recovers **class / asset / enum / action / data-source**
names, but **not** field names.

**2. The binding mechanism is named data-source binding (owner's prediction
confirmed).** The EXE carries a `DataBinding` / `SetObjectBinding` /
`OnChangeOfObjectBinding` system and discrete boolean source symbols:
`ParagonNodeIsPurchased` (and `Paragon_Node_Is_Purchased`),
`ParagonGlyphAffixIsActive`, `IsSelected`, `IsLocked`, `IsEquipped`,
`IsActive`. A bindable widget property binds to a named source; the
controller class is `ParagonBoardUI` (`ViewParagon_Controller`). The
per-widget wiring (property ← source) lives in that compiled C++ controller,
**not** in any SNO field (matches R10's exhaustive scene decode).

**3. Data-driven texture-by-state.** `ParagonNode_Texture_{Normal,Legendary,
Locked}` + `ParagonNode_Legendary_{Locked,Unlocked}` show the base-disc
texture is selected by node state — validating CL-50's base-disc-substitution
model and adding `Locked`/`Unlocked` states.

**4. CL-51 vocabulary validated + refined.** The engine's real predicate
names map onto `NodeFact` (Purchased=`ParagonNodeIsPurchased`,
Selected=`IsSelected`, Locked=`IsLocked`, Equipped=`IsEquipped`). Added
`NodeFact.Locked`/`Unlocked`; documented the EXE provenance. Node KINDS
confirmed from the purchase actions: Common, Magic, Rare, Legendary, Gate,
Socket.

Cracked `hTooltipText` `0x0204DBB8` (EXE) → `KnownFieldNames` + dictionary.
Saved `e:/tmp/exe-fieldhashes.tsv` (179k) + `exe-identifiers.txt` (285k) for
reuse. **Nothing copied** — names verified against the live blob; cited as
intel only.

## Brute-force symbol-ID evaluation (owner proposal)

Charset = 63 (`[A-Za-z0-9_]`); field names 4–20 chars. The killer is the
**28-bit `FieldHash` collision space** (`2^28 = 268M`), not throughput:

| candidate space | size | preimages per 28-bit hash |
|---|---|---|
| `63^5` | 9.9e8 | ~3.7 |
| `63^6` | 6.3e10 | ~233 |
| `63^8` | 2.5e14 | ~925,000 |

So raw charset brute force over any name ≥6 chars returns hundreds-to-millions
of *colliding* strings per target — it cannot identify *the* name. A
GPU (≈1e11–1e12 DJB2/s) exhausts `63^8` in minutes but the output is
collision garbage; **GPU speed does not solve the identification problem.**

**Conclusion:** raw brute force (CPU or GPU) is the wrong tool for naming.
The right tool is the owner's **pruned recombination** — a small,
semantically-meaningful candidate set (known prefixes `h/sno/n/e/b/dw/rgba/sz`
× word-stems × state suffixes), where a match is almost certainly real
because the candidate set is tiny vs `2^28`. The EXE's 285k identifiers are a
ready morpheme source (split on camelCase/underscore). **But for the FR-C16
activation goal this is moot** — the predicate/class/asset vocabulary we
needed is plain ASCII in the EXE (extracted above); only residual *field*
hashes (`0x0CDB00E9` etc., low value) would need recombination.

## Remaining gap

The literal per-widget wiring (`Node_Purchased.bActive ← ParagonNodeIsPurchased`)
is in `ParagonBoardUI` compiled code. Reading it verbatim needs disassembly
of that controller (a separate, patch-fragile effort beyond string extract).
The activation table CASC ships is the naming-convention decode, now
EXE-vocabulary-validated and provenance-marked.
