using System;

namespace bLua.Internal
{
    /// <summary>
    /// "This attribute is valid on static functions and it is used by Mono's Ahead of Time Compiler to generate the code necessary
    /// to support native call calling back into managed code. In regular ECMA CIL programs this happens automatically, and it is
    /// not necessary to flag anything specially, but with pure Ahead of Time compilation the compiler needs to know which methods
    /// will be called from the unmanaged code."
    /// <para> https://learn.microsoft.com/en-us/dotnet/api/objcruntime.monopinvokecallbackattribute </para>
    /// </summary>
    public class MonoPInvokeCallbackAttribute : Attribute
    {
    }
} // bLua.Internal namespace
