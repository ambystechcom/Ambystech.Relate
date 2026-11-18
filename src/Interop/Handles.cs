using System.Runtime.InteropServices;

namespace Ambystech.Relate.Interop;

/// <summary>An open store (<c>relate_database</c>). May be shared across threads.</summary>
internal sealed class DatabaseHandle : SafeHandle
{
    public DatabaseHandle() : base(0, ownsHandle: true) { }

    internal DatabaseHandle(nint handle) : this() => SetHandle(handle);

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle() => RelateNative.relate_database_close(handle) == RelateNative.RELATE_OK;
}

/// <summary>One session (<c>relate_connection</c>). Calls on it must be serialized.</summary>
internal sealed class ConnectionHandle : SafeHandle
{
    public ConnectionHandle() : base(0, ownsHandle: true) { }

    internal ConnectionHandle(nint handle) : this() => SetHandle(handle);

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle() => RelateNative.relate_disconnect(handle) == RelateNative.RELATE_OK;
}

/// <summary>One statement's result (<c>relate_query_result</c>); owns every value handle and string under it.</summary>
internal sealed class ResultHandle : SafeHandle
{
    public ResultHandle() : base(0, ownsHandle: true) { }

    internal ResultHandle(nint handle) : this() => SetHandle(handle);

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        RelateNative.relate_destroy_result(handle);
        return true;
    }
}

/// <summary>A root params builder (<c>relate_params</c>); children are owned by the root.</summary>
internal sealed class ParamsHandle : SafeHandle
{
    public ParamsHandle() : base(0, ownsHandle: true) { }

    internal ParamsHandle(nint handle) : this() => SetHandle(handle);

    public override bool IsInvalid => handle == 0;

    protected override bool ReleaseHandle()
    {
        RelateNative.relate_params_free(handle);
        return true;
    }
}
