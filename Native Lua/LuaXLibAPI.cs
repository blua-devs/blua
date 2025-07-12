using System;
using System.Runtime.InteropServices;

namespace bLua.NativeLua
{
    /// <summary> Container for all luaL_* API calls to Lua </summary>
    public static class LuaXLibAPI
    {
        [DllImport(Lua.LUA_DLL)]
        public static extern int luaL_loadbufferx(IntPtr L, string buff, ulong sz, string name, string mode);

        [DllImport(Lua.LUA_DLL)]
        public static extern int luaL_newmetatable(IntPtr L, string tname);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr luaL_newstate();

        [DllImport(Lua.LUA_DLL)]
        public static extern int luaL_ref(IntPtr L, int t);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaL_traceback(IntPtr L, IntPtr L1, string msg, int level);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaL_unref(IntPtr L, int t, int _ref);
    }
} // bLua.NativeLua namespace
