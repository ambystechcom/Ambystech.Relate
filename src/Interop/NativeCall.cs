using System.Runtime.InteropServices;

namespace Ambystech.Relate.Interop;

internal static class NativeCall
{
    /// <summary>Throw the classified engine error for a <c>RELATE_ERROR</c> status, freeing its error_json.</summary>
    internal static void Check(int status, nint errorJson, string operation)
    {
        if (status == RelateNative.RELATE_OK)
        {
            NativeStrings.TakeOwned(errorJson);
            return;
        }

        throw RelateException.FromErrorJson(NativeStrings.TakeOwned(errorJson), operation);
    }

    /// <summary>
    /// Keep <paramref name="handle"/> alive for the duration of a native call, so a concurrent
    /// Dispose cannot free it mid-call. Throws <see cref="ObjectDisposedException"/> once closed.
    /// </summary>
    internal static Lease Borrow(SafeHandle handle, string owner) => new(handle, owner);

    internal readonly ref struct Lease
    {
        private readonly SafeHandle _handle;
        private readonly bool _added;

        public Lease(SafeHandle handle, string owner)
        {
            _handle = handle;
            _added = false;
            if (handle.IsClosed)
            {
                throw new ObjectDisposedException(owner);
            }

            handle.DangerousAddRef(ref _added);
            Value = handle.DangerousGetHandle();
        }

        public nint Value { get; }

        public void Dispose()
        {
            if (_added)
            {
                _handle.DangerousRelease();
            }
        }
    }
}
