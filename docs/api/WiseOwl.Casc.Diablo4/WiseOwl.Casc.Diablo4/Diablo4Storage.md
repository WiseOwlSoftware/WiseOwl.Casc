# Diablo4Storage class

The Diablo IV game module: a thin, modern facade over a CascStorage that resolves content by SNO id through `CoreTOC.dat` and the TVFS file system, exposes the `0x44CF00F5` combined-meta texture catalog, and BLTE-decodes the result. The Diablo IV trademark appears only in this module, used nominatively as a compatibility descriptor.

```csharp
public sealed class Diablo4Storage : IDisposable
```

## Public Members

| name | description |
| --- | --- |
| static [Attach](Diablo4Storage/Attach.md)(…) | Wrap an already-opened CascStorage as a Diablo IV view (the caller keeps ownership of the storage). |
| static [Open](Diablo4Storage/Open.md)(…) | Open a local Diablo IV installation. |
| [Casc](Diablo4Storage/Casc.md) { get; } | The underlying game-agnostic CASC storage. |
| [CoreToc](Diablo4Storage/CoreToc.md) { get; } | The master SNO directory, parsed from `Base\CoreTOC.dat`. |
| [SharedPayloads](Diablo4Storage/SharedPayloads.md) { get; } | The shared-payload de-duplication mapping (`Base\CoreTOCSharedPayloadsMapping.dat`, magic `0xABBA0003`), parsed on first use. Empty if the file is absent. |
| [TextureMeta](Diablo4Storage/TextureMeta.md) { get; } | The combined-meta texture catalog (`Base\Texture-Base-Global.dat`, magic `0x44CF00F5`), parsed on first use. |
| [Dispose](Diablo4Storage/Dispose.md)() |  |
| [GetStrings](Diablo4Storage/GetStrings.md)(…) | The per-locale localized-string catalog (`base/StringList-Text-<locale>.dat`), parsed and cached on first use. *locale* is a D4 locale code such as `enUS`, `deDE`, `frFR`, `esES`, `esMX`, `itIT`, `jaJP`, `koKR`, `plPL`, `ptBR`, `ruRU`, `trTR`, `zhCN`, `zhTW`. |
| [OpenSno](Diablo4Storage/OpenSno.md)(…) | Resolve and open a SNO by id as a decoded stream. |
| [ReadAttributeFormulas](Diablo4Storage/ReadAttributeFormulas.md)(…) | Read + decode the GameBalance [`AttributeFormulaTable`](./AttributeFormulaTable.md) (default SNO `201912`, the paragon formula table). Returns formula text + name/GBID indices only — evaluation and the calibrated intrinsics stay with the consumer. |
| [ReadGroup](Diablo4Storage/ReadGroup.md)(…) | Stream every SNO in a group as `(id, bytes)`, skipping ids that legitimately have no content in *folder*. The local index, encoding table and archive handles stay resident, so sweeping a large group (Affix/Power are thousands of records) does not re-open storage. |
| [ReadParagonBoard](Diablo4Storage/ReadParagonBoard.md)(…) | Read + decode a [`ParagonBoardDefinition`](./ParagonBoardDefinition.md) by SNO id (group 108). |
| [ReadParagonBoardName](Diablo4Storage/ReadParagonBoardName.md)(…) | Resolve a `ParagonBoard`'s localized display name, throwing if it cannot be resolved. See [`TryReadParagonBoardName`](./Diablo4Storage/TryReadParagonBoardName.md) for the convention, the boundary (raw value only), and the no-fallback note; prefer that overload when the consumer owns an unknown-name fallback. |
| [ReadParagonGlyph](Diablo4Storage/ReadParagonGlyph.md)(…) | Read + decode a [`ParagonGlyphDefinition`](./ParagonGlyphDefinition.md) by SNO id (group 111). |
| [ReadParagonGlyphAffix](Diablo4Storage/ReadParagonGlyphAffix.md)(…) | Read + decode a [`ParagonGlyphAffixDefinition`](./ParagonGlyphAffixDefinition.md) by SNO id (group 112). |
| [ReadParagonNode](Diablo4Storage/ReadParagonNode.md)(…) | Read + decode a [`ParagonNodeDefinition`](./ParagonNodeDefinition.md) by SNO id (group 106). |
| [ReadParagonRenderLayout](Diablo4Storage/ReadParagonRenderLayout.md)() | The typed paragon-board render projection (FR-C7) over the generic [`ReadUiScene`](./Diablo4Storage/ReadUiScene.md) decode of `ParagonBoard` (SNO 657304). Raw decoded geometry only; the absolute resolution/zoom scale is permanently the consumer's. See [`ParagonRenderLayout`](./ParagonRenderLayout.md) for the staged-delivery contract (`Ratios.Provisional`; the 18-row state matrix is filled as the per-state assembly is decode-proven — no fabricated rows). |
| [ReadSno](Diablo4Storage/ReadSno.md)(…) | Resolve and BLTE-read a SNO by id (the [`SnoGroup`](./SnoGroup.md) documents intent; the TVFS address is id-only). For Payload, an empty/absent direct payload is transparently resolved through the shared-payload mapping. (2 methods) |
| [ReadSnoAsync](Diablo4Storage/ReadSnoAsync.md)(…) | Asynchronously resolve and read a SNO by id. |
| [ReadUiScene](Diablo4Storage/ReadUiScene.md)(…) | Read + decode a Diablo IV UI-scene SNO (group [`Group`](./UiScene/Group.md) = 46, format hash [`FormatHash`](./UiScene/FormatHash.md)) into its raw widget graph. Generic surface (any `0xE4825AB8` SNO, e.g. `ParagonBoard` 657304); raw fields only, no layout/imaging/policy — see [`UiScene`](./UiScene.md). |
| [TryGetIconFrame](Diablo4Storage/TryGetIconFrame.md)(…) | Resolve a node icon handle ([`HIconMask`](./ParagonNodeDefinition/HIconMask.md) or [`HIcon`](./ParagonNodeDefinition/HIcon.md)) to the atlas SNO and [`TexFrame`](./TexFrame.md) that carry it — the first-party node↔icon link (`hIconMask == TexFrame.ImageHandle`). The handle→frame index is built once from [`TextureMeta`](./Diablo4Storage/TextureMeta.md) on first use. |
| [TryGetSharedPayloadSource](Diablo4Storage/TryGetSharedPayloadSource.md)(…) | True if *id*'s payload is physically stored under another SNO; *sourceId* is that holder. |
| [TryGetString](Diablo4Storage/TryGetString.md)(…) | Resolve a label within a known StringList table (SNO) to its localized text. Prefer this — labels are unique only within a table. (2 methods) |
| [TryReadParagonBoardName](Diablo4Storage/TryReadParagonBoardName.md)(…) | Resolve a `ParagonBoard`'s localized display name — the in-game board name ("Start", "Dynamism", "Pyrosis", …) — for a locale. |
| [TryReadSno](Diablo4Storage/TryReadSno.md)(…) | Try to resolve and BLTE-read a SNO by id. Returns `false` (no throw) when the SNO legitimately has no such content — the common "skip the art-less node" case. (2 methods) |
| const [DefaultLocale](Diablo4Storage/DefaultLocale.md) | The default locale (the one most installs ship enabled). |
| const [ProductCode](Diablo4Storage/ProductCode.md) | The Diablo IV TACT product code. |
| static [ExtensionFor](Diablo4Storage/ExtensionFor.md)(…) | The Diablo IV SNO-group → file-extension table (factual data, matching the current build). Unknown groups fall back to the numeric `.NNN` form the game uses. |
| static [OpenAsync](Diablo4Storage/OpenAsync.md)(…) | Open a local Diablo IV installation asynchronously. |
| static [SnoPath](Diablo4Storage/SnoPath.md)(…) | The TVFS path a SNO resolves through: `<prefix>\<Folder>\<id>` (a child sub-id appends `-<subId>`). Verified empirically against the live build: Diablo IV addresses SNO content in TVFS by the numeric id — not by a `<group>\<name><ext>` name path and not by the `base:meta\<id>` colon form. |

## Remarks

A SNO is addressed by the path `<prefix>\<Folder>\<groupId>\<name><ext>` (prefix defaults to `Base`), hashed and resolved through TVFS — the same scheme the game uses. The [`CoreToc`](./Diablo4Storage/CoreToc.md) supplies the name, group and extension.

## See Also

* namespace [WiseOwl.Casc.Diablo4](../WiseOwl.Casc.Diablo4.md)
* [Diablo4Storage.cs](https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/Diablo4Storage.cs)

<!-- DO NOT EDIT: generated by xmldocmd for WiseOwl.Casc.Diablo4.dll -->
