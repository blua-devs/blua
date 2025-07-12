using System;
using System.Runtime.InteropServices;

namespace bLua.NativeLua
{
    /// <summary> Container for all lua_* API calls to Lua </summary>
    public static class LuaLibAPI
    {
        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_setfield(IntPtr L, int index, IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getfield(IntPtr L, int index, IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_setglobal(IntPtr L, string name);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getglobal(IntPtr L, string name);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_seti(IntPtr L, int index, int i);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_geti(IntPtr L, int index, int i);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_setiuservalue(IntPtr L, int index, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getiuservalue(IntPtr L, int index, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_setmetatable(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getmetatable(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_settable(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_gettable(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_settop(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_gettop(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr lua_setupvalue(IntPtr L, int funcindex, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_getupvalue(IntPtr L, int funcindex, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_checkstack(IntPtr L, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr lua_close(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_createtable(IntPtr L, int narr, int nrec);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_isyieldable(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr lua_newthread(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr lua_newuserdatauv(IntPtr L, IntPtr size, int nuvalue);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_next(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_pcallk(IntPtr L, int nargs, int nresults, int msgh, long ctx, IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushboolean(IntPtr L, int b);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushcclosure(IntPtr L, IntPtr fn, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushinteger(IntPtr L, int n);

        [DllImport(Lua.LUA_DLL, CharSet = CharSet.Ansi)]
        public static extern void lua_pushlstring(IntPtr L, IntPtr s, ulong len);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushnil(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushnumber(IntPtr L, double n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushvalue(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_rawequal(IntPtr L, int index1, int index2);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_rawgeti(IntPtr L, int index, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern uint lua_rawlen(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_resume(IntPtr L, IntPtr from, int nargs, out int nresults);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_status(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_toboolean(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_tointegerx(IntPtr L, int index, IntPtr isnum);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr lua_tolstring(IntPtr L, int index, StrLen len);

        [DllImport(Lua.LUA_DLL)]
        public static extern double lua_tonumberx(IntPtr L, int index, IntPtr isnum);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr lua_topointer(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern IntPtr lua_tothread(IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_type(IntPtr L, int tp);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_xmove(IntPtr from, IntPtr to, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_yieldk(IntPtr L, int nresults, IntPtr ctx, IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_base(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_coroutine(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_debug(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_io(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_math(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_os(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_package(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_string(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_table(IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_utf8(IntPtr L);
    }
} // bLua.NativeLua namespace
