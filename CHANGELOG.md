# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); this project uses
Semantic Versioning once it reaches `1.0.0`.

## [Unreleased]

### Added
- Initial repository scaffold: `.slnx` solution, `WiseOwl.Casc` (multi-TFM
  core), `WiseOwl.Casc.Diablo4`, test projects, runnable sample, CI.
- Clean-room **Diablo IV byte parsers**: `SnoGroup`, `DataType` primitive
  decoders, `SnoFile` reader (16-byte header + payload base `0x10`),
  `CoreToc` (new-format `0xBCDE6611` header), and the `0x44CF00F5`
  combined-meta bundle / `TextureDefinition` parser — proven against a real
  extracted `CoreTOC.dat`.
- Clean-room **CASC transport** foundation in `WiseOwl.Casc`: modern public
  API surface (`CascStorage`, `ContentKey`/`EncodingKey`, async streams),
  `.build.info` / build-config parsing, local `.idx` index, BLTE decoder.

### Proven (vs live Diablo IV `3.0.2.71886`)
- Full lower transport end to end: `.build.info` → build config →
  16-bucket local `.idx` → archive envelope → BLTE (real ~100 MB
  multi-chunk `encoding`) → encoding table → closed-loop CKey→EKey→index.
- Clean-room **TVFS** resolves + reads `Base\CoreTOC.dat` and
  `Base\Texture-Base-Global.dat`.
- **CoreTOC `0xBCDE6611`**: 849,257 SNOs / 181 groups (stock CascLib NuGet
  overflows on this file; this parser does not).
- **Combined-meta `0x44CF00F5`**: 140,197 texture definitions;
  `2DUI_ParagonNodes` → BC3 4224×192, 31 ptFrames.
- 12 tests pass, 1 documented skip; solution builds with 0 warnings.

### Known gap
- Per-SNO read by id (`Diablo4Storage.ReadSno`) — the deep nested-`vfs-N`
  TVFS subtree + the `0xABBA0003` shared-payload layer. Top-level
  `Base\*.dat` resolve; per-SNO records do not yet. The test self-skips
  with a precise reason rather than report a false pass. See
  `docs/casc-format.md` (correction log, "OPEN") and `docs/devlog/0001`.

See [`docs/devlog/`](docs/devlog/) for the narrative of how each piece was
built and why, and [`docs/casc-format.md`](docs/casc-format.md) for the
self-contained transport spec.
