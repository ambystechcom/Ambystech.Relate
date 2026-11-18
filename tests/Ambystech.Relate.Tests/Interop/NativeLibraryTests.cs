using Ambystech.Relate.Interop;

namespace Ambystech.Relate.Tests;

public class NativeLibraryTests
{
    [Fact]
    public void Native_library_exports_the_whole_v2_abi()
    {
        Assert.Equal(2, RelateDatabase.AbiVersion);

        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", NativeLoader.RuntimeIdentifier(), "native", NativeLoader.FileName());
        var lib = System.Runtime.InteropServices.NativeLibrary.Load(Environment.GetEnvironmentVariable("RELATE_LIB") ?? path);
        var imported = typeof(RelateNative)
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name.StartsWith("relate_", StringComparison.Ordinal))
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        Assert.Equal(81, imported.Count);
        foreach (var name in imported)
        {
            Assert.True(System.Runtime.InteropServices.NativeLibrary.TryGetExport(lib, name, out _), $"missing export {name}");
        }
    }
}
