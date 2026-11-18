using System.Reflection;
using System.Runtime.InteropServices;

namespace Ambystech.Relate.Interop;

/// <summary>
/// Resolves <c>relate_ffi</c> and checks the ABI once per process.
/// </summary>
/// <remarks>
/// Order: the <c>RELATE_LIB</c> environment variable (an explicit path, the same escape
/// hatch the upstream Java and Go clients honour), then the RID-specific copy under
/// <c>runtimes/&lt;rid&gt;/native</c> next to this assembly, then the platform's default
/// probing (which also covers NuGet's deps.json resolution).
/// </remarks>
internal static class NativeLoader
{
    internal const string LibraryOverrideVariable = "RELATE_LIB";

    private static readonly object Gate = new();
    private static bool _initialized;

    internal static void EnsureLoaded()
    {
        if (Volatile.Read(ref _initialized))
        {
            return;
        }

        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(NativeLoader).Assembly, Resolve);
            }
            catch (InvalidOperationException)
            {
                // A resolver was already set for this assembly (e.g. by a host); keep it.
            }

            int abi;
            try
            {
                abi = RelateNative.relate_abi_version();
            }
            catch (DllNotFoundException e)
            {
                throw new RelateException(
                    $"Could not load the Relate native library ({FileName()}). Use the Ambystech.Relate package's bundled natives " +
                    $"for a supported RID (win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64), or set {LibraryOverrideVariable} to its path.",
                    e);
            }

            if (abi != RelateNative.ExpectedAbiVersion)
            {
                throw new RelateException(
                    $"Relate native ABI version {abi} is not supported; Ambystech.Relate targets ABI v{RelateNative.ExpectedAbiVersion}.");
            }

            Volatile.Write(ref _initialized, true);
        }
    }

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != RelateNative.Library)
        {
            return 0;
        }

        var overridePath = Environment.GetEnvironmentVariable(LibraryOverrideVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return NativeLibrary.Load(Path.GetFullPath(overridePath));
        }

        // AppContext.BaseDirectory also works for single-file apps (Assembly.Location is empty there).
        foreach (var candidate in new[]
                 {
                     Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeIdentifier(), "native", FileName()),
                     Path.Combine(AppContext.BaseDirectory, FileName()),
                 })
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var fallback) ? fallback : 0;
    }

    internal static string FileName() =>
        OperatingSystem.IsWindows() ? "relate_ffi.dll"
        : OperatingSystem.IsMacOS() ? "librelate_ffi.dylib"
        : "librelate_ffi.so";

    internal static string RuntimeIdentifier()
    {
        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var other => other.ToString().ToLowerInvariant(),
        };
        return $"{os}-{arch}";
    }
}
