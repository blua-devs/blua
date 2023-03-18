using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Profiling;

namespace bLua.NativeLua
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LuaCFunction(IntPtr state);

    [StructLayout(LayoutKind.Sequential)]
    public class StrLen
    {
        public ulong len;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class CoroutineResult
    {
        public int result;
    }

    public struct StringCacheEntry
    {
        public string key;
        public bLuaValue value;
    }

    /// <summary> Contains helper functions as well as functions that interface with the LuaLibAPI and LuaXLibAPI. </summary>
    public static class Lua
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        public const string dllName = "lua54.dll";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        public const string dllName = "Lua";
#endif

        public static int LUA_TNONE = -1;
        public static int LUAI_MAXSTACK = 1000000;
        public static int LUA_REGISTRYINDEX = (-LUAI_MAXSTACK - 1000);

        public static int LUA_RIDX_MAINTHREAD = 1;
        public static int LUA_RIDX_GLOBALS = 2;
        public static int LUA_RIDX_LAST = LUA_RIDX_GLOBALS;

        public static ProfilerMarker s_profileLuaGC = new ProfilerMarker("Lua.GC");
        public static ProfilerMarker s_profileLuaCo = new ProfilerMarker("Lua.Coroutine");
        public static ProfilerMarker s_profileLuaCall = new ProfilerMarker("Lua.Call");
        public static ProfilerMarker s_profileLuaCallInner = new ProfilerMarker("Lua.CallInner");

        static StrLen s_strlen = new StrLen();


        #region Miscellaneous
        public static IntPtr GetMainThread(IntPtr _state)
        {
            LuaLibAPI.lua_rawgeti(_state, LUA_REGISTRYINDEX, LUA_RIDX_MAINTHREAD);
            IntPtr thread = LuaLibAPI.lua_tothread(_state, -1);
            LuaPop(_state, 1);
            return thread;
        }

        /// <remarks> WARNING!!! All variables declared in an environment must be declared with `local` </remarks>
        /// <summary> Call this after loading a buffer, but before calling it. This function creates an empty environment for the provided name (overwriting
        /// an existing one, if there is one) and copies over _G from the main environment so libraries can be accessed. </summary>
        public static void SetupEnvironmentForBuffer(IntPtr _state, string _environment)
        {
            LuaLibAPI.lua_getglobal(_state, _environment); // pushes a new, named environment (_environment). S: (buffer)(newEnv)
            LuaLibAPI.lua_createtable(_state, 0, 0); // pushes an empty table. S: (buffer)(newEnv)(emptyTable)
            LuaLibAPI.lua_getglobal(_state, "_G"); // pushes _G. S: (buffer)(newEnv)(emptyTable)(_G)
            LuaLibAPI.lua_setfield(_state, -2, Lua.StringToIntPtr("__index")); // sets the stack at index to the -1 stack index's value. pops the value. S: (buffer)(newEnv)(emptyTableWith_GAs__index)
            LuaLibAPI.lua_setmetatable(_state, -2); // pops -1 stack index and sets it to the stack value at index. S: (buffer)(newEnvWith_GAs__index)
            LuaLibAPI.lua_setupvalue(_state, -2, 1); // assigns -1 stack index to the upvalue for value in stack at index. S: (bufferWithNewEnvUpvalue)
        }

        public static IntPtr StringToIntPtr(string _string)
        {
            byte[] b = StrToUTF8(_string);

            unsafe
            {
                fixed (byte* p = b)
                {
                    return new IntPtr((void*)p);
                }
            }
        }

        public static byte[] StrToUTF8(string _string)
        {
            return System.Text.UTF8Encoding.UTF8.GetBytes(_string);
        }

        public static string UTF8ToStr(byte[] _bytes)
        {
            return System.Text.UTF8Encoding.UTF8.GetString(_bytes);
        }

        public static int UpValueIndex(int _i)
        {
            return LUA_REGISTRYINDEX - _i;
        }

        public static string GetString(IntPtr _state, int _n)
        {
            var ptr = LuaLibAPI.lua_tolstring(_state, _n, s_strlen);
            byte[] bytes = new byte[s_strlen.len];
            Marshal.Copy(ptr, bytes, 0, (int)s_strlen.len);
            return UTF8ToStr(bytes);
        }

        public static void DestroyDynValue(bLuaInstance _instance, int _refid)
        {
            if (_instance.state != IntPtr.Zero)
            {
                //remove the value from the registry.
                LuaXLibAPI.luaL_unref(_instance.state, LUA_REGISTRYINDEX, _refid);
            }
        }

        public static DataType InspectTypeOnTopOfStack(bLuaInstance _instance)
        {
            return (DataType)LuaLibAPI.lua_type(_instance.state, -1);
        }

        public static string TraceMessage(bLuaInstance _instance, string _message = null, int _level = 1)
        {
            if (_message == null)
            {
                _message = "stack";
            }
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            LuaXLibAPI.luaL_traceback(_instance.state, _instance.state, _message, _level);
            return PopString(_instance);
        }
        #endregion // Miscellaneous

        #region Push (Stack)
        public static void PushObjectOntoStack(bLuaInstance _instance, bool _bool)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            LuaLibAPI.lua_pushboolean(_instance.state, _bool ? 1 : 0);
        }

        public static void PushObjectOntoStack(bLuaInstance _instance, double _double)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            LuaLibAPI.lua_pushnumber(_instance.state, _double);
        }

        public static void PushObjectOntoStack(bLuaInstance _instance, float _double)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            LuaLibAPI.lua_pushnumber(_instance.state, (double)_double);
        }

        public static void PushObjectOntoStack(bLuaInstance _instance, int _int)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            LuaLibAPI.lua_pushinteger(_instance.state, _int);
        }

        public static void PushString(IntPtr _state, string _string)
        {
            byte[] b = StrToUTF8(_string);
            LuaLibAPI.lua_pushlstring(_state, StringToIntPtr(_string), (ulong)b.Length);
        }

        public static void PushObjectOntoStack(bLuaInstance _instance, string _d)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            if (_d == null)
            {
                PushNil(_instance);
                return;
            }
            PushString(_instance.state, _d);
        }

        public static void PushNil(bLuaInstance _instance)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            LuaLibAPI.lua_pushnil(_instance.state);
        }

        public static void LuaPushCFunction(bLuaInstance _instance, LuaCFunction _fn)
        {
            LuaLibAPI.lua_pushcclosure(_instance.state, Marshal.GetFunctionPointerForDelegate(_fn), 0);
        }

        public static void PushClosure(bLuaInstance _instance, LuaCFunction _fn, bLuaValue[] _upvalues)
        {
            for (int i = 0; i != _upvalues.Length; ++i)
            {
                PushStack(_instance, _upvalues[i]);
            }

            LuaLibAPI.lua_pushcclosure(_instance.state, Marshal.GetFunctionPointerForDelegate(_fn), _upvalues.Length);
        }

        public static void PushNewTable(bLuaInstance _instance, int _reserveArray = 0, int _reserveTable = 0)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            LuaLibAPI.lua_createtable(_instance.state, _reserveArray, _reserveTable);
        }

        public static void PushObjectOntoStack(bLuaInstance _instance, object _object)
        {
            bLuaValue dynValue = _object as bLuaValue;
            if (dynValue != null)
            {
                PushStack(_instance, dynValue);
                return;
            }

            LuaLibAPI.lua_checkstack(_instance.state, 1);

            if (_object == null)
            {
                LuaLibAPI.lua_pushnil(_instance.state);
            }
            else if (_object is int)
            {
                LuaLibAPI.lua_pushinteger(_instance.state, (int)_object);
            }
            else if (_object is double)
            {
                LuaLibAPI.lua_pushnumber(_instance.state, (double)_object);
            }
            else if (_object is float)
            {
                LuaLibAPI.lua_pushnumber(_instance.state, (double)(float)_object);
            }
            else if (_object is bool)
            {
                LuaLibAPI.lua_pushboolean(_instance.state, ((bool)_object) ? 1 : 0);
            }
            else if (_object is string)
            {
                PushString(_instance.state, (string)_object);
            }
            else if (_object is LuaCFunction)
            {
                LuaPushCFunction(_instance, _object as LuaCFunction);
            }
            else
            {
                LuaLibAPI.lua_pushnil(_instance.state);
                _instance.Error($"Unrecognized object pushing onto stack: {_object.GetType().ToString()}");
            }
        }

        public static int PushStack(bLuaInstance _instance, bLuaValue _value)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);

            if (_value == null)
            {
                PushNil(_instance);
                return (int)DataType.Nil;
            }

            return LuaLibAPI.lua_rawgeti(_instance.state, LUA_REGISTRYINDEX, _value.refid);
        }
        #endregion // Push (Stack)

        #region Pop (Stack)
        public static void LuaPop(IntPtr _state, int _n)
        {
            LuaLibAPI.lua_settop(_state, -(_n) - 1);
        }

        public static bLuaValue PopStackIntoValue(bLuaInstance _instance)
        {
            int t = LuaLibAPI.lua_type(_instance.state, -1);
            switch (t)
            {
                case (int)DataType.Nil:
                    LuaPop(_instance.state, 1);
                    return bLuaValue.Nil;

                default:
                    //pops the value on top of the stack and makes a reference to it.
                    int refid = LuaXLibAPI.luaL_ref(_instance.state, LUA_REGISTRYINDEX);
                    return new bLuaValue(_instance, refid);
            }
        }

        public static object PopStackIntoObject(bLuaInstance _instance)
        {
            DataType t = (DataType)LuaLibAPI.lua_type(_instance.state, -1);
            switch (t)
            {
                case DataType.Nil:
                    PopStack(_instance);
                    return bLuaValue.Nil;
                case DataType.Boolean:
                    return PopBool(_instance);
                case DataType.Number:
                    return PopNumber(_instance);
                case DataType.String:
                    return PopString(_instance);
                default:
                    return PopStackIntoValue(_instance);
            }
        }

        public static double PopNumber(bLuaInstance _instance)
        {
            double result = LuaLibAPI.lua_tonumberx(_instance.state, -1, IntPtr.Zero);
            LuaPop(_instance.state, 1);
            return result;
        }

        public static int PopInteger(bLuaInstance _instance)
        {
            int result = LuaLibAPI.lua_tointegerx(_instance.state, -1, IntPtr.Zero);
            LuaPop(_instance.state, 1);
            return result;
        }

        public static bool PopBool(bLuaInstance _instance)
        {
            int result = LuaLibAPI.lua_toboolean(_instance.state, -1);
            LuaPop(_instance.state, 1);
            return result != 0;
        }

        public static string PopString(bLuaInstance _instance)
        {
            string result = GetString(_instance.state, -1);
            LuaPop(_instance.state, 1);
            return result;
        }

        public static List<bLuaValue> PopList(bLuaInstance _instance)
        {
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            List<bLuaValue> result = new List<bLuaValue>(len);

            LuaLibAPI.lua_checkstack(_instance.state, 2);

            for (int i = 1; i <= len; ++i)
            {
                LuaLibAPI.lua_geti(_instance.state, -1, i);
                result.Add(PopStackIntoValue(_instance));
            }

            //we're actually popping the list off.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static List<string> PopListOfStrings(bLuaInstance _instance)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 2);

            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            List<string> result = new List<string>(len);

            for (int i = 1; i <= len; ++i)
            {
                int t = LuaLibAPI.lua_geti(_instance.state, -1, i);
                if (t == (int)DataType.String)
                {
                    result.Add(PopString(_instance));
                }
                else
                {
                    PopStack(_instance);
                }
            }

            //we're actually popping the list off.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static Dictionary<string, bLuaValue> PopDict(bLuaInstance _instance)
        {
            Dictionary<string, bLuaValue> result = new Dictionary<string, bLuaValue>();
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                if (LuaLibAPI.lua_type(_instance.state, -2) != (int)DataType.String)
                {
                    LuaPop(_instance.state, 1);
                    continue;
                }

                string key = GetString(_instance.state, -2);
                result.Add(key, PopStackIntoValue(_instance));
            }

            //pop the table off the stack.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static List<bLuaValue.Pair> PopFullDict(bLuaInstance _instance)
        {
            List<bLuaValue.Pair> result = new List<bLuaValue.Pair>();
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                var val = PopStackIntoValue(_instance);
                var key = PopStackIntoValue(_instance);
                PushStack(_instance, key);

                result.Add(new bLuaValue.Pair()
                {
                    Key = key,
                    Value = val,
                });
            }

            //pop the table off the stack.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static bool PopTableHasNonInts(bLuaInstance _instance)
        {
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                var val = PopStackIntoValue(_instance);

                if (LuaLibAPI.lua_type(_instance.state, -1) != (int)DataType.String)
                {
                    //pop key, value, and table.
                    LuaPop(_instance.state, 3);
                    return true;
                }

                //just pop value, key goes with next.
                LuaPop(_instance.state, 1);
            }

            //pop the table off the stack.
            LuaPop(_instance.state, 1);

            return false;
        }

        public static bool PopTableEmpty(bLuaInstance _instance)
        {
            LuaLibAPI.lua_pushnil(_instance.state);

            bool result = (LuaLibAPI.lua_next(_instance.state, -2) == 0);
            LuaPop(_instance.state, result ? 1 : 3); //if empty pop just the table, otherwise the table and the key/value pair.

            return result;
        }

        public static void PopStack(bLuaInstance _instance)
        {
            LuaPop(_instance.state, 1);
        }
        #endregion // Pop (Stack)

        #region New Values
        public static bLuaValue NewMetaTable(bLuaInstance _instance, string tname)
        {
            LuaXLibAPI.luaL_newmetatable(_instance.state, tname);
            return PopStackIntoValue(_instance);
        }

        public static bLuaValue NewBoolean(bLuaInstance _instance, bool val)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_pushboolean(_instance.state, val ? 1 : 0);
            return PopStackIntoValue(_instance);
        }

        public static bLuaValue NewNumber(bLuaInstance _instance, double val)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_pushnumber(_instance.state, val);
            return PopStackIntoValue(_instance);
        }

        public static bLuaValue NewString(bLuaInstance _instance, string val)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            PushString(_instance.state, val);
            return PopStackIntoValue(_instance);
        }
        #endregion // New Values

        #region Tables
        public static bLuaValue GetTable(bLuaInstance _instance, bLuaValue tbl, string key)
        {
            PushStack(_instance, tbl);
            PushObjectOntoStack(_instance, key);
            DataType t = (DataType)LuaLibAPI.lua_gettable(_instance.state, -2);
            var result = PopStackIntoValue(_instance); //pop the result value out.
            result.dataType = t;
            PopStack(_instance); //pop the table out and discard it.
            return result;
        }

        public static bLuaValue GetTable(bLuaInstance _instance, bLuaValue tbl, object key)
        {
            PushStack(_instance, tbl);
            PushObjectOntoStack(_instance, key);
            DataType t = (DataType)LuaLibAPI.lua_gettable(_instance.state, -2);
            var result = PopStackIntoValue(_instance); //pop the result value out.
            result.dataType = t;
            PopStack(_instance); //pop the table out and discard it.
            return result;
        }

        public static bLuaValue RawGetTable(bLuaInstance _instance, bLuaValue tbl, string key)
        {
            PushStack(_instance, tbl);
            PushObjectOntoStack(_instance, key);
            DataType t = (DataType)LuaLibAPI.lua_rawget(_instance.state, -2);
            var result = PopStackIntoValue(_instance); //pop the result value out.
            result.dataType = t;
            PopStack(_instance); //pop the table out and discard it.
            return result;
        }

        public static bLuaValue RawGetTable(bLuaInstance _instance, bLuaValue _tbl, object _key)
        {
            PushStack(_instance, _tbl);
            PushObjectOntoStack(_instance, _key);
            DataType t = (DataType)LuaLibAPI.lua_rawget(_instance.state, -2);
            var result = PopStackIntoValue(_instance); //pop the result value out.
            result.dataType = t;
            PopStack(_instance); //pop the table out and discard it.
            return result;
        }

        public static void SetTable(bLuaInstance _instance, bLuaValue tbl, bLuaValue key, bLuaValue val)
        {
            PushStack(_instance, tbl);
            PushStack(_instance, key);
            PushStack(_instance, val);
            LuaLibAPI.lua_settable(_instance.state, -3);
            PopStack(_instance);
        }

        public static void SetTable(bLuaInstance _instance, bLuaValue tbl, string key, bLuaValue val)
        {
            PushStack(_instance, tbl);
            PushObjectOntoStack(_instance, key);
            PushStack(_instance, val);
            LuaLibAPI.lua_settable(_instance.state, -3);
            PopStack(_instance);
        }

        public static void SetTable(bLuaInstance _instance, bLuaValue tbl, string key, object val)
        {
            PushStack(_instance, tbl);
            PushObjectOntoStack(_instance, key);
            PushObjectOntoStack(_instance, val);
            LuaLibAPI.lua_settable(_instance.state, -3);
            PopStack(_instance);
        }

        public static void SetTable(bLuaInstance _instance, bLuaValue tbl, object key, object val)
        {
            PushStack(_instance, tbl);
            PushObjectOntoStack(_instance, key);
            PushObjectOntoStack(_instance, val);
            LuaLibAPI.lua_settable(_instance.state, -3);
            PopStack(_instance);
        }
        #endregion // Tables

        #region Arrays
        public static int Length(bLuaInstance _instance, bLuaValue val)
        {
            PushStack(_instance, val);
            uint result = LuaLibAPI.lua_rawlen(_instance.state, -1);
            PopStack(_instance);

            return (int)result;
        }

        //index -- remember, 1-based!
        public static bLuaValue Index(bLuaInstance _instance, bLuaValue val, int index)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 3);
            PushStack(_instance, val);
            LuaLibAPI.lua_geti(_instance.state, -1, index);
            var result = PopStackIntoValue(_instance);
            PopStack(_instance);
            return result;
        }

        public static void SetIndex(bLuaInstance _instance, bLuaValue array, int index, bLuaValue newVal)
        {
            PushStack(_instance, array);
            PushStack(_instance, newVal);
            LuaLibAPI.lua_seti(_instance.state, -2, index);
            PopStack(_instance);
        }

        public static void AppendArray(bLuaInstance _instance, bLuaValue array, bLuaValue newVal)
        {
            PushStack(_instance, array);
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            PushStack(_instance, newVal);
            LuaLibAPI.lua_seti(_instance.state, -2, len + 1);
            PopStack(_instance);
        }

        public static void AppendArray(bLuaInstance _instance, bLuaValue array, object newVal)
        {
            PushStack(_instance, array);
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            PushObjectOntoStack(_instance, newVal);
            LuaLibAPI.lua_seti(_instance.state, -2, len + 1);
            PopStack(_instance);
        }
        #endregion // Arrays
    }
} // bLua.NativeLua namespace