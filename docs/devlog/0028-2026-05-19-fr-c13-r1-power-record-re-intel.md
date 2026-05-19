# 0028 — FR-C13 R1: Power-record RE intel + needs:owner on the SF_N decode

*2026-05-19*

Per the owner's direction, started FR-C13 R1 (#23 — structured numeric
effect parameters for Paragon Legendary node Powers) with a web-search
leg-up before fresh RE on the deferred Power record. The community has
mapped most of the PowerDefinition struct in third-party schemas;
those schemas got us **a real map** but turned out to be partially
stale for build `3.0.2.71886`. Honest disposition: surface the
findings here so a future RE round doesn't repeat the dead-ends, and
raise `needs:owner` on the SF_N specific decode because the records
do not cleanly settle the FR's stated model on the named anchor
(Dynamism `SnoPassivePower=2524312`).

## Intel consulted (cited; not imported)

Per the new memory `feedback_third-party-re-as-intel`, treat
community schemas as an offset / field-name map for CASC's
first-party RE — never copy code or data, always verify against the
raw blob.

- [`blizzhackers/d4data` (archived, richest schemas)](https://github.com/blizzhackers/d4data) —
  `definitions/!PowerDefinition.14af0cb6.yml`,
  `definitions/!ScriptFormulaInfo.20e37537.yml`,
  `basic_definitions/!DT_STRING_FORMULA.920cd243.yml`.
- [`DiabloTools/d4data` (active successor)](https://github.com/DiabloTools/d4data) —
  parsed JSON data + current-season checksums.
- [`Dakota628/d4parse`](https://github.com/Dakota628/d4parse) — Go
  parser that uses the d4data definitions.

## Third-party schema (PowerDefinition) — key fields

| Offset | Field | Type |
|---|---|---|
| `0x198` | `tAttackSpeed` | DT_STRING_FORMULA |
| `0x1d0` | `tCombatEffectChance` | DT_STRING_FORMULA |
| `0x230..0x2f0` | per-cost / cooldown formulas | DT_STRING_FORMULA each |
| `0x420` | `tAttackRadius` | DT_STRING_FORMULA |
| `0xb20` | `arBuffs` | PowerBuffDefinition[] |
| `0xb30` | `nFormulaCount` | INT |
| `0xb38` | `ptScriptFormulas` | ScriptFormulaInfo[] |
| `0xb48` | `arPayloads` | PowerPayloadDefinition[] |
| `0xb58` | `arMods` | PowerMod[] |

`ScriptFormulaInfo` = 32 bytes (one `DT_STRING_FORMULA` at offset 0).
`DT_STRING_FORMULA` = 32 bytes, **8 × uint32_t, all `unk_*`** in the
public schema.

## Verified vs stale (against the Dynamism blob)

**Verified.** Per-cost STRING_FORMULA fields at the schema-listed
offsets (`0x1d0`/`0x230`/`0x250`/`0x270`/…/`0x420`) all have valid
(offset, length) pointers into the blob's tail-data region. Reading
the tail bytes at the first such pointer (offset `0xCE0`) returns:

```
0x0CE0: 41 74 74 61 63 6B 73 5F 50 65 72 5F 53 65 63 6F  | Attacks_Per_Seco
0x0CF0: 6E 64 5F 54 6F 74 61 6C 00 00 00 00 05 00 00 00  | nd_Total........
0x0D00: 00 00 00 00 E4 00 00 00 FE FF FF FF 00 00 00 00  | ................
0x0D10: 00 00 00 00 30 00 00 00 06 00 00 00 00 00 00 00  | ....0...........
0x0D20: 00 00 00 00 30 00 00 00 06 00 00 00 00 00 00 00  | ....0...........
…
0x0D60: 00 00 00 00 31 00 00 00 06 00 00 00 00 00 80 3F  | ....1..........?
…
0x0DC0: 00 00 00 00 36 00 00 00 06 00 00 00 00 00 C0 40  | ....6..........@
```

Real RE find: tail data begins with an ASCII identifier
(`Attacks_Per_Second_Total`) followed by **16-byte repeating
structures** that encode `(0, NN, 6, float)` where `NN` is `48` /
`49` / `54` (i.e. ASCII `'0'` / `'1'` / `'6'`) and `float` is the
matching scalar (`0.0` / `1.0` / `6.0`). Likely an inline
expression AST: type-marker + arity + scalar. Not fully decoded;
more anchors needed to confirm the AST shape.

**Stale.** The schema's `nFormulaCount` @ `0xb30` and
`ptScriptFormulas` @ `0xb38` do **not** decode cleanly against the
current build:

- **Dynamism (`SNO 2524312`)**: `nFormulaCount = 0`,
  `ptScriptFormulas+0/+4 = 0x00000000 / 0x00000000`. Yet the
  decoded Description text references `SF_0`, `SF_2`, `SF_3` — so
  the SF_N data must live elsewhere.
- **Druid_Boulder (`SNO 238345`)**: `nFormulaCount = 0xF0F03371`
  (4,043,178,673), `ptScriptFormulas+0/+4 = 0x28802C04 / 0xAD18D664`.
  These are not a count + array pointer — they look like
  GBID-shaped 32-bit hashes.
- **Druid_Trample (`SNO 258243`)**: same pattern — values that
  look like GBIDs, not array metadata.

Two possible explanations:
1. The build has shifted those struct offsets since the schemas were
   captured (the schemas pre-date `3.0.2.71886`).
2. The schemas conflated two different mechanisms (Power-internal
   formulas vs. node-bound Effects), and Legendary passives' SF_N
   actually lives on the consuming `ParagonNode` record's
   `Effects[]` / `AttributeId+Formula` (which CASC already exposes
   typed) — not in the Power record at all.

The FR-C13 R1 message itself cites `ParagonNodeAttribute`
(`AttributeId` + `FormulaGbid` / inline expression) as the parallel
mechanism. It is plausible that for Legendary passives the SF_N
substitution slots come from the **node's** `Effects[]`, with the
Power record only contributing the localized template text. That
would mean the FR's "extend PowerDefinition" framing is targeted at
the wrong record class for the Legendary use-case.

## Disposition: needs:owner

The records do not cleanly settle:

- Whether SF_N for a Legendary passive's localized template is filled
  from the Power record or from the consuming `ParagonNode`'s
  `Effects[]` (CASC's existing surface).
- Whether the `0xb30`/`0xb38` schema offsets are stale or pointing at
  the wrong mechanism.
- The DT_STRING_FORMULA inner layout (8 × uint32 `unk_*`) requires
  more anchor data than this session can produce confidently.

Per B-6 ("pause rather than guess on anything the authoritative
records can't settle") this is a `needs:owner` on #23. Options the
owner can consider:

1. **Re-frame the FR.** Confirm whether the Legendary-node SF_N
   data lives on `ParagonNode.Effects[]` (already exposed). If yes,
   CASC's existing surface might already cover the consumer's
   Stage-2 magnitude needs — the FR scope shrinks to a documentation
   round rather than a Power-record extension.
2. **Anchor with non-Legendary powers.** RE the active-skill Power
   blobs (Druid_Boulder, etc.) where the schema offsets show non-zero
   values, derive the live `nFormulaCount`/`ptScriptFormulas`
   semantics from comparison, then verify the SF_N mapping.
3. **Defer until a third-party schema refresh covers the current
   build.** Cheap; loses no work; takes a community-dependency.

Whichever option the owner picks, what's verified in this round
(per-cost STRING_FORMULA slot offsets + the tail-data ASCII
identifier + 16-byte AST node pattern) is good intel for the next
session. No code change this round; documentation only, straight to
main per the doc-only convention.
