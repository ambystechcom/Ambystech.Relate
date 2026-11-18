# Third-party notices

Ambystech.Relate bundles the native Relate engine (`librelate_ffi` / `relate_ffi.dll`) under
`runtimes/<rid>/native/`.

## Relate by Neo4j

- Source: https://github.com/relatedb/relate
- Version: 1.0.2 (C ABI v2)
- License: Apache License 2.0
- Copyright (c) Neo4j, Inc.

The binaries are the upstream release builds published in the `@relate/relate-<platform>`
npm packages. They are redistributed unmodified. The engine statically links third-party
Rust crates whose licenses are listed in the upstream repository's `THIRDPARTY-NOTICES.md`.

Relate and Neo4j are trademarks of Neo4j, Inc. This package is an independent community
client and is not affiliated with or endorsed by Neo4j, Inc.
