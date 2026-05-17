# 0007 — 2026-05-16 — NuGet packaging, and an honest icon blocker

> Narrative source for the wiseowl.com session. Continues 0006.

Stood up real NuGet packaging for both shippable libraries, modelled on
the Wise Owl house style observed in the Demeanor project (`WiseOwl.*`
ids, `icon.png` packed from `assets/icons/`, README/LICENSE/CHANGELOG
bundled, deterministic/CI, dynamic-year copyright). Both produce
`.nupkg` + symbol `.snupkg` with per-TFM assemblies and their XML docs,
a per-package README that renders on nuget.org, the MIT expression,
Source Link, and correct dependency groups (the Diablo4 package depends
on the transport package; the ns2.0 polyfills are declared). Verified by
unpacking and inspecting the nuspec and payload.

Icons: a reproducible pipeline mirroring the api-docs one — SVG sources
are the truth, a tiny in-repo `build/IconGen` (Svg.Skia) rasterises a
committed PNG size ladder, regenerated via `scripts/gen-icons.*`. The
**Diablo4** mark landed as designed: a bold red `D` with an overlaid,
readable Roman `IV` (ox-blood→ember), on the shared dark tile, with a
thin autumn-brown keyline as the Wise Owl family cue — Diablo-evocative
with zero Blizzard art (our own lettermark; trademark-safe).

The honest beat: the **WiseOwl.Casc owl** is not done. The owner's
preferred mark is a specific hand-drawn horned-owl sketch. A
hand-authored SVG interpretation was attempted and, correctly, rejected
by the owner as not faithful. Faithfully reproducing a real drawing is
an *image-tracing* problem, not a "draw it from memory" one — and the
right tools (potrace/autotrace/inkscape) aren't installed and, more
fundamentally, the pasted image isn't on disk for any tool to consume.
Rather than ship a pretty-but-wrong placeholder pretending to be the
brand, the state is recorded plainly: the Diablo4 icon is final; the
Casc owl is a clearly-labelled provisional placeholder pending the
source file. (Through-line of this project: don't fake a pass — here,
don't fake the brand.)

Plan once the source image is provided (`e:\tmp\wiseowl-owl.*`):
isolate the ink line-art from the parchment by luminance threshold →
alpha (the owner's "ignore the background"), recolour the kept strokes
to autumn medium brown, composite on the dark brand tile, and emit the
size ladder through the existing pipeline — pixel-faithful to the actual
drawing. Optionally also potrace it to a true vector SVG if a crisp
scalable mark is wanted. Packaging is already wired to the icon path, so
this is a drop-in regenerate + re-pack.

## Resolution

The owner supplied `assets/Owl.jpg` (a phone photo of the small original
business card; the original art was lost). Built `build/OwlTrace`:
SkiaSharp EXIF-orients + downscales + Otsu-thresholds the photo;
connected-component cleanup drops paper specks, the card/desk background,
and the two wide-flat bottom rules (the owner said those aren't the owl);
a morphological *close* bridges the faded-ink stroke dropouts, an *open*
de-jags; then the managed potrace (`BitmapToVector`) traces smooth filled
curves → a calligraphic SVG, recoloured autumn medium brown on the brand
tile. The big photo is processed entirely inside the tool — it never
enters the assistant context (an explicit owner constraint, and good
practice for a multi-megapixel input). Iterated `close`/`turd`/`alpha`
against the small rendered output only. Result is a faithful vector of
the owner's *own* drawing; being vector it scales cleanly.

A judgement call worth recording: the owner floated a fallback —
"find an actual horned-owl photo in a similar pose and reduce it to a
calligraphic drawing." Declined for the shipped brand icon: embedding a
third-party copyrighted photograph into a distributed NuGet package mark
is exactly the IP exposure this clean-room, MIT project exists to avoid,
and it was unnecessary since the owner's own art vectorised well. The
IP-safe alternative offered instead: clean-room stylised detailing
informed by *general horned-owl anatomy* (anatomy isn't copyrightable; a
specific photo is) — on request, not by default. Same through-line as the
rest of the project: don't fake the brand, and don't borrow someone
else's IP to dress it up. Packaging now ships the authentic mark; the
"placeholder" status is closed.

Final structure: the lone owl is the **organisation** mark
(`wiseowl-org`, for the nuget.org org profile), not a package icon. Each
package gets its own subject mark on the shared dark tile: the
**`WiseOwl.Casc`** icon is a bold "CASC" 2×2 lettermark (typeface
outlines baked to vector via `build/Lettermark` — no runtime font dep),
and **`WiseOwl.Casc.Diablo4`** its structural sibling, the red "D·IV"
lettermark; both carry the autumn-brown family keyline.

IP-safe detailing test (owner-requested): auto-detecting the eye blobs
from the photo proved unreliable (it mis-picked a large loop and
gutted the owl). The robust fix is *anatomy-driven, not photo-driven*:
place the spectacles from general great-horned-owl facial proportions
(eyes large, high in the facial disc, ~one eye-width apart) — fact, not
a copyrighted image — as deterministic geometry over the owner's own
trace. `OwlTrace eyeAnat=true` composites a clean rim + iris + pupil +
catchlight at those proportions; `wiseowl-org-spectacled.svg` is the
result and reads as a wise owl in spectacles without touching the
accepted mark. Good demonstration that "use the characteristics, not
the image" is both IP-clean and technically the more reliable route.
