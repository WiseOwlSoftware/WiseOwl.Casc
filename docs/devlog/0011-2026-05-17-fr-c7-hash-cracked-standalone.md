# 0011 — 2026-05-17 — FR-C7: the D4 hash, cracked clean-room

> Narrative source for the wiseowl.com session. Continues 0010.

The deep dive into the paragon UI-scene format (`0xE4825AB8`) had
reached an honest wall: the format was fully *modelled* — a
reflection-serialised, hash-addressed widget graph — but every field was
an opaque 28-bit id, and a broad test of eight standard hashes
(GbidHash, FNV-1a, DJB2, SDBM, CRC32, lookup3 …) matched **none** of the
structural constants. The discipline held: nothing was guessed, and the
spec said so plainly.

The owner pushed — *"determine the decoded data semantics"*, then
*"are the words not discoverable from the game data?"* Both questions
turned out to be the key. The honest answer to the second reframed
everything: the field **names are absent from the SNO data by design**
(the format is hash-keyed — one-way), but a serialization system needs
those names *somewhere*, and that somewhere is the **client binary's
reflection registry**. The community's wordlists are not guesses; they
are strings lifted from the executable.

The crack came from reading the public d4data `parse.js` for the
*method* (algorithm facts, no code taken): D4's three hashes are all the
DJB2 core `h = h*33 + ch` but seeded **0**, not the textbook **5381** —
which is exactly why a seed-5381 DJB2 test missed. `fieldHash` masks to
28 bits, `typeHash` is the full word, `gbidHash` lowercases first (and
is, pleasingly, our existing `Diablo4.GbidHash` — the family unified).
Implemented, then **self-verified** against a number we already trusted:
`gbidHash("ParagonNodeCoreStat_Normal") = 0x42C16A1B`, the project's
own known-good GBID. Green.

Then the clean-room payoff: string-extract the owner's *own*,
legally-installed `Diablo IV.exe` (in-tool, never into context — the
same constraint as the brand-art binaries), hash 285k identifiers, match
the observed ids. The opaque format dissolved. `0x1332C78D` — the
"mysterious separator" chased for several passes — is
`typeHash("DT_BINDABLEPROPERTY")`: every widget field is a *bindable
property*. `0xA4C42E02` is `DT_INT`. And the geometry fell out by name:
the widget rect is `nLeft / nRight / nTop / nBottom / nWidth / nHeight`
(DT_INT, bindable), with `rgbaTint` (DT_RGBACOLOR) confirming — by its
mere named existence — the §2.3 shader-tint answer, and five `DT_SNO`
fields carrying the texture bindings.

Two through-lines worth keeping. First, the honesty discipline paid
*forward*: because no fake pitch was ever asserted, the eventual
decoded schema slots into a spec with no wrong claims to retract — the
"located + structured, numbers pending" stance was the thing that made a
clean finish possible. Second, this stopped being only an FR-C7 result:
the D4 hash + the binary-extraction method are now a permanent,
first-party, dependency-free capability of the library — any future D4
SNO meta format is now nameable from the user's own install, no
third-party JSON. §10 of `casc-diablo4-format.md` was rewritten from a
discovery log into an authoritative reference to reflect that.

What remains is small and well-defined: the rect fields are *bindable*,
so their per-widget values live in the instance-data section; read them,
reproduce the 67.7 px/grid anchor at the stated provenance, emit the
normalised `ParagonRenderLayout`. No number until it reproduces the
anchor — same rule as the whole round.
