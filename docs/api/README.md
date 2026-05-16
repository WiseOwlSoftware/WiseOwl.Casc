# API reference

Complete, generated API documentation for the two shippable packages.

| Package | Reference | Purpose |
|---|---|---|
| **WiseOwl.Casc** | [API index](WiseOwl.Casc/WiseOwl.Casc.md) | Game-agnostic CASC / TACT / TVFS / BLTE transport. |
| **WiseOwl.Casc.Diablo4** | [API index](WiseOwl.Casc.Diablo4/WiseOwl.Casc.Diablo4.md) | Diablo IV module — CoreTOC, SNO read, typed paragon/GameBalance records, textures, StringList. References `WiseOwl.Casc`. |

```
dotnet add package WiseOwl.Casc
dotnet add package WiseOwl.Casc.Diablo4   # also pulls WiseOwl.Casc
```

## How this is generated (and kept honest)

The **XML doc comments in the source are the single source of truth** —
every public type and member carries them (a binding project rule). This
tree is generated from them with [`xmldocmd`](https://github.com/ejball/XmlDocMarkdown),
pinned in [`.config/dotnet-tools.json`](../../.config/dotnet-tools.json):

```
scripts/gen-api-docs.sh          # or: pwsh scripts/gen-api-docs.ps1
```

CI regenerates and **fails the build on any diff** (the `api-docs` job), so
the committed reference can never drift from the public surface. Generated
from the `netstandard2.0` build — the public API is identical across all
target frameworks (`netstandard2.0;net8.0;net10.0`); no API is
TFM-conditional.

Layout: `docs/api/<Package>/<Package>.md` is the package index;
`<Package>/<Namespace>/<Type>.md` documents a type; per-member pages live
under `<Type>/`. Pages cross-link, and type headers link back to source on
GitHub.

## Reading guide — the public surface by layer

The reference is exhaustive (it documents *everything* public). For most
consumers only the top two layers matter:

### Consumer-facing — `WiseOwl.Casc`
- `CascStorage` — open a local install; read by `ContentKey`/`EncodingKey`
  or TVFS path. `ContentKey` / `EncodingKey` / `Md5Key` — typed,
  allocation-free content-addressing keys.
- `CascOpenOptions`; the `CascException` family
  (`CascFormatException`, `CascContentNotFoundException`,
  `CascEncryptedContentException`).

### Consumer-facing — `WiseOwl.Casc.Diablo4`
- `Diablo4Storage` — the facade: `Open`/`Attach`, `ReadSno`/`TryReadSno`,
  `ReadParagonBoard`/`Node`/`Glyph`/`GlyphAffix`,
  `ReadAttributeFormulas`, `TextureMeta`, `GetStrings`/`TryGetString`,
  `TryGetIconFrame`, `ReadGroup`.
- `CoreToc` / `SnoEntry` / `SnoGroup` / `SnoFolder`; the typed records
  (`ParagonBoardDefinition`, `ParagonNodeDefinition` + `NodeAttribute` +
  `ParagonRarity`, `ParagonGlyphDefinition`,
  `ParagonGlyphAffixDefinition`, `AttributeFormulaTable` +
  `AttributeFormula` + `FormulaRange`); textures
  (`CombinedTextureMeta`, `TextureDefinition`, `TexFrame`,
  `TextureDecoder`, `DecodedImage`, `TextureCodec`); strings
  (`StringListCatalog`, `StringListTable`); `SharedPayloadMapping`;
  `Diablo4.GbidHash`.

### Infrastructure / advanced (public for cross-assembly + testing)
`WiseOwl.Casc.Configuration` (`BuildInfo`, `BuildConfiguration`),
`WiseOwl.Casc.Indices` (`LocalIndex`, `ArchiveLocation`),
`WiseOwl.Casc.Encoding` (`Blte`, `EncodingTable`), `WiseOwl.Casc.Tvfs`
(`TvfsManifest`), `WiseOwl.Casc.Internal` (`Bytes`, `CascPathHash`).
These implement and are documented for completeness; a typical consumer
goes through `CascStorage` / `Diablo4Storage` instead.

## See also

- [`docs/casc-format.md`](../casc-format.md) — the canonical **byte-format**
  reference (CASC + Diablo IV layouts, correction log). The API docs say
  *what the types are*; the format spec says *what the bytes mean*.
- [`README.md`](../../README.md) — quickstart. [`samples/`](../../samples/)
  — a runnable console.
