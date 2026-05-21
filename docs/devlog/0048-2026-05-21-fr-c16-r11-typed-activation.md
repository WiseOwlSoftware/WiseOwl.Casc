# Devlog 0048 — FR-C16 R11: typed per-layer activation surface

*2026-05-21*

## Decision

Owner directive: build the naming-convention activation surface now;
follow with EXE RE; evaluate a brute-force symbol-ID process (GPU optional)
— but prefer EXE symbols as the cracking source if available.

R10 established (devlog 0047) that scene 657304 stores no activation
condition: the engine binds widget visibility to runtime state **by name**
in its C++ UI controller. R11 turns that naming convention into a typed,
evaluable surface so the consumer authors zero dispatch.

## Shipped (CL-51)

- `NodeFact` — closed runtime-fact vocabulary (Selected/Unselected,
  Purchased/Purchasable/Revealed/Located/Socketed/Equipped/SearchMatch/
  Tutorial, NeighbourPurchasable[T/R/B/L], Rarity{Magic,Rare,Legendary},
  Type{Socket,Gate,Start}, Pressed/MouseOver/Disabled, Always/Never).
- `NodeActivation(AllOf, Source)` + `Evaluate(IReadOnlySet<NodeFact>)` —
  AND-of-facts; the consumer supplies its computed facts.
- `NodeActivationSource` — `NameConvention` / `EngineBehavior` /
  `SceneField` provenance marker (the widget-name-≠-role honesty line).
- `ParagonNodeRecipeLayer` gains `Activation` + `Slot` (`NodeSlot`);
  `NodeDiscLayer` gains `Activation`.
- The base-disc family (grey + per-rarity + per-type) share
  `NodeSlot.BaseDisc` → mutually-exclusive variants; the gate composite
  splits its ornate by selection (`0xC2DF4786`/`0x0E6B6249`) and gates the
  locator (`0x6D68F45F`) on `Located`.

Cracked this round (per-state field-name family): `hImageFramePressed`
`0x0D75128C`, `hImageFrameMouseOver` `0x0B63D29B`, `hImageFrameDisable`
`0x0DAEFCAA`, `hImageFrameIcon` `0x02330CBF`, `hText` `0x0789C1CD`,
`rgbaTint` `0x09A3F17B` (added to `Diablo4.KnownFieldNames` /
`d4-hash-dictionary.md`).

58/58 tests green on build `3.0.2.71886`. Recon: `build/SnoScan recdump`,
`snorefs`.

## Provenance discipline

Most node-state activations are `EngineBehavior` (the name is suggestive,
the fact inferred) or `NameConvention` (the name literally spells the
state). None is `SceneField` — there is no scene field to read. This is the
honest boundary: CASC owns the engine-knowledge table so the consumer
doesn't, marked so a future EXE-RE pass (which could upgrade entries to a
verified source) is clean.

## Next

EXE RE of `Diablo IV.exe`: extract the identifier/symbol table, hash with
D4 TypeHash/FieldHash, crack remaining unknown UI field hashes, and search
for the UI-controller name→state binding table. Brute force (GPU-capable)
is the fallback only if the EXE doesn't carry the names.
