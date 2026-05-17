# WiseOwl.Casc.Diablo4

The **Diablo IV** game module for [`WiseOwl.Casc`](https://www.nuget.org/packages/WiseOwl.Casc) —
clean-room, modern .NET. CoreTOC, SNO read-by-id, shared-payload
resolution, the `0x44CF00F5` combined-meta family (textures + the
per-locale StringList catalog), BC1/BC3 decode, and typed paragon /
GameBalance record decoders.

> **Unofficial.** Not affiliated with or endorsed by Blizzard
> Entertainment. Diablo and Diablo IV are trademarks of Blizzard
> Entertainment, Inc.; the name is used here only nominatively, to
> describe the file formats this module is compatible with. Use only with
> your own legally-obtained game files.

## What it does

```csharp
using WiseOwl.Casc.Diablo4;

using var d4 = Diablo4Storage.Open(@"D:\Diablo IV");

// SNO read by id; CoreTOC name↔id↔group.
byte[] meta = d4.ReadSno(SnoGroup.ParagonNode, 678776);

// Typed record decoders (raw fields only).
var board = d4.ReadParagonBoard(2458674);            // Width, Cells…
var gb    = d4.ReadAttributeFormulas();              // name → formula text

// Localized strings + textures.
d4.TryGetString(4087, "ChatLink_WhisperedTo", out var s);
var td = d4.TextureMeta.Get(1208406);                // BC3 atlas meta
```

**Boundary:** the library decodes **raw fields only** — it ships no
formula evaluator and performs no scoring; interpretation/policy stays
with the consumer. References `WiseOwl.Casc` for the transport.

## Documentation

- Byte-format spec: `docs/casc-diablo4-format.md` (and `docs/casc-format.md`
  for the underlying transport).
- API reference & dev logs in the repository.

MIT licensed. Credits in `THIRD-PARTY.md`.
