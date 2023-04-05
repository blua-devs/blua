using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace bLua.NativeLua
{
    /// <summary> Container for all luaL_* API calls to Lua </summary>
    public static class LuaXLibAPI
    {
        [DllImport(Lua.LUA_DLL)]
        public static extern void luaL_traceback(System.IntPtr state, System.IntPtr state2, string msg, int level);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr luaL_newstate();

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaL_openlibs(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern int luaL_newmetatable(System.IntPtr state, string tname);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaL_setmetatable(System.IntPtr state, string tname);

        //int lua_pushthread (lua_State* L);

        [DllImport(Lua.LUA_DLL)]
        public static extern int luaL_loadbufferx(System.IntPtr state, string buff, ulong sz, string name, string mode);

        //references
        [DllImport(Lua.LUA_DLL)]
        public static extern int luaL_ref(System.IntPtr state, int t);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaL_unref(System.IntPtr state, int t, int refIndex);
    }
} // bLua.NativeLua namespace
