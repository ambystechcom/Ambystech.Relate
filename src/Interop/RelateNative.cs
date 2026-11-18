using System.Runtime.InteropServices;

namespace Ambystech.Relate.Interop;

/// <summary>
/// The Relate typed C ABI v2 (<c>relate.h</c>, ADR-0502), one-to-one: 81 entry points.
/// </summary>
/// <remarks>
/// Ownership follows the header: a mutable <c>char*</c> (error_json, bookmark_out) is
/// caller-owned and freed with <see cref="relate_free_string"/>; a <c>const char*</c>
/// is owned by its result and valid until <see cref="relate_destroy_result"/>. Value
/// handles are result-owned and never freed individually. Owned handles are wrapped in
/// <see cref="SafeHandle"/> subclasses by the managed layer; this class stays raw.
/// </remarks>
internal static unsafe partial class RelateNative
{
    internal const string Library = "relate_ffi";

    /// <summary>The ABI major version this binding was written against.</summary>
    internal const int ExpectedAbiVersion = 2;

    internal const int RELATE_OK = 0;
    internal const int RELATE_ERROR = 1;

    // ---- Lifecycle and misc (9) ---------------------------------------------------

    [LibraryImport(Library)]
    internal static partial int relate_abi_version();

    [LibraryImport(Library)]
    internal static partial void relate_free_string(nint s);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_database_open(string uri, out nint db, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_database_open_read_only(string uri, out nint db, out nint errorJson);

    [LibraryImport(Library)]
    internal static partial int relate_database_close(nint db);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_connect_namespace(nint db, string? name, out nint conn, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_conn_set_namespace(nint conn, string name, out nint errorJson);

    [LibraryImport(Library)]
    internal static partial int relate_disconnect(nint conn);

    [LibraryImport(Library)]
    internal static partial int relate_checkpoint(nint db, out nint errorJson);

    // ---- Statements and transactions (5) ------------------------------------------

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_query_params(nint conn, string cypher, nint @params, out nint result, out nint errorJson);

    [LibraryImport(Library)]
    internal static partial void relate_destroy_result(nint result);

    [LibraryImport(Library)]
    internal static partial int relate_conn_begin(nint conn, out nint errorJson);

    [LibraryImport(Library)]
    internal static partial int relate_conn_commit(nint conn, out nint bookmark, out nint errorJson);

    [LibraryImport(Library)]
    internal static partial int relate_conn_rollback(nint conn, out nint errorJson);

    // ---- Results (15) ---------------------------------------------------------------

    [LibraryImport(Library)]
    internal static partial long relate_column_count(nint result);

    [LibraryImport(Library)]
    internal static partial long relate_row_count(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_column_name(nint result, long col);

    [LibraryImport(Library)]
    internal static partial byte relate_result_is_error(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_result_error_json(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_result_bookmark(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_result_query_type(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_result_namespace(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_result_stats(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_result_notifications(nint result);

    [LibraryImport(Library)]
    internal static partial byte* relate_result_plan_json(nint result);

    [LibraryImport(Library)]
    internal static partial double relate_result_elapsed_ms(nint result);

    [LibraryImport(Library)]
    internal static partial double relate_result_parse_ms(nint result);

    [LibraryImport(Library)]
    internal static partial double relate_result_plan_ms(nint result);

    [LibraryImport(Library)]
    internal static partial double relate_result_execute_ms(nint result);

    // ---- Value walker (34) ----------------------------------------------------------

    [LibraryImport(Library)]
    internal static partial nint relate_value_at(nint result, long row, long col);

    [LibraryImport(Library)]
    internal static partial int relate_value_kind(nint v);

    [LibraryImport(Library)]
    internal static partial int relate_value_bool(nint v, out byte value);

    [LibraryImport(Library)]
    internal static partial int relate_value_int(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_float(nint v, out double value);

    [LibraryImport(Library)]
    internal static partial int relate_value_is_nan(nint v, out byte value);

    [LibraryImport(Library)]
    internal static partial int relate_value_is_inf(nint v, out byte value);

    [LibraryImport(Library)]
    internal static partial byte* relate_value_string(nint v, out nuint len);

    [LibraryImport(Library)]
    internal static partial byte* relate_value_element_id(nint v);

    [LibraryImport(Library)]
    internal static partial byte* relate_value_start_element_id(nint v);

    [LibraryImport(Library)]
    internal static partial byte* relate_value_end_element_id(nint v);

    [LibraryImport(Library)]
    internal static partial int relate_value_id(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_start_id(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_end_id(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial long relate_value_list_len(nint v);

    [LibraryImport(Library)]
    internal static partial nint relate_value_list_get(nint v, long i);

    [LibraryImport(Library)]
    internal static partial long relate_value_map_len(nint v);

    [LibraryImport(Library)]
    internal static partial byte* relate_value_map_key(nint v, long i, out nuint len);

    [LibraryImport(Library)]
    internal static partial nint relate_value_map_get(nint v, byte* key);

    [LibraryImport(Library)]
    internal static partial int relate_value_year(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_month(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_day(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_hour(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_minute(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_second(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_nanosecond(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial int relate_value_offset_seconds(nint v, out long value);

    [LibraryImport(Library)]
    internal static partial byte* relate_value_zone(nint v);

    [LibraryImport(Library)]
    internal static partial int relate_value_duration(nint v, out long months, out long days, out long seconds, out long nanos);

    [LibraryImport(Library)]
    internal static partial int relate_value_point(nint v, out int srid, out int dim, out double x, out double y, out double z);

    [LibraryImport(Library)]
    internal static partial byte* relate_value_vector_dtype(nint v);

    [LibraryImport(Library)]
    internal static partial long relate_value_vector_dim(nint v);

    [LibraryImport(Library)]
    internal static partial int relate_value_vector_float(nint v, long i, out double value);

    [LibraryImport(Library)]
    internal static partial int relate_value_vector_int(nint v, long i, out long value);

    // ---- Params builder (18) --------------------------------------------------------
    // A MAP container takes a non-NULL unique name; a LIST container takes NULL and appends.

    [LibraryImport(Library)]
    internal static partial nint relate_params_new();

    [LibraryImport(Library)]
    internal static partial void relate_params_free(nint p);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_null(nint p, string? name, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_bool(nint p, string? name, byte v, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_int(nint p, string? name, long v, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_float(nint p, string? name, double v, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_string(nint p, string? name, byte* v, nuint len, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_date(nint p, string? name, long year, long month, long day, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_time(nint p, string? name, long hour, long minute, long second, long nanosecond, long offsetSeconds, string? zone, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_local_time(nint p, string? name, long hour, long minute, long second, long nanosecond, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_datetime(nint p, string? name, long year, long month, long day, long hour, long minute, long second, long nanosecond, long offsetSeconds, string? zone, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_local_datetime(nint p, string? name, long year, long month, long day, long hour, long minute, long second, long nanosecond, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_duration(nint p, string? name, long months, long days, long seconds, long nanos, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_point(nint p, string? name, int srid, double x, double y, double* z, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_vector_float(nint p, string? name, string dtype, double* values, nuint dim, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_vector_int(nint p, string? name, string dtype, long* values, nuint dim, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_list(nint p, string? name, out nint child, out nint errorJson);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int relate_params_put_map(nint p, string? name, out nint child, out nint errorJson);
}
