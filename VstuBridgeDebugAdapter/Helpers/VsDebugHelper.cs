using System.Buffers;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Debugger.Interop;

namespace VstuBridgeDebugAdapter.Helpers;

static class VsDebugHelper
{
    public static IDebugThread2[] ToArray(IEnumDebugThreads2 e)
    {
        var ret = e.GetCount(out var count);
        if (ret != 0)
            throw new InvalidOperationException();
        if (count == 0)
            return Array.Empty<IDebugThread2>();
        var array = new IDebugThread2[count];
        var fetched = 0u;
        ret = e.Next(count, array, ref fetched);
        if (ret != 0 || fetched != count)
            throw new InvalidOperationException();
        return array;
    }

    public static FRAMEINFO[] ToArray(IEnumDebugFrameInfo2 e)
    {
        var ret = e.GetCount(out var count);
        if (ret != 0)
            throw new InvalidOperationException();
        if (count == 0)
            return Array.Empty<FRAMEINFO>();
        var array = new FRAMEINFO[count];
        var fetched = 0u;
        ret = e.Next(count, array, ref fetched);
        if (ret != 0 || fetched != count)
            throw new InvalidOperationException();
        return array;
    }

    public static DEBUG_PROPERTY_INFO[] ToArray(IEnumDebugPropertyInfo2 e)
    {
        var ret = e.GetCount(out var count);
        if (ret != 0)
            throw new InvalidOperationException();
        if (count == 0)
            return Array.Empty<DEBUG_PROPERTY_INFO>();
        var array = new DEBUG_PROPERTY_INFO[count];
        ret = e.Next(count, array, out var fetched);
        if (ret != 0 || fetched != count)
            throw new InvalidOperationException();
        return array;
    }

    public static DEBUG_PROPERTY_INFO[] GetChildren(IDebugProperty2 property)
    {
        var guidFilter = default(Guid);
        var flags = enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_STANDARD
                    | enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_PROP;
        var attribFlags = enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL;
        var nameFilter = "";
        var timeout = 5000u;
        property.EnumChildren(flags, 10u, ref guidFilter, attribFlags, nameFilter, timeout, out var ppEnum);
        if (ppEnum is null)
            return Array.Empty<DEBUG_PROPERTY_INFO>();

        return ToArray(ppEnum);
    }
}
