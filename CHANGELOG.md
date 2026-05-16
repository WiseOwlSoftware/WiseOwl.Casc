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

See [`docs/devlog/`](docs/devlog/) for the narrative of how each piece was
built and why.
