using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using bLua.Internal;

namespace bLua.NativeLua
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LuaCFunction(IntPtr state);

    [StructLayout(LayoutKind.Sequential)]
    public class StrLen
    {
        public ulong len;
    }

    public enum LuaThreadStatus
    {
        LUA_OK = 0,
        LUA_YIELD = 1,
        LUA_ERRRUN = 2,
        LUA_ERRSYNTAX = 3,
        LUA_ERRMEM = 4,
        LUA_ERRERR = 5
    }
    
    /// <summary>
    /// Contains helper functions as well as functions that interface with the LuaLibAPI and LuaXLibAPI.
    /// </summary>
    public static class Lua
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        public const string LUA_DLL = "lua54.dll";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        public const string LUA_DLL = "Lua";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        public const string LUA_DLL = "liblua54.so";
#endif

        public static int LUA_TNONE = -1;
        public static int LUA_MAXSTACK = 1000000;
        public static int LUA_REGISTRYINDEX = (-LUA_MAXSTACK - 1000);

        public static int LUA_RIDX_MAINTHREAD = 1;
        public static int LUA_RIDX_GLOBALS = 2;


#region Miscellaneous
        public static IntPtr GetMainThread(IntPtr _state)
        {
            LuaLibAPI.lua_rawgeti(_state, LUA_REGISTRYINDEX, LUA_RIDX_MAINTHREAD);
            IntPtr thread = LuaLibAPI.lua_tothread(_state, -1);
            Pop(_state, 1);
            return thread;
        }

        public static IntPtr StringToIntPtr(string _string)
        {
            byte[] b = StrToUTF8(_string);

            unsafe
            {
                fixed (byte* p = b)
                {
                    return new IntPtr(p);
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

        public static string GetString(IntPtr _state, int n)
        {
            StrLen strlen = new();
            
            var ptr = LuaLibAPI.lua_tolstring(_state, n, strlen);
            byte[] bytes = new byte[strlen.len];
            Marshal.Copy(ptr, bytes, 0, (int)strlen.len);
            return UTF8ToStr(bytes);
        }

        public static int UpValueIndex(int i)
        {
            return LUA_REGISTRYINDEX - i;
        }

        public static LuaType InspectTypeOnTopOfStack(bLuaInstance _instance)
        {
            return (LuaType)LuaLibAPI.lua_type(_instance.state, -1);
        }

        /// <summary>
        /// Returns a stack trace from the top of the stack.
        /// </summary>
        /// <param name="_message"> An optional message to be prepended to the returned trace </param>
        /// <param name="_level"> The level in the stack to start tracing from </param>
        public static string TraceMessage(bLuaInstance _instance, string _message = null, int _level = 1)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaXLibAPI.luaL_traceback(_instance.state, _instance.state, _message != null ? _message : "", _level);
            return PopString(_instance);
        }

        public static void Unreference(bLuaInstance _instance, int _referenceID)
        {
            if (_instance.state != IntPtr.Zero)
            {
                LuaXLibAPI.luaL_unref(_instance.state, LUA_REGISTRYINDEX, _referenceID);
            }
        }
#endregion // Miscellaneous

#region Push (Stack)
        public static void PushNil(bLuaInstance _instance)
        {
            PushNil(_instance.state);
        }
        
        public static void PushNil(IntPtr _state)
        {
            LuaLibAPI.lua_checkstack(_state, 1);
            LuaLibAPI.lua_pushnil(_state);
        }

        public static void PushString(bLuaInstance _instance, string _string)
        {
            byte[] b = StrToUTF8(_string);
            LuaLibAPI.lua_pushlstring(_instance.state, StringToIntPtr(_string), (ulong)b.Length);
        }

        public static void PushCFunction(bLuaInstance _instance, IntPtr _state, LuaCFunction _fn)
        {
            LuaLibAPI.lua_pushcclosure(_state, Marshal.GetFunctionPointerForDelegate(_fn), 0);
        }

        public static void PushClosure(bLuaInstance _instance, IntPtr _state, LuaCFunction _fn, bLuaValue[] _upvalues)
        {
            for (int i = 0; i != _upvalues.Length; ++i)
            {
                PushValue(_state, _upvalues[i]);
            }

            LuaLibAPI.lua_pushcclosure(_state, Marshal.GetFunctionPointerForDelegate(_fn), _upvalues.Length);
        }

        public static void PushClosure(bLuaInstance _instance, IntPtr _state, GlobalMethodCallInfo _globalMethodCallInfo)
        {
            LuaCFunction fn = CallGlobalMethod;
            bLuaValue[] upvalues = new bLuaValue[] { bLuaValue.CreateNumber(_instance, _instance.registeredMethods.Count) };
            _instance.registeredMethods.Add(_globalMethodCallInfo);

            PushClosure(_instance, _state, fn, upvalues);
        }

        public static void PushClosure<T>(bLuaInstance _instance, IntPtr _state, T _func) where T : MulticastDelegate
        {
            MethodInfo methodInfo = _func.Method;

            ParameterInfo[] methodParams = methodInfo.GetParameters();
            List<UserDataType> argTypes = new();
            List<object> defaultArgs = new();
            for (int i = 0; i != methodParams.Length; ++i)
            {
                if (methodParams[i].GetCustomAttributes().FirstOrDefault(a => a is bLuaParam_Ignored) != null)
                {
                    continue;
                }

                UserDataType paramType = methodParams[i].ParameterType.ToUserDataType(_instance);
                if (methodParams[i].GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
                {
                    argTypes.Add(UserDataType.Params);
                }
                else
                {
                    argTypes.Add(paramType);
                }

                if (methodParams[i].HasDefaultValue)
                {
                    defaultArgs.Add(methodParams[i].DefaultValue);
                }
                else if (paramType == UserDataType.LuaValue)
                {
                    defaultArgs.Add(bLuaValue.Nil);
                }
                else
                {
                    defaultArgs.Add(null);
                }
            }

            LuaCFunction fn = CallDelegate;
            bLuaValue[] upvalues = new bLuaValue[] { bLuaValue.CreateNumber(_instance, _instance.registeredMethods.Count) };

            DelegateCallInfo methodCallInfo = new DelegateCallInfo()
            {
                methodInfo = methodInfo,
                returnType = methodInfo.ReturnType.ToUserDataType(_instance),
                argTypes = argTypes.ToArray(),
                defaultArgs = defaultArgs.ToArray(),
                closure = bLuaValue.CreateClosure(_instance, fn, upvalues),
                multicastDelegate = _func
            };
            _instance.registeredMethods.Add(methodCallInfo);

            PushClosure(_instance, _state, fn, upvalues);
        }

        public static void PushReturnType(bLuaInstance _instance, UserDataType _returnType, object _result)
        {
            PushReturnType(_instance, _instance.state, _returnType, _result);
        }
        
        public static void PushReturnType(bLuaInstance _instance, IntPtr _state, UserDataType _returnType, object _result)
        {
            switch (_returnType)
            {
                case UserDataType.Void:
                    PushNil(_state);
                    return;
                case UserDataType.LuaValue:
                    PushValue(_state, _result as bLuaValue);
                    return;
                case UserDataType.Bool:
                    PushObject(_instance, _state, (bool)_result);
                    return;
                case UserDataType.Integer:
                    PushObject(_instance, _state, (int)_result);
                    return;
                case UserDataType.Float:
                    PushObject(_instance, _state, (float)_result);
                    return;
                case UserDataType.Double:
                    PushObject(_instance, _state, (double)_result);
                    return;
                case UserDataType.String:
                    PushObject(_instance, _state, (string)_result);
                    return;
                case UserDataType.Array:
                    if (!_result.GetType().IsArray)
                    {
                        goto default;
                    }

                    bLuaValue arrayTable = bLuaValue.CreateTable(_instance);
                    foreach (object o in (object[])_result)
                    {
                        arrayTable.TableAppend(o);
                    }

                    PushObject(_instance, _state, arrayTable);

                    return;
                case UserDataType.List:
                    if (!_result.GetType().IsGenericType || _result.GetType().GetGenericTypeDefinition() != typeof(List<>))
                    {
                        goto default;
                    }

                    bLuaValue listTable = bLuaValue.CreateTable(_instance);
                    foreach (object o in (IEnumerable)_result)
                    {
                        listTable.TableAppend(o);
                    }

                    PushObject(_instance, _state, listTable);

                    return;
                case UserDataType.Dictionary:
                    if (!_result.GetType().IsGenericType || _result.GetType().GetGenericTypeDefinition() != typeof(Dictionary<,>))
                    {
                        goto default;
                    }

                    bLuaValue dictionaryTable = bLuaValue.CreateTable(_instance);
                    foreach (object key in ((IDictionary)_result).Keys)
                    {
                        dictionaryTable.Set(key, ((IDictionary)_result)[key]);
                    }

                    PushObject(_instance, _state, dictionaryTable);

                    return;
                case UserDataType.UserDataClass:
                    PushNewUserData(_instance, _state, _result);
                    return;
                default:
                    PushNil(_state);
                    return;
            }
        }

        public static void PushNewUserData(bLuaInstance _instance, object _object)
        {
            PushNewUserData(_instance, _instance.state, _object);
        }
        
        public static void PushNewUserData(bLuaInstance _instance, IntPtr _state, object _object)
        {
            if (_object == null)
            {
                PushNil(_state);
                return;
            }

            if (_instance.typenameToEntryIndex.TryGetValue(_object.GetType().Name, out int typeIndex) == false)
            {
                PushNil(_state);
                return;
            }

            UserDataRegistryEntry entry = _instance.registeredEntries[typeIndex];

            int objIndex;
            if (_instance.liveObjectsFreeList.Count > 0)
            {
                objIndex = _instance.liveObjectsFreeList[_instance.liveObjectsFreeList.Count - 1];
                _instance.liveObjectsFreeList.RemoveAt(_instance.liveObjectsFreeList.Count - 1);
            }
            else
            {
                if (_instance.nextLiveObject >= _instance.liveObjects.Length)
                {
                    object[] liveObjects = new object[_instance.liveObjects.Length * 2];
                    for (int i = 0; i < _instance.liveObjects.Length; ++i)
                    {
                        liveObjects[i] = _instance.liveObjects[i];
                    }
                    _instance.liveObjects = liveObjects;

                    object[] syntaxSugarProxies = new object[_instance.syntaxSugarProxies.Length * 2];
                    for (int i = 0; i < _instance.syntaxSugarProxies.Length; ++i)
                    {
                        syntaxSugarProxies[i] = _instance.syntaxSugarProxies[i];
                    }
                    _instance.syntaxSugarProxies = syntaxSugarProxies;
                }

                objIndex = _instance.nextLiveObject;
                _instance.nextLiveObject++;
            }

            LuaLibAPI.lua_newuserdatauv(_instance.state, new IntPtr(8), 1);
            PushObject(_instance, _state, objIndex);
            LuaLibAPI.lua_setiuservalue(_instance.state, -2, 1);
            PushValue(_state, entry.metatable);
            LuaLibAPI.lua_setmetatable(_instance.state, -2);

            _instance.liveObjects[objIndex] = _object;
        }
        
        public static void PushNewTable(bLuaInstance _instance, int _reserveArray = 0, int _reserveTable = 0)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_createtable(_instance.state, _reserveArray, _reserveTable);
        }

        public static void PushObject(bLuaInstance _instance, object _object)
        {
            PushObject(_instance, _instance.state, _object);
        }
        
        public static void PushObject(bLuaInstance _instance, IntPtr _state, object _object)
        {
            if (_object is bLuaValue dynValue)
            {
                PushValue(_state, dynValue);
                return;
            }
            
            if (_object.GetType().IsUserDataRegistered(_instance))
            {
                bLuaValue ud = bLuaValue.CreateUserData(_instance, _object);
                PushValue(_state, ud);
                return;
            }

            LuaLibAPI.lua_checkstack(_state, 1);

            switch (_object)
            {
                case int objectInt:
                    LuaLibAPI.lua_pushinteger(_state, objectInt);
                    break;
                case double objectDouble:
                    LuaLibAPI.lua_pushnumber(_state, objectDouble);
                    break;
                case float objectFloat:
                    LuaLibAPI.lua_pushnumber(_state, objectFloat);
                    break;
                case bool objectBool:
                    LuaLibAPI.lua_pushboolean(_state, objectBool ? 1 : 0);
                    break;
                case string objectString:
                    LuaLibAPI.lua_pushlstring(_state, StringToIntPtr(objectString), (ulong)StrToUTF8(objectString).Length);
                    break;
                case LuaCFunction objectFunction:
                    PushCFunction(_instance, _state, objectFunction);
                    break;
                case GlobalMethodCallInfo objectInfo:
                    PushClosure(_instance, _state, objectInfo);
                    break;
                case MulticastDelegate objectDelegate: // Func<> and Action<>
                    PushClosure(_instance, _state, objectDelegate);
                    break;
                default:
                    LuaLibAPI.lua_pushnil(_state);
                    _instance.ErrorFromCSharp($"{bLuaError.error_unrecognizedStackPush}{_object.GetType()}");
                    break;
            }
        }

        public static int PushValue(bLuaInstance _instance, bLuaValue _value)
        {
            return PushValue(_instance.state, _value);
        }
        public static int PushValue(IntPtr _state, bLuaValue _value)
        {
            LuaLibAPI.lua_checkstack(_state, 1);

            if (_value == null)
            {
                PushNil(_state);
                return (int)LuaType.Nil;
            }

            return LuaLibAPI.lua_rawgeti(_state, LUA_REGISTRYINDEX, _value.referenceId);
        }
#endregion // Push (Stack)

#region Pop (Stack)
        public static void Pop(IntPtr _state, int n)
        {
            LuaLibAPI.lua_settop(_state, -(n) - 1);
        }

        public static void Pop(bLuaInstance _instance)
        {
            Pop(_instance.state, 1);
        }
        
        public static bool PopBool(bLuaInstance _instance)
        {
            int result = LuaLibAPI.lua_toboolean(_instance.state, -1);
            Pop(_instance.state, 1);
            return result != 0;
        }

        public static double PopNumber(bLuaInstance _instance)
        {
            double result = LuaLibAPI.lua_tonumberx(_instance.state, -1, IntPtr.Zero);
            Pop(_instance.state, 1);
            return result;
        }

        public static int PopInteger(bLuaInstance _instance)
        {
            int result = LuaLibAPI.lua_tointegerx(_instance.state, -1, IntPtr.Zero);
            Pop(_instance.state, 1);
            return result;
        }

        public static string PopString(bLuaInstance _instance)
        {
            string result = GetString(_instance.state, -1);
            Pop(_instance.state, 1);
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
                result.Add(PopValue(_instance));
            }

            Pop(_instance.state, 1);

            return result;
        }

        public static Dictionary<string, bLuaValue> PopDictionary(bLuaInstance _instance)
        {
            Dictionary<string, bLuaValue> result = new Dictionary<string, bLuaValue>();
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                if (LuaLibAPI.lua_type(_instance.state, -2) != (int)LuaType.String)
                {
                    Pop(_instance.state, 1);
                    continue;
                }

                string key = GetString(_instance.state, -2);
                result.Add(key, PopValue(_instance));
            }

            Pop(_instance.state, 1);

            return result;
        }

        public static List<bLuaValue.bLuaValuePair> PopDictionaryPairs(bLuaInstance _instance)
        {
            List<bLuaValue.bLuaValuePair> result = new List<bLuaValue.bLuaValuePair>();
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                var val = PopValue(_instance);
                var key = PopValue(_instance);
                PushValue(_instance, key);

                result.Add(new bLuaValue.bLuaValuePair()
                {
                    Key = key,
                    Value = val,
                });
            }

            Pop(_instance.state, 1);

            return result;
        }

        public static bool PopIsTableEmpty(bLuaInstance _instance)
        {
            LuaLibAPI.lua_pushnil(_instance.state);

            bool result = (LuaLibAPI.lua_next(_instance.state, -2) == 0);
            Pop(_instance.state, result ? 1 : 3);

            return result;
        }

        public static IntPtr PopPointer(bLuaInstance _instance)
        {
            IntPtr pointer = LuaLibAPI.lua_topointer(_instance.state, 1);
            Pop(_instance.state, 1);
            return pointer;
        }
        
        public static bLuaValue PopValue(bLuaInstance _instance)
        {
            int t = LuaLibAPI.lua_type(_instance.state, -1);
            switch (t)
            {
                case (int)LuaType.Nil:
                    Pop(_instance.state, 1);
                    return bLuaValue.Nil;

                default:
                    int refid = LuaXLibAPI.luaL_ref(_instance.state, LUA_REGISTRYINDEX);
                    return new bLuaValue(_instance, refid);
            }
        }

        public static object PopObject(bLuaInstance _instance)
        {
            LuaType t = (LuaType)LuaLibAPI.lua_type(_instance.state, -1);
            switch (t)
            {
                case LuaType.Nil:
                    Pop(_instance);
                    return bLuaValue.Nil;
                case LuaType.Boolean:
                    return PopBool(_instance);
                case LuaType.Number:
                    return PopNumber(_instance);
                case LuaType.String:
                    return PopString(_instance);
                default:
                    return PopValue(_instance);
            }
        }
        
        public static object PopObject(bLuaInstance _instance, UserDataType _paramType)
        {
            switch (_paramType)
            {
                case UserDataType.Bool:
                    return PopBool(_instance);
                case UserDataType.Double:
                    return PopNumber(_instance);
                case UserDataType.Float:
                    return (float)PopNumber(_instance);
                case UserDataType.Integer:
                    return PopInteger(_instance);
                case UserDataType.String:
                    return PopString(_instance);
                case UserDataType.Array:
                    return PopList(_instance).ToArray();
                case UserDataType.List:
                    return PopList(_instance);
                case UserDataType.Dictionary:
                    return PopDictionary(_instance);
                case UserDataType.LuaValue:
                    return PopValue(_instance);
                case UserDataType.UserDataClass:
                    object userDataClassObject = PopValue(_instance).ToObject();
                    return userDataClassObject != null ? Convert.ChangeType(userDataClassObject, userDataClassObject.GetType()) : null;
                default:
                    Pop(_instance);
                    return null;
            }
        }
        
        public static object PopUserDataObject(bLuaInstance _instance, int _nstack)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            int ntype = LuaLibAPI.lua_getiuservalue(_instance.state, _nstack, 1);
            if (ntype != (int)LuaType.Number)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidUserdata}");
                Pop(_instance);
                return null;
            }

            int liveObjectIndex = LuaLibAPI.lua_tointegerx(_instance.state, -1, IntPtr.Zero);
            object obj = _instance.liveObjects[liveObjectIndex];

            Pop(_instance);

            return obj;
        }
#endregion // Pop (Stack)

#region New Values
        public static bLuaValue NewMetaTable(bLuaInstance _instance, string _name)
        {
            LuaXLibAPI.luaL_newmetatable(_instance.state, _name);
            return PopValue(_instance);
        }

        public static bLuaValue NewBoolean(bLuaInstance _instance, bool _value)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_pushboolean(_instance.state, _value ? 1 : 0);
            return PopValue(_instance);
        }

        public static bLuaValue NewNumber(bLuaInstance _instance, double _value)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_pushnumber(_instance.state, _value);
            return PopValue(_instance);
        }

        public static bLuaValue NewString(bLuaInstance _instance, string _value)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            PushObject(_instance, _value);
            return PopValue(_instance);
        }
#endregion // New Values

#region Tables
        public static bLuaValue GetTable<T>(bLuaInstance _instance, bLuaValue _table, T _key)
        {
            PushValue(_instance, _table);
            PushObject(_instance, _key);
            LuaType t = (LuaType)LuaLibAPI.lua_gettable(_instance.state, -2);
            bLuaValue result = PopValue(_instance);
            Pop(_instance);
            return result;
        }

        public static void SetTable<TKey, TValue>(bLuaInstance _instance, bLuaValue _table, TKey _key, TValue _value)
        {
            PushObject(_instance, _table);
            PushObject(_instance, _key);
            PushObject(_instance, _value);
            LuaLibAPI.lua_settable(_instance.state, -3);
            Pop(_instance);
        }
#endregion // Tables

#region Arrays
        public static int Length(bLuaInstance _instance, bLuaValue _value)
        {
            PushValue(_instance, _value);
            uint result = LuaLibAPI.lua_rawlen(_instance.state, -1);
            Pop(_instance);

            return (int)result;
        }

        // Remember - Indexes are 1 based in Lua
        public static bLuaValue Index(bLuaInstance _instance, bLuaValue _value, int i)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 3);
            PushValue(_instance, _value);
            LuaLibAPI.lua_geti(_instance.state, -1, i);
            var result = PopValue(_instance);
            Pop(_instance);
            return result;
        }

        public static void SetIndex(bLuaInstance _instance, bLuaValue _array, int i, bLuaValue _newValue)
        {
            PushValue(_instance, _array);
            PushValue(_instance, _newValue);
            LuaLibAPI.lua_seti(_instance.state, -2, i);
            Pop(_instance);
        }

        public static void AppendArray(bLuaInstance _instance, bLuaValue _array, bLuaValue _newValue)
        {
            PushValue(_instance, _array);
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            PushValue(_instance, _newValue);
            LuaLibAPI.lua_seti(_instance.state, -2, len + 1);
            Pop(_instance);
        }

        public static void AppendArray(bLuaInstance _instance, bLuaValue _array, object _newValue)
        {
            PushValue(_instance, _array);
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            PushObject(_instance, _newValue);
            LuaLibAPI.lua_seti(_instance.state, -2, len + 1);
            Pop(_instance);
        }
#endregion // Arrays

#region Coroutines
        public static bool IsYieldable(IntPtr _state)
        {
            return LuaLibAPI.lua_isyieldable(_state) == 1;
        }

        public static LuaThreadStatus YieldThread(bLuaInstance _instance, IntPtr _state, int _results)
        {
            return (LuaThreadStatus)LuaLibAPI.lua_yieldk(_state, _results, IntPtr.Zero, IntPtr.Zero);
        }

        public static bool IsDead(bLuaInstance _instance, IntPtr _state)
        {
            LuaThreadStatus preResumeStatus = (LuaThreadStatus)LuaLibAPI.lua_status(_state);
            if (preResumeStatus == LuaThreadStatus.LUA_OK) // Coroutine is either dead or hasn't started yet
            {
                if (LuaLibAPI.lua_gettop(_state) == 0) // Coroutine stack is empty (coroutine is dead)
                {
                    return true;
                }
            }

            return false;
        }
        
        public static bool ResumeThread(bLuaInstance _instance, IntPtr _state, IntPtr _instigator, params object[] _args)
        {
            if (IsDead(_instance, _state))
            {
                return false;
            }
            
            // Push arguments to the coroutine state
            if (_args != null)
            {
                foreach (object arg in _args)
                {
                    PushObject(_instance, _state, arg);
                }
            }
            
            LuaThreadStatus postResumeStatus = (LuaThreadStatus)LuaLibAPI.lua_resume(_state, _instigator, _args != null ? _args.Length : 0, out int nResults);
            
            if (postResumeStatus != LuaThreadStatus.LUA_OK
                && postResumeStatus != LuaThreadStatus.LUA_YIELD)
            {
                string error = GetString(_state, -1);
                Pop(_state, 1);
                _instance.ErrorFromLua($"{bLuaError.error_inCoroutineResume}", $"{error}");

                return false;
            }

            return true;
        }

        public static IntPtr NewThread(IntPtr _state)
        {
            return LuaLibAPI.lua_newthread(_state);
        }
#endregion // Coroutines

#region MonoPInvokeCallback
        [MonoPInvokeCallback]
        public static int CallGlobalMethod(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo methodCallInfo))
                {
                    return 0;
                }
                
                if (!PopStackIntoArgs(mainThreadInstance, methodCallInfo, out object[] args, 1))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }
                
                return (int)InvokeCSharpMethod(mainThreadInstance, _state, methodCallInfo, null, args);
            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingDelegate);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        [MonoPInvokeCallback]
        public static int CallDelegate(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;
                
                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo methodCallInfo))
                {
                    return 0;
                }
                
                if (!PopStackIntoArgs(mainThreadInstance, methodCallInfo, out object[] args))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }
                
                return (int)InvokeCSharpMethod(mainThreadInstance, _state, methodCallInfo, null, args);
            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingDelegate);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        [MonoPInvokeCallback]
        public static int CallUserDataFunction(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                if (LuaLibAPI.lua_gettop(_state) == 0 // Stack size equals 0
                    || LuaLibAPI.lua_type(_state, 1) != (int)LuaType.UserData) // First arg passed isn't userdata
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_objectNotProvided}");
                    return 0;
                }

                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo info))
                {
                    return 0;
                }

                if (!PopStackIntoArgs(mainThreadInstance, info, out object[] args, 1))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }

                bLuaValue liveObjectValue = PopValue(mainThreadInstance);
                object liveObject = liveObjectValue.ToObject();
                
                return (int)InvokeCSharpMethod(mainThreadInstance, _state, info, liveObject, args);
            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingFunction);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        [MonoPInvokeCallback]
        public static int CallStaticUserDataFunction(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo info))
                {
                    return 0;
                }
                
                if (!PopStackIntoArgs(mainThreadInstance, info, out object[] args))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }

                return (int)InvokeCSharpMethod(mainThreadInstance, _state, info, null, args);
            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingFunction);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }
#endregion // MonoPInvokeCallback
        
#region Invocation
        public static LuaThreadStatus InvokeCSharpMethod(bLuaInstance _instance, IntPtr _state, MethodCallInfo _methodCallInfo, object _liveObject, params object[] _args)
        {
            // If the method is async, yield the coroutine, pause the coroutine, and only resume + unpause when the async method has completed
            if (typeof(Task).IsAssignableFrom(_methodCallInfo.methodInfo.ReturnType)
                && _methodCallInfo.methodInfo.GetCustomAttributes(typeof(AsyncStateMachineAttribute), false).Any()
                && _instance.GetIsFeatureEnabled(Features.Coroutines)
                && IsYieldable(_state))
            {
                Func<Task> asyncTask = async () =>
                {
                    object returnValue = InvokeMethodCallInfo(_methodCallInfo, _liveObject, _state, _args);
                    
                    // Await the async Task's completion before continuing
                    await (Task)returnValue;

                    _instance.SetCoroutinePauseFlag(_state, CoroutinePauseFlags.BLUA_CSHARPASYNCAWAIT, false);

                    // If our Task has a return type, we need to push those to the Lua stack as the return value(/s)
                    Type returnValueType = returnValue.GetType();
                    if (typeof(Task).IsAssignableFrom(returnValueType))
                    {
                        object[] taskReturnValues = new object[0];
                        
                        PropertyInfo taskTypeResultPropertyInfo = returnValueType.GetProperty("Result");
                        if (taskTypeResultPropertyInfo != null)
                        {
                            object taskReturnValue = taskTypeResultPropertyInfo.GetValue(returnValue);

                            Type taskReturnType = taskReturnValue.GetType();
                            if (taskReturnType.IsTuple())
                            {
                                FieldInfo[] fieldInfos = taskReturnType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                taskReturnValues = new object[fieldInfos.Length];
                                for (int i = 0; i < fieldInfos.Length; i++)
                                {
                                    taskReturnValues[i] = fieldInfos[i].GetValue(taskReturnValue);
                                }
                            }
                            else if (taskReturnType.ToUserDataType(_instance) != UserDataType.Void)
                            {
                                taskReturnValues = new object[] { taskReturnValue };
                            }
                        }
                        
                        // Resume the thread and pass in any return values from the C# function
                        ResumeThread(_instance, _state, _state, taskReturnValues);
                        return;
                    }
                    
                    // Resume without passing any return values
                    ResumeThread(_instance, _state, _state);
                };

                _instance.SetCoroutinePauseFlag(_state, CoroutinePauseFlags.BLUA_CSHARPASYNCAWAIT, true);
                
                Task.Run(asyncTask);
                
                return YieldThread(_instance, _state, 0);
            }
            
            // Otherwise call the method normally
            object returnValue = InvokeMethodCallInfo(_methodCallInfo, _liveObject, _state, _args);
            PushReturnType(_instance, _state, _methodCallInfo.returnType, returnValue);
            
            return LuaThreadStatus.LUA_YIELD;
        }

        private static object InvokeMethodCallInfo(MethodCallInfo _methodCallInfo, object _liveObject, IntPtr _state, params object[] _args)
        {
            ParameterInfo[] parameterInfos = _methodCallInfo.methodInfo.GetParameters();
            object[] newArgs = new object[parameterInfos.Length];
            int consumedOldArgIndex = 0;
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                if (parameterInfos[i].GetCustomAttributes().FirstOrDefault(a => a is bLuaParam_Ignored) != null)
                {
                    newArgs[i] = _state;
                }
                else
                {
                    newArgs[i] = _args[consumedOldArgIndex++];
                }
            }
            _args = newArgs;
            
            if (_methodCallInfo is DelegateCallInfo delegateCallInfo)
            {
                return delegateCallInfo.multicastDelegate.DynamicInvoke(_args);
            }
            
            if (_methodCallInfo is GlobalMethodCallInfo globalMethodCallInfo)
            {
                return globalMethodCallInfo.methodInfo.Invoke(globalMethodCallInfo.objectInstance, _args);
            }
            
            if (_methodCallInfo.methodInfo.IsStatic)
            {
                return _methodCallInfo.methodInfo.Invoke(null, _args);
            }
            
            return _methodCallInfo.methodInfo.Invoke(_liveObject, _args);
        }
        
        public static bool GetMethodInfoUpvalue(IntPtr _state, bLuaInstance _instance, out MethodCallInfo _methodInfo)
        {
            _methodInfo = null;

            int m = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(1), IntPtr.Zero);
            if (m < 0 || m >= _instance.registeredMethods.Count)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidMethodIndex}{m}");
                return false;
            }
            
            _methodInfo = _instance.registeredMethods[m];

            return true;
        }

        public static bool GetLiveObjectUpvalue(IntPtr _state, bLuaInstance _instance, out object _liveObject)
        {
            _liveObject = null;

            if (LuaLibAPI.lua_gettop(_state) < 1)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_stackIsEmpty}");
                return false;
            }

            LuaType luaType = (LuaType)LuaLibAPI.lua_type(_state, 1);
            if (luaType != LuaType.UserData)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_objectIsNotUserdata}{luaType}");
                return false;
            }

            if (LuaLibAPI.lua_checkstack(_state, 1) == 0)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_stackHasNoRoom}");
                return false;
            }
            
            int liveObjectRefId = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(2), IntPtr.Zero);
            if (liveObjectRefId < 0 || liveObjectRefId >= _instance.liveObjects.Length)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidLiveObjectIndex}{liveObjectRefId}");
                return false;
            }

            _liveObject = _instance.liveObjects[liveObjectRefId];

            return true;
        }
        
        public static bool PopStackIntoArgs(bLuaInstance _instance, MethodCallInfo _methodInfo, out object[] _args, int _skipNum = 0)
        {
            int stackSize = LuaLibAPI.lua_gettop(_instance.state);
            
            _args = new object[_methodInfo.argTypes.Length];
            int argIndex = _args.Length - 1;
            
            // If we have a params argument, prepare our iterators for that data
            object[] parms = null;
            int parmsIndex = 0;
            int len = _methodInfo.argTypes.Length;
            if (len > 0 && _methodInfo.argTypes[len - 1] == UserDataType.Params)
            {
                len--;
                if (stackSize - _skipNum > len)
                {
                    parms = new object[stackSize - _skipNum - len];
                    parmsIndex = parms.Length - 1;
                }
            }

            // If we had a params argument, populate that argument with the params array
            if (parms != null)
            {
                _args[argIndex--] = parms;
            }
            
            // Populate arguments with defaults
            while (argIndex > stackSize - (1 + _skipNum))
            {
                _args[argIndex] = _methodInfo.defaultArgs[argIndex];
                --argIndex;
            }
            
            // Populate params from stack
            while (stackSize - (1 + _skipNum) > argIndex)
            {
                if (parms != null)
                {
                    parms[parmsIndex--] = PopObject(_instance);
                }
                else
                {
                    Pop(_instance);
                }
                --stackSize;
            }

            // Populate arguments from stack
            while (stackSize > _skipNum)
            {
                _args[argIndex] = PopObject(_instance, _methodInfo.argTypes[argIndex]);

                --stackSize;
                --argIndex;
            }
            
            return true;
        }
#endregion // Invocation
    }
} // bLua.NativeLua namespace
