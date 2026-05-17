# WiseOwl.Casc

A modern, unified, thoroughly-documented .NET library for **Blizzard's CASC**
content-storage stack — and the per-game layers built on top of it. Diablo IV
is the first fully-supported game module.

> **Unofficial.** Not affiliated with or endorsed by Blizzard Entertainment.
> World of Warcraft, Diablo, and related marks are trademarks of Blizzard
> Entertainment, Inc. Use only with your own legally-obtained game files.

---

## What is CASC?

**CASC** (Content Addressable Storage Container) is the on-disk content system
Blizzard ships with modern games — World of Warcraft, Diablo IV, Overwatch,
Diablo II: Resurrected, and others. It replaced the old MPQ archives. Content
is addressed by hash, not by path:

- A file's bytes have a **content key** (CKey, an MD5 of the content).
- The stored, compressed-and-maybe-encrypted form has an **encoding key**
  (EKey). **BLTE** is the chunked container/codec for that stored form.
- **TACT** is the manifest/transport layer (build & CDN configs, the
  *encoding* table mapping CKey↔EKey, the *root* that maps game-meaningful
  names to CKeys).
- **TVFS** (TACT Virtual File System) is the path→content tree used by newer
  titles, including Diablo IV.

Only the top *root* layer is game-specific. Diablo IV addresses content by a
numeric **SNO** id resolved through `CoreTOC.dat`, with texture pixels
de-duplicated via a shared-payload mapping and texture metadata consolidated
into a single `0x44CF00F5` combined-meta bundle.

The ecosystem of existing tools is fragmented, frequently stale against the
current game build, and thinly documented. WiseOwl.Casc is a clean-room
redesign: one modern API, multi-game by construction, documented to the byte.

## Packages

| Package | Purpose |
|---|---|
| `WiseOwl.Casc` | Game-agnostic CASC/TACT/TVFS/BLTE transport. Zero Blizzard trademark exposure. |
| `WiseOwl.Casc.Diablo4` | Diablo IV module: `CoreTOC`, SNO records, `.tex` decode, paragon helpers. |
| _(planned)_ `WiseOwl.Casc.Wow` / `.Overwatch` / `.D2R` | Additional game roots — the core is designed so these are clean additions. |

Published under the **reserved, verified `WiseOwl.*` NuGet prefix** — which is
precisely what makes a one-character-off impostor package impossible.

## Quickstart

```csharp
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;

// Open a local Diablo IV install (product code "fenris").
await using var d4 = await Diablo4Storage.OpenAsync(@"D:\Diablo IV");

// Resolve + BLTE-read a SNO record by id (Base : Meta is the default folder).
await using Stream meta = await d4.OpenSnoAsync(SnoGroup.ParagonNode, 678776);

// CoreTOC.dat is the master directory: every (group, id) -> name.
foreach (var e in d4.CoreToc.EntriesInGroup(SnoGroup.ParagonBoard))
    Console.WriteLine($"{e.Id}  {e.Name}");
```

See [`samples/`](samples/) for a runnable console.

## Documentation

- **[API reference](docs/api/)** — complete, generated per-type/per-member
  docs for both packages ([WiseOwl.Casc](docs/api/WiseOwl.Casc/WiseOwl.Casc.md),
  [WiseOwl.Casc.Diablo4](docs/api/WiseOwl.Casc.Diablo4/WiseOwl.Casc.Diablo4.md)).
- **Byte-format specs** — the canonical binary-layout references (each
  with its own correction log), mirroring the two packages:
  [`casc-format.md`](docs/casc-format.md) (CASC/TACT/TVFS/BLTE transport)
  and [`casc-diablo4-format.md`](docs/casc-diablo4-format.md) (Diablo IV
  SNO/container/record layer).
- **[Dev logs](docs/devlog/)** — how each piece was built and why.

## Status

Early (`0.1.0-alpha`). See [`CHANGELOG.md`](CHANGELOG.md),
[`docs/devlog/`](docs/devlog/), and [`docs/resume-prompt.md`](docs/resume-prompt.md)
for exactly what is working vs. stubbed.

## Building

Requires the **.NET 10 SDK**. The core multi-targets
`netstandard2.0;net8.0;net10.0`.

```
dotnet build WiseOwl.Casc.slnx
dotnet test  WiseOwl.Casc.slnx
```

No Blizzard game bytes are committed to this repository. Tests use synthetic
fixtures; integration tests that touch a real install are skipped unless one is
present.

## License

MIT — see [`LICENSE`](LICENSE), [`NOTICE`](NOTICE), and
[`THIRD-PARTY.md`](THIRD-PARTY.md) (credits to the reverse-engineering
community whose published format notes this clean-room implementation builds
upon).
