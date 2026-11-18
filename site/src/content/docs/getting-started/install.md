---
title: Install
description: Add Ambystech.Relate to a .NET project.
---

```bash
dotnet add package Ambystech.Relate
```

The package targets `net8.0` and `net10.0` and bundles the native Relate engine for `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`. There is nothing else to install.

| Package | Relate engine | C ABI |
| --- | --- | --- |
| 1.0.x | 1.0.2 | v2 |

Linux binaries require glibc (there is no musl/Alpine build upstream), and on Windows the engine needs the Visual C++ runtime. See [Native library resolution](/reference/native-library-resolution/) for how the library is located and for the `RELATE_LIB` override.

## Building from source

```bash
scripts/fetch-natives.sh 1.0.2      # stages the five natives into ./natives (from the official npm platform packages)
dotnet build -c Release
dotnet test -c Release
dotnet pack src/Ambystech.Relate.csproj -c Release -o artifacts
```
