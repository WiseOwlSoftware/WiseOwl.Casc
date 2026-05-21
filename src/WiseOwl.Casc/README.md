# WiseOwl.Casc

A modern **.NET library for Blizzard's CASC** content-storage
stack — the game-agnostic transport: **CASC / TACT / TVFS / BLTE**.

> **Unofficial.** Not affiliated with or endorsed by Blizzard Entertainment.
> World of Warcraft, Diablo, and related marks are trademarks of Blizzard
> Entertainment, Inc. Use only with your own legally-obtained game files.

## What it does

Opens a *local* Blizzard installation and resolves content by path or by
content/encoding key — `.build.info` → build config → local `.idx` index
→ archive envelope → BLTE decode → encoding table → the TVFS path tree.
Modern API: typed `ContentKey` / `EncodingKey` value types, async reads,
spans, `net8.0;net10.0`.

```csharp
using WiseOwl.Casc;

using var casc = CascStorage.OpenLocal(@"D:\Diablo IV");
Console.WriteLine(casc.Build.Version);
byte[] bytes = casc.ReadPath(@"Base\CoreTOC.dat");   // path → BLTE bytes
```

For Diablo IV (CoreTOC, SNO records, textures, localized strings) add the
companion package **`WiseOwl.Casc.Diablo4`**, which builds on this.

## Documentation

- Byte-format spec: `docs/casc-format.md`
- API reference & dev logs in the repository.

MIT licensed. See `LICENSE`, `NOTICE`, and `THIRD-PARTY.md` for credits to
the reverse-engineering community whose published format notes this
implementation builds upon.
