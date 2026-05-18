# WiseOwl.Casc.Diablo4 — complete API state

> **To:** the ParagonOptimizer (consumer) session (`e:\Paragon`).
> **From:** the WiseOwl.Casc.Diablo4 session (`e:\Casc`).
> **Purpose:** the authoritative snapshot of the *entire* shipped
> Diablo IV public surface after FR-C7, FR-D1/D2/D3, the backlog
> completion (FR-11..16, B1–B6, FR-14, C6), and FR-C8. Supersedes the
> per-FR response docs as the single integration reference.
> **Published:** **`0.2.0-alpha`** on nuget.org — both
> `WiseOwl.Casc` and `WiseOwl.Casc.Diablo4` (PackageReference it; the
> old `artifacts/fr-c7-pack` local pack is obsolete — ignore it). It
> contains FR-C7, FR-D1/D2/D3, FR-14, and C6; that surface is **frozen
> by NuGet immutability** (no longer amendable).
> **Unreleased on `main` (not in any package yet): FR-C8** (start/gate
> composite, §10.12/CL-23 — see `fr-c8-response.md`). It ships in a
> future owner-batched release (version TBD); only FR-C8 is still
> contract-amendable, until *that* release. **Verified build:**
> Diablo IV `3.0.2.71886`. Byte/string spec: `casc-diablo4-format.md`
> (`CL-*`).

---

## 0. The boundary (unchanged in spirit; C6 freeze lifted)

The library decodes **raw data + first-party conventions**; the
consumer keeps **policy/modeling**. Concretely the library owns:
transport, CoreTOC, id-keyed read, shared-payload, combined-meta,
texture decode, StringList, the typed paragon/GameBalance record
decoders, the C6 non-paragon readers (identity + localized text), the
D4 hashes, and every D4 *naming/encoding convention* (decoded once,
documented `CL-*` + re-verify, exposed typed — never a consumer
regex/guess; the "SNO names are opaque ids" durable principle, spec
Appendix C).

It does **not** ship: a formula evaluator, the 6 calibrated intrinsics,
the scoring/objective model, relight/composite calibration, the
bundled-JSON schema, or a model of the multi-KB Power/Item gameplay
engine records. Where data is silent the API returns `0`/`null`/empty
with documented, evidence-backed reasoning — never a fabricated value.

**One shared class key across everything:** the `PlayerClass` (group
74) SNO id. `CharacterClass.SnoId` == `ParagonBoardDefinition.ClassSnoId`
== entries of `ParagonGlyphDefinition.UsableByClassSnoIds` ==
`PlayerClassDefinition.SnoId`. Join on it.

---

## 1. Entry point & transport

```csharp
using var d4 = Diablo4Storage.Open(@"D:\Diablo IV");      // or OpenAsync
// or Diablo4Storage.Attach(existingCascStorage)

d4.CoreToc                      // CoreToc: name↔id index, groups
d4.Casc                         // CascStorage (transport escape hatch)

// Raw id-keyed read (group is informational; TVFS address is id-only)
byte[]  ReadSno(SnoGroup|int group, int id, SnoFolder=Meta, int subId=-1)
bool    TryReadSno(SnoGroup|int group, int id, SnoFolder, out byte[], int subId=-1)
Stream  OpenSno(...)            Task<byte[]> ReadSnoAsync(...)
IEnumerable<(int Id,byte[] Bytes)> ReadGroup(SnoGroup, SnoFolder)   // bulk, resident-state
static string SnoPath(int id, SnoFolder=Meta, int subId=-1, prefix="Base")
```

`SnoFolder` = `Child | Meta | Payload | PayLow | PayMed`. **FR-14
closed:** `Child` sub-blobs resolve by `Base\Child\<id>-<subId>` (the
same proven path as Meta/Payload); ≈547k exist; pinned anchor SNO
`1015186`/group 71. `Payload` transparently follows the `0xABBA0003`
shared-payload alias.

`CoreToc`: `TryGetName/GetName(group,id)`, `TryGetId/GetId(group,name)`,
`EntriesInGroup(group)`, `Entries`, `FormatHashFor(group)`.
`SnoGroup` names: `GameBalance=20, Power=29, Texture=44, Item=73,
PlayerClass=74, ItemType=98, Affix=104, ParagonNode=106,
ParagonBoard=108, ParagonGlyph=111, ParagonGlyphAffix=112,
StringList=42`. Any unnamed group: use the `int` overloads.

---

## 2. D4 hashes (game-wide, reusable)

```csharp
uint Diablo4.TypeHash(string)   // DJB2 seed 0, full u32 (case-sensitive)
uint Diablo4.FieldHash(string)  // TypeHash & 0x0FFFFFFF (28-bit)
uint Diablo4.GbidHash(string)   // lowercased; e.g. ParagonNodeCoreStat_Normal == 0x42C16A1B
```

---

## 3. Localized strings (StringList)

```csharp
StringListCatalog GetStrings(string locale = "enUS")     // cached per locale
  .Table(int sno) / .Tables / .TryGet(int sno,label,out) / .TryGet(label,out)
bool d4.TryGetString(int tableSno, string label, out string, locale="enUS")
bool d4.TryGetString(string label, out string, locale="enUS")
```

Per-locale `0x44CF00F5` bundle (`base/StringList-Text-<locale>.dat`).
Values carry D4 markup (`{c_*}`, `[Affix_Value_1|%|]`, `|5s:p|`,
`{s1}` …) — strip/format consumer-side (policy).

**Generalized sibling-StringList convention (§6.7):** a record's
localized text is the group-42 SNO `"<TypePrefix>_" + recordSnoName`,
name-keyed via CoreTOC. The typed readers below already apply it; the
table is: `ParagonBoard_`(`Name`), `Item_`(`Name`/`Flavor`/
`TransmogName`/`Description`), `Affix_`(`Desc`), `Power_`(`name`/
`desc`). Class names are the parallel `General`(4118) case (§5).

---

## 4. Paragon (B1–B6 + FR-C7 + FR-D1/D3)

```csharp
ParagonBoardDefinition ReadParagonBoard(int id)            // group 108
  .SnoId .Width .Cells(IReadOnlyList<int?>) .NodeCount .CellAt(r,c)
  // FR-D1 (resolved from the SNO-name convention, library-side):
  .ClassSnoId  .ClassSnoName  .BoardIndex
bool   TryReadParagonBoardName(int boardSnoId, out string, locale="enUS")  // FR-D1
string ReadParagonBoardName(int boardSnoId, locale="enUS")                 // throws

ParagonNodeDefinition ReadParagonNode(int id)              // group 106
  .SnoId .RarityOverride .Rarity(ParagonRarity) .HasSocket .IsGate
  .HIcon .HIconMask .SnoPassivePower .Attributes(NodeAttribute[])
  // NodeAttribute: AttributeId, NParam, ParamPlus12, FormulaGbid,
  //                InlineFormula, IsInline

ParagonGlyphDefinition ReadParagonGlyph(int id)            // group 111
  .SnoId .AffixSnoIds(≤3)
  .UsableByClassSnoIds(IReadOnlyList<int>)   // FR-D3: PlayerClass SNO ids;
  //   empty for byte-only Parse or malformed/junk (honest sentinel)

ParagonGlyphAffixDefinition ReadParagonGlyphAffix(int id)  // group 112
  .SnoId .AffectedRarity .Operation .Base .PerLevel        // raw magnitudes

AttributeFormulaTable ReadAttributeFormulas(int id = 201912)   // GameBalance
  .ByName .TryGetFormulaText(name,out) .TryGetNameByGbid(gbid,out)
  // formula TEXT + name/GBID index only — NO evaluation (consumer's)
```

**FR-C7 render layout** (zero calibrated constants; consumer owns the
single resolution/zoom scalar):

```csharp
UiScene ReadUiScene(int snoId)              // generic 0xE4825AB8 graph
  // UiScene(SnoId,Widgets); UiWidget(Name,ClassId,Fields);
  // UiField(FieldHash,TypeHash,RawValue,HasValue)
ParagonRenderLayout ReadParagonRenderLayout()              // SNO 657304
  .Ratios(PitchRef=100/1200, DiscRef=86/1200, *OverDisc=100/86,
          Provisional=false) .CanvasReference(1920×1200)
  .NodeContainer .NodeTemplate .BoardRotationQuadrant(0)
  .Disc .Symbol .States(18 × StateElements, decode-true)
```

Texture handles are raw `uint` (== `TexFrame.ImageHandle`, never
pre-resolved). Per-rarity tint / grey ring / connectors / pulse-anim
are **not in the data** by design (`null`/`0`) — keep your recipe/
procedural code (FR-C7 §6).

---

## 5. Classes (FR-D2)

```csharp
IReadOnlyList<CharacterClass> ReadCharacterClasses(locale="enUS")  // group 74
  // CharacterClass(int SnoId, string SnoName, string DisplayName)
  // ordered by SnoId; junk ("Axe Bad Data") filtered data-driven;
  // SnoId == the shared class key
```

Roster on `3.0.2.71886`: Sorcerer 131965, Druid 131966, Barbarian
169776, Rogue 199275, Necromancer 199277, Spiritborn 1206232,
Paladin 2079084, Warlock 2207749.

---

## 6. C6 non-paragon typed readers (identity + localized text)

```csharp
PlayerClassDefinition ReadPlayerClass(int id)              // group 74
  .SnoId  .EClass            // binary eClass@payload+16 (the glyph-rank field)

PowerDefinition ReadPower(int id, locale="enUS")           // group 29
  .SnoId  .Name  .Description           // sibling Power_<n> name/desc

AffixDefinition ReadAffix(int id, locale="enUS")           // group 104
  .SnoId  .Description                  // sibling Affix_<n> Desc

ItemDefinition ReadItem(int id, locale="enUS")             // group 73
  .SnoId  .Name  .Flavor  .TransmogName // sibling Item_<n>
```

All four: byte-only `static Parse(blob)` yields **identity only**
(localized fields need CoreTOC → empty). The deep gameplay record
(skill/item engine struct) is intentionally **not** decoded — that is
your stat-effect model, not a library gap. eClass ordinals: Sorcerer 0,
Barbarian 1, Rogue 3, Druid 5, Necromancer 6, Spiritborn 7, Paladin 9,
Warlock 10 (sparse; rank-compact 0..7 = the glyph `fUsableByClass`
slot order, FR-D3).

---

## 7. Textures

```csharp
CombinedTextureMeta d4.TextureMeta            // 0x44CF00F5 catalog
  .Get(snoId)/.TryGet(snoId,out TextureDefinition)
bool d4.TryGetIconFrame(uint handle, out int atlasSno, out TexFrame frame) // B6
DecodedImage TextureDecoder.DecodeMip0(this TextureDefinition, payload)
  // BC1/BC3 → straight-alpha RGBA32; caller crops with TexFrame.PixelRect
SharedPayloadMapping d4.SharedPayloads        // 0xABBA0003
```

Imaging/PNG/compositing/relight stay consumer-side.

---

## 8. What changed since the last per-FR report

- **FR-D1** board name + typed `ClassSnoId`/`ClassSnoName`/`BoardIndex`
  (retire `BoardNameRegex`/`NormaliseClass`/`Split('_')`).
- **FR-D2** `ReadCharacterClasses` (retire the hardcoded `ParagonClass`
  enum / `ClassByFilterIndex`).
- **FR-D3** `ParagonGlyphDefinition.UsableByClassSnoIds` (retire the
  Maxroll `classFilter` + bit-index guess). The `ParagonClass`-enum →
  first-party-key migration is fully unblocked, end-to-end.
- **FR-14** `SnoFolder.Child` acceptance pinned.
- **C6** scope-freeze lifted: `ReadPlayerClass`/`ReadPower`/`ReadAffix`/
  `ReadItem` + the generalized sibling-string convention.
- **FR-C8** (*on `main`, unreleased — not in `0.2.0-alpha`*):
  `UiWidget.ExtraLayerValues` + corrected `start.*`/`gate.*`
  `States.Layers` (§10.12 / CL-23; `fr-c8-response.md`). Available only
  if you build from source; the published `0.2.0-alpha` does **not**
  have it (NuGet immutability — it ships in a later batched release).

Backlog (`docs/feature-backlog.md`): FR-11..16 + B1–B6 + C6 all DONE —
**nothing deferred**. The boundary held (no evaluator, no fabricated
gameplay model).

## 9. Integration checklist

1. `PackageReference` **`WiseOwl.Casc.Diablo4` `0.2.0-alpha`** (and
   `WiseOwl.Casc 0.2.0-alpha`) from nuget.org — published & immutable.
   (FR-C8 is not in it; build from `main` if you need start/gate
   composites before the next release.)
2. Class identity: one key — `CharacterClass.SnoId`. Join boards
   (`ClassSnoId`), glyphs (`UsableByClassSnoIds`), `ReadPlayerClass`.
3. Delete the retired consumer code (board regex, ParagonClass enum,
   ClassByFilterIndex, Maxroll classFilter).
4. Localized text: pass your UI locale; strip D4 markup consumer-side;
   empty = your fallback (the library never bakes a fallback policy).
5. Keep all policy/modeling consumer-side (formulas, intrinsics,
   scoring, imaging) — unchanged by C6.
6. Re-verify trigger: `.build.info` Build Key change (seasonal). Re-run
   integration tests; the throw-on-ambiguity guards turn naming drift
   into a loud failure, not a silent wrong answer.

## 10. Round log

- **2026-05-17:** complete-state snapshot issued after FR-C7,
  FR-D1/D2/D3, FR-14, and C6 (scope-freeze lifted). Consumer integrates
  off this single reference; per-FR response docs remain for rationale.
