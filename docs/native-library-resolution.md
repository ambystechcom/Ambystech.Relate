# Native library resolution

1. `RELATE_LIB`: an explicit path to `librelate_ffi.so`, `librelate_ffi.dylib` or `relate_ffi.dll`. This is the same escape hatch the upstream clients use.
2. `runtimes/<rid>/native/` next to the app, which is how the package delivers it.
3. The platform's default library search.

The client checks `relate_abi_version() == 2` on first use.

On Windows, the native engine needs the Visual C++ runtime (`VCRUNTIME140.dll`), which most machines already have. Linux binaries require glibc (no musl/Alpine build upstream).
