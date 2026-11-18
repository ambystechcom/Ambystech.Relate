# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed
- Reorganized sources into `Database/`, `Results/`, `Values/`, `Errors/`, `Data/` and `Interop/`, one type per file. No public API or namespace changes.
- Split tests by feature; moved long README sections into `docs/`.
- Single version source in `Directory.Build.props`; `LICENSE` is now packed.

### Added
- `examples/TeamGraph` sample app.

## [0.1.0]

- Initial release: driver-style API and ADO.NET provider over Relate engine 1.0.2 (C ABI v2).
