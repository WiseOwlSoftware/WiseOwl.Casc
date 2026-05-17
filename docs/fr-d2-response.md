# FR-D2 — response: first-party character-class roster + localized names

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Re:** `fr-d2` (class roster + localized names, decoupled from
> paragon).
> **Status: DELIVERED — answer (B): API gap; minimal typed surface
> shipped.** Authoritative byte/string spec: `casc-diablo4-format.md`
> §6.5; Appendix A `CL-17`. Answered from the **class data**, with
> **zero** dependency on the paragon SNO groups (§0 honored).

## 1. Verdict — (B)

The roster is reachable, but only by decoding (a) which SNO **group**
is the class definition and (b) the StringList table+label convention
for the localized name — both undocumented D4 data (RE, our side). So:
minimal typed surface.

## 2. What shipped (public API)

```csharp
public sealed record CharacterClass(int SnoId, string SnoName, string DisplayName);

IReadOnlyList<CharacterClass>
    Diablo4Storage.ReadCharacterClasses(string locale = "enUS");
```

- `SnoId` — the `SnoGroup.PlayerClass` (group 74) SNO id: the **stable
  per-class key**, never an array position; survives classes being
  added/reordered. (This is the *same* key FR-D1 puts on
  `ParagonBoardDefinition.ClassSnoId` — one shared class identity, two
  decoupled FRs.)
- `SnoName` — CoreTOC name (`Warlock`, `Sorcerer`, `Spiritborn`, …);
  stable internal token. Treat as opaque.
- `DisplayName` — localized, per the requested locale.
- Ordered by `SnoId` (deterministic); cached per locale; raw values
  only — no policy/imaging.

`SnoGroup.PlayerClass = 74` was already a named group; no new group
constant needed.

## 3. The decoded convention (for your audit)

- **Roster:** SNO **group 74** (`PlayerClass`) — independent of
  paragon, so it stays correct even if paragon leaves scope (your §0).
- **Localized name:** the **`General`** StringList table (SNO **4118**,
  §6.3), label **`"PlayerClass" + SnoName + "Male"`**. That gendered
  label is the markup-free display string; the base
  `PlayerClass<SnoName>` label carries D4 `|5sing:plur` pluralization
  markup, and the `…Male`/`…Female` variants are identical display
  strings on the verified build. Locale-aware.
- **Membership filter (data-driven, no hardcoded enum):** a group-74
  entry is a real playable class **iff** that label exists. The junk
  entry `Axe Bad Data` (SNO 159433) has no such label → excluded
  automatically; a new seasonal class appears automatically with no
  code edit. This is exactly the brittleness retirement you asked for
  (`Domain/Enums.cs ParagonClass` + `ClassByFilterIndex` guesses).

## 4. Acceptance (your §4 probes, vs live `3.0.2.71886`, enUS)

Full roster (ordered by SnoId), junk filtered:

| SnoId | SnoName | DisplayName |
|---|---|---|
| 131965 | Sorcerer | Sorcerer |
| 131966 | Druid | Druid |
| 169776 | Barbarian | Barbarian |
| 199275 | Rogue | Rogue |
| 199277 | Necromancer | Necromancer |
| 1206232 | Spiritborn | Spiritborn |
| 2079084 | Paladin | Paladin |
| 2207749 | Warlock | Warlock |

- Latest classes **Warlock**, **Paladin**, and **Spiritborn** present
  with exact localized names ✓
- Stable per-class key = the PlayerClass SNO id (not array position) ✓
- Deterministic per (class, locale); defined for all classes; **no
  paragon-group dependency** (resolved purely from group 74 + table
  4118 — verified by construction) ✓
- `Axe Bad Data` excluded with no hardcoded list ✓
- Locale-aware: `ReadCharacterClasses("deDE")` returns the same roster
  with localized `DisplayName`s ✓

Recorded with `CL-17` + the verified-anchor table in §6.5; asserted by
`ReadCharacterClasses_returns_first_party_roster` (passes vs live
build; full suite green, 0 warnings).

## 5. How to integrate

Replace the hardcoded `ParagonClass` enum + `ClassByFilterIndex`
guesses with the data-driven roster:

```csharp
foreach (var c in d4.ReadCharacterClasses(locale))
    // c.SnoId   → stable key (join to ParagonBoardDefinition.ClassSnoId)
    // c.SnoName → stable internal token
    // c.DisplayName → localized UI label
```

No paragon-derived class inference, no enum edits per season.

## 6. Boundary & what is owed

Library = the decoded group + StringList convention + raw values
(SnoId/SnoName/DisplayName). No policy/imaging; D4 markup (if any in a
locale) is left intact for the consumer. Decoupled from FR-D1 by
design — they share only the stable class key. Nothing further owed
unless the gated NuGet release ships, or a seasonal build changes
`.build.info` Build Key — then re-verify (Appendix D); the data-driven
membership filter absorbs new classes without code changes. Same
amend-until-next-publish contract as FR-C7 §7.

## 7. Round log

- **Round-1 (open, 2026-05-17):** consumer raises the recipe-or-gap
  question, deliberately decoupled from FR-D1.
- **Round-1 (delivered, 2026-05-17):** library — answer (B). Roster =
  group 74; localized name = `General` table 4118, label
  `PlayerClass<SnoName>Male`; data-driven junk filter. Shipped
  `CharacterClass` + `Diablo4Storage.ReadCharacterClasses(locale)`;
  spec §6.5 + CL-17; integration test asserts the full roster vs the
  live build. Consumer replaces the hardcoded enum with the
  data-driven list.
