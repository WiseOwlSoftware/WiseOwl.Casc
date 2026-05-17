# Third-party references & credits

WiseOwl.Casc is a **clean-room** implementation: it is written from a
self-contained binary-format specification, and **no third-party source code
is copied or linked into the library or its packages**. The library has no
runtime dependency on any CASC/TACT library.

The publicly-documented CASC / TACT / TVFS / BLTE container formats and the
Diablo IV SNO / `.tex` formats were learned from, and cross-checked against,
the following permissively-licensed community projects and documentation. We
credit them with gratitude — this library would not exist without the
reverse-engineering community's prior work.

| Project | License | What we learned / verified against it |
|---|---|---|
| [WoW-Tools/CascLib](https://github.com/WoW-Tools/CascLib) (TOM_RUS) | MIT | CASC local-index (`.idx`), build/CDN config, BLTE frame decoding, TVFS root traversal, and the Diablo IV root (`CoreTOC.dat`, `CoreTOCSharedPayloadsMapping.dat` `0xABBA0003`) behaviour. The package-name collision with the unrelated `CascLib.NET` is the origin story of this library. |
| [Dakota628/d4parse](https://github.com/Dakota628/d4parse) | — | Diablo IV SNO record conventions and the `.tex` (`TextureDefinition`) mip/`SerTex` layout. |
| [DiabloTools/d4data](https://github.com/DiabloTools/d4data) | — | The `0x44CF00F5` combined-meta bundle (`Texture-Base-Global.dat`) container layout. |
| [HoldMyBeer-gg/rustydemon](https://github.com/HoldMyBeer-gg/rustydemon) | — | Diablo IV texture format cross-check. |
| [alkhdaniel/diablo-4-string-parser](https://github.com/alkhdaniel/diablo-4-string-parser) | — | Standalone Diablo IV `.stl` (StringList) layout cross-check (the consolidated per-locale bundle was reversed independently — see `docs/casc-diablo4-format.md §6.3`). |
| [wowdev.wiki](https://wowdev.wiki/TACT) | community | TACT / CASC / TVFS / BLTE protocol documentation. |

The byte-level specification this library implements is maintained in this
repository as two self-contained references — [`docs/casc-format.md`](docs/casc-format.md)
(CASC/TACT/TVFS/BLTE transport) and
[`docs/casc-diablo4-format.md`](docs/casc-diablo4-format.md) (the Diablo IV
SNO/container/record layer) — each with its own correction log.

> **Disclaimer.** Not affiliated with or endorsed by Blizzard Entertainment.
> World of Warcraft, Diablo, and related marks are trademarks of Blizzard
> Entertainment, Inc. Use only with your own legally-obtained game files.
