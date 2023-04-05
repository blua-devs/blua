using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace bLua.NativeLua
{
    /// <summary> Container for all lua_* API calls to Lua </summary>
    public static class LuaLibAPI
    {
        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_createtable(System.IntPtr state, int narray, int ntable);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_geti(System.IntPtr state, int stack_index, int table_index);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_seti(System.IntPtr state, int stack_index, int table_index);

        //returns the length of the string or table.
        [DllImport(Lua.LUA_DLL)]
        public static extern uint lua_rawlen(System.IntPtr state, int stack_index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_next(System.IntPtr state, int idx);

        //Does the equivalent to t[k] = v, where t is the value at the given index, v is the value on the top of the stack, and k is the value just below the top.
        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_settable(System.IntPtr state, int idx);

        //Pushes onto the stack the value t[k], where t is the value at the given index and k is the value on the top of the stack.
        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_gettable(System.IntPtr state, int idx);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_rawget(System.IntPtr state, int idx);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getglobal(System.IntPtr state, string key);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_setglobal(System.IntPtr state, string key);

        // returns a char*
        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_typename(System.IntPtr state, int idx);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_close(System.IntPtr state);

        //push values onto stack.
        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushnil(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushnumber(System.IntPtr state, double n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushinteger(System.IntPtr state, int n);

        public static void lua_pushint64(System.IntPtr state, long n)
        {
            int r;
            if (!int.TryParse(n.ToString(), out r))
            {
                r = int.MaxValue;
            }
            lua_pushinteger(state, r);
        }
        public static void lua_pushuint(System.IntPtr state, uint n)
        {
            lua_pushinteger(state, unchecked((int)n));
        }
        public static void lua_pushuint64(System.IntPtr state, ulong n)
        {
            lua_pushint64(state, unchecked((long)n + long.MinValue));
        }

        [DllImport(Lua.LUA_DLL, CharSet = CharSet.Ansi)]
        public static extern void lua_pushlstring(System.IntPtr state, System.IntPtr s, ulong len);

        public static void lua_pushstring(System.IntPtr state, string str)
        {
            lua_pushlstring(state, Lua.StringToIntPtr(str), (ulong)Lua.StrToUTF8(str).Length);
        }
        public static void lua_pushbytes(System.IntPtr state, byte[] bytes)
        {
            lua_pushstring(state, System.Text.Encoding.Default.GetString(bytes));
        }

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushboolean(System.IntPtr state, int b);

        public static void lua_pushboolean(System.IntPtr state, bool b)
        {
            lua_pushboolean(state, b ? 1 : 0);
        }

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushvalue(System.IntPtr state, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_xmove(System.IntPtr state, System.IntPtr to, int n);

        //find type of value on stack.
        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_type(System.IntPtr state, int idx);

        //inspect values on stack.
        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_toboolean(System.IntPtr state, int idx);

        [DllImport(Lua.LUA_DLL)]
        public static extern double lua_tonumberx(System.IntPtr state, int n, System.IntPtr /*|int*|*/ isnum);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_tointegerx(System.IntPtr state, int n, System.IntPtr /*|int*|*/ isnum);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_tolstring(System.IntPtr state, int n, StrLen /*|size_t*|*/ len);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_tothread(System.IntPtr state, int n);

        //void lua_pushcclosure (lua_State* L, lua_CFunction fn, int n);
        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushcclosure(System.IntPtr state, System.IntPtr fn, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_newuserdatauv(System.IntPtr state, System.IntPtr sz, int nuvalue);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getiuservalue(System.IntPtr state, int idx, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_setiuservalue(System.IntPtr state, int idx, int n);

        // returns a char*
        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_setupvalue(System.IntPtr state, int funcindex, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_setmetatable(System.IntPtr state, int objindex);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getmetatable(System.IntPtr state, int objindex);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushlightuserdata(System.IntPtr state, System.IntPtr addr);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_base(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_coroutine(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_table(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_io(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_os(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_string(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_utf8(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_math(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_debug(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_package(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_pcall(System.IntPtr state, int nargs, int nresults, int msgh);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_pcallk(System.IntPtr state, int nargs, int nresults, int msgh, long ctx, System.IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_resume(System.IntPtr state, System.IntPtr from, int nargs, CoroutineResult result);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_settop(System.IntPtr state, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_rotate(System.IntPtr state, int idx, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_gettop(System.IntPtr state);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_checkstack(System.IntPtr state, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_rawgeti(System.IntPtr state, int idx, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_rawequal(System.IntPtr state, int idx1, int idx2);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_compare(System.IntPtr state, int idx1, int idx2, int op);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_setfield(System.IntPtr state, int idx, System.IntPtr k);
    }
} // bLua.NativeLua namespace
