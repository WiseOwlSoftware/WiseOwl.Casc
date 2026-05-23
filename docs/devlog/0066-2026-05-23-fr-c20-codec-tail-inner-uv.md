# 0066 — FR-C20 #32 codec tail: per-frame inner UV rect

2026-05-23 · CL-71 · branch `fr-c20-codec-tail-inner-uv`

## Trigger

The owner's 2026-05-22 directive on `casc-fr#32` named **three deferred
extras** for CASC to investigate:

1. Power → class facet (via skill-kit RE).
2. Item `NameConvention` facets.
3. **Codec tail** — *"whatever decode-tail bytes remain on
   `TextureMeta`/`TextureDefinition` that fell out of P2's codec
   disclosure"*.

This devlog covers (3). The other two are tracked separately; if either
turns out larger than expected per the §3 protocol it'll split into a
fresh issue.

## Recon

`build/SnoScan texdump <sno>` (new) dumps the raw bundle entry bytes
for one texture (the format I'd been opaquely calling "the codec
tail"). A walk over a paragon atlas + a UI selection atlas surfaced
the **per-frame** decode gap: a `TexFrame` is 36 bytes on disk, of
which CL-55 surfaced the first 20 (handle + outer UV rect at
`+4..+19`). The remaining 16 bytes at `+20..+35` look like four
floats — and across both probes, the four floats *exactly equalled*
the primary UV. Quick hypothesis: a redundant duplicate.

That was wrong. `build/SnoScan framescan` (new, scans every
combined-meta entry) reported:

```
scanned 140197 TextureDefinitions with frames;
24082 atlases with tail != primary UV
(42877 divergent frames)
```

So ~17 % of atlases author a real distinct tail. `build/SnoScan
framediv` (new) printed primary vs tail UV side by side on a sample
of divergent atlases. Concrete examples:

| Atlas (size)              | Primary UV (outer)                 | Tail UV (inner)                   | Inset |
|---                        |---                                 |---                                |---|
| `2DUIBreathMeter` (224×144) | (0.0179, 0.0278) - (0.9821, 0.2847) | (0.0357, 0.0625) - (0.9643, 0.2569) | ~4 px each side |
| `2DUIConfirmation` (352×184) | (0.0227, 0.8696) - (0.9716, 0.9565) | (0.0284, 0.8696) - (0.9659, 0.9457) | ~2 px each side |
| `2DUIChatBlackCorners` (144×184) | (0.5278, 0.4565) - (0.9722, 0.6304) | (0.5278, 0.4565) - (0.5278, 0.4565) | degenerate point |
| `2DUIConsoleScrollbar` (24×104) | (0, 0) - (0.75, 0.9519) | (0.0417, 0.0192) - (0.75, 0.9327) | 1-px inset |

The tail is an **inner / 9-slice-middle UV rect**:

- ~83 % of frames: inner ≡ outer (the simple "outer is the content"
  case — no trim, no 9-slice middle).
- ~17 %: inner is a few-pixel inset from outer (sprite trim for
  tighter UV sampling, or the 9-slice authored centre for stretchable
  UI tiles).
- A small remainder: inner collapsed to a single point at the
  outer's top-left (`InnerU0 == InnerU1`, `InnerV0 == InnerV1`) — the
  engine's "no authored inner rect" sentinel.

## What ships

`TexFrame` gains four `InnerU0/InnerV0/InnerU1/InnerV1` float fields,
plus two helpers — `InnerPixelRect(width, height)` and a boolean
`HasDistinctInner`:

```csharp
public readonly record struct TexFrame(
    uint ImageHandle,
    float U0, float V0, float U1, float V1,
    float InnerU0, float InnerV0, float InnerU1, float InnerV1)
{
    public (int X, int Y, int Width, int Height) PixelRect(int w, int h);
    public (int X, int Y, int Width, int Height) InnerPixelRect(int w, int h);
    public bool HasDistinctInner;
}
```

`PixelRect` / `U0`/`V0`/`U1`/`V1` keep their existing semantic (the
outer / full sprite rect). `InnerPixelRect` returns the same pixel
rect when inner ≡ outer (the common case); for degenerate-point
authoring it floors width/height at `1` to keep the rect non-empty —
consumers detect "no authored inner" via `HasDistinctInner`.

`TextureDefinition.ParseFromBundle` now reads all eight floats per
frame from the 36-byte slot (instead of dropping the trailing 16
bytes).

## Boundary

Inside the FR-C20 catalog surface. The Optimizer didn't ask for the
inner UV specifically — the owner asked CASC to swing at the
remaining decode tail — but the data is now there for any consumer
that wants to render 9-slice tiles or sample sprite-trim rects more
tightly than the outer rect allows. Existing consumers see no
behavioural change.

## Tests

The CL-71 assertion extends `Acceptance_matrix_against_live_install`:

- Iterates every `TextureMeta.BySno` frame; asserts every
  `InnerPixelRect` is non-empty (width/height ≥ 1) — the degenerate
  case must still resolve to a 1×1 pixel rect, not a 0-sized
  rectangle that would crash a sampler.
- Stops at the first frame with `HasDistinctInner == true` and
  asserts it was found — confirms the field is being read from the
  bundle, not just defaulted from the outer rect.

92/92 tests green on build `3.0.2.71886`.

## Recon tooling added

`build/SnoScan` (committed alongside the next CL or this one):

- `texdump <sno> [offset] [len]` — payload-relative hex/u32/f32 dump
  for one TextureDefinition's bundle entry, with the known field
  offsets annotated.
- `frametail <sno> [maxFrames]` — per-frame primary UV + tail bytes
  (hex + float) for one atlas.
- `framescan` — global count of divergent vs equal tail across every
  texture in the combined-meta.
- `framediv [max]` — sample of divergent atlases with primary vs
  tail UV side by side, for spot-checking the structural
  interpretation.

## What's next on #32

- **Power → class facet** — concrete consumer ask (filter powers by
  class without hardcoding). Needs first-party skill-kit RE; no
  cheap source from `PowerDefinition` / `PlayerClass` / names. Next
  CL candidate.
- **Item `NameConvention` facets** — exposes the localized-name
  composition data missing from current `Find(Item)` results.
