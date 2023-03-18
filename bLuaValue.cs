using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using bLua.NativeLua;

namespace bLua
{
    public class bLuaValue : IDisposable
    {
#if UNITY_EDITOR
        public static int nLive = 0;
        public static int nLiveHighWater = 0;
#endif

        public static int nTotalCreated = 0;

        public static int NOREF = -2;
        public static int REFNIL = -1;

        public int refid = NOREF;
        public DataType dataType = DataType.Unknown;

        public int ReferenceID
        {
            get
            {
                return refid;
            }
        }

        bLuaInstance instance;


        static public bLuaValue Nil = new bLuaValue()
        {
            dataType = DataType.Nil
        };

        public static bLuaValue NewNil()
        {
            return Nil;
        }

        public static bLuaValue CreateNil()
        {
            return Nil;
        }

        public static bool IsNilOrNull(bLuaValue val)
        {
            return val == null || val.refid == REFNIL;
        }

        public static bool NotNilOrNull(bLuaValue val)
        {
            return val != null && val.refid != REFNIL;
        }

        public bool IsNil()
        {
            return refid == REFNIL;
        }

        public bool IsNotNil()
        {
            return refid != REFNIL;
        }

        public bool IsTable()
        {
            return Type == DataType.Table;
        }

        public static bLuaValue CreateString(bLuaInstance _instance, string _string)
        {
            if (_string == null)
            {
                return Nil;
            }

            if (_string.Length < 32)
            {
                uint hash = (uint)_string.GetHashCode();
                uint n = hash % (uint)_instance.s_stringCache.Length;
                var entry = _instance.s_stringCache[n];
                if (entry.key == _string)
                {
                    Assert.AreEqual(entry.key, entry.value.String);
                    ++_instance.s_stringCacheHit;
                    return entry.value;
                } else
                {
                    Lua.PushObjectOntoStack(_instance, _string);
                    bLuaValue result = Lua.PopStackIntoValue(_instance);

                    entry.key = _string;
                    entry.value = result;
                    _instance.s_stringCache[n] = entry;
                    ++_instance.s_stringCacheMiss;
                    return result;
                }
            }

            Lua.PushObjectOntoStack(_instance, _string);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateNumber(bLuaInstance _instance, double _double)
        {
            Lua.PushObjectOntoStack(_instance, _double);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateBool(bLuaInstance _instance, bool _bool)
        {
            Lua.PushObjectOntoStack(_instance, _bool);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateTable(bLuaInstance _instance, int _reserveArray = 0, int _reserveTable = 0)
        {
            Lua.PushNewTable(_instance, _reserveArray, _reserveTable);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateFunction(bLuaInstance _instance, LuaCFunction _fn)
        {
            Lua.LuaPushCFunction(_instance, _fn);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateClosure(bLuaInstance _instance, LuaCFunction _fn, params bLuaValue[] _upvalues)
        {
            Lua.PushClosure(_instance, _fn, _upvalues);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateUserData(bLuaInstance _instance, object _object)
        {
            if (_object == null)
            {
                return Nil;
            }
            bLuaUserData.PushNewUserData(_instance, _object);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue FromObject(bLuaInstance _instance, object _object)
        {
            Lua.PushObjectOntoStack(_instance, _object);
            return Lua.PopStackIntoValue(_instance);
        }

        public bLuaValue()
        {
            FinishConstruction();
            refid = REFNIL;

            instance = null;
        }

        public bLuaValue(bLuaInstance _instance)
        {
            FinishConstruction();
            refid = REFNIL;
            instance = _instance;
        }

        public bLuaValue(bLuaInstance _instance, int _refid)
        {
            FinishConstruction();
            refid = _refid;
            instance = _instance;
        }

        void FinishConstruction()
        {
#if UNITY_EDITOR
            System.Threading.Interlocked.Increment(ref nLive);
            if (nLive > nLiveHighWater)
            {
                nLiveHighWater = nLive;
            }
#endif
            ++nTotalCreated;
        }

        ~bLuaValue()
        {
            Dispose(false);

#if UNITY_EDITOR
            System.Threading.Interlocked.Decrement(ref nLive);
#endif
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);

#if UNITY_EDITOR
            System.Threading.Interlocked.Decrement(ref nLive);
#endif
        }

        void Dispose(bool deterministic)
        {
            if (refid != NOREF && refid != REFNIL)
            {
                if (deterministic)
                {
                    Lua.DestroyDynValue(instance, refid);
                }
                // remnant from C#-managed GC
                /*
                else
                {
                    deleteQueue.Enqueue(refid);
                }
                */
                refid = NOREF;
            }
        }

        public DataType Type
        {
            get
            {
                if (dataType == DataType.Unknown)
                {
                    Lua.PushStack(instance, this);
                    dataType = Lua.InspectTypeOnTopOfStack(instance);
                    Lua.PopStack(instance);
                }

                return dataType;
            }
        }

        public double Number
        {
            get
            {
                Lua.PushStack(instance, this);
                return Lua.PopNumber(instance);
            }
        }

        public int Integer
        {
            get
            {
                Lua.PushStack(instance, this);
                return Lua.PopInteger(instance);
            }
        }

        public bool Boolean
        {
            get
            {
                Lua.PushStack(instance, this);
                return Lua.PopBool(instance);
            }
        }

        public string String
        {
            get
            {
                int t = Lua.PushStack(instance, this);
                if (t == (int)DataType.String)
                {
                    return Lua.PopString(instance);
                }

                Lua.PopStack(instance);
                return null;
            }
        }

        public bLuaValue UserData
        {
            get
            {
                if (Type != DataType.UserData)
                {
                    return null;
                }

                return this;
            }
        }

        public object Object
        {
            get
            {
                if (Type != DataType.UserData)
                {
                    return null;
                }

                Lua.PushStack(instance, this);
                object result = bLuaUserData.GetUserDataObject(instance, - 1);
                Lua.PopStack(instance);
                return result;
            }
        }

        public bLuaValue Function
        {
            get
            {
                if (Type == DataType.Function)
                {
                    return this;
                }

                return null;
            }
        }

        public bLuaValue Table
        {
            get
            {
                if (Type == DataType.Table)
                {
                    return this;
                }

                return null;
            }
        }

        public bLuaValue MetaTable
        {
            get
            {
    #if UNITY_EDITOR
                int nstack = LuaLibAPI.lua_gettop(instance.state);
    #endif

                Lua.PushStack(instance, this);
                int res = LuaLibAPI.lua_getmetatable(instance.state, -1);
                if (res == 0)
                {
                    Lua.PopStack(instance);
                    return Nil;
                }

                var result = Lua.PopStackIntoValue(instance);
                Lua.PopStack(instance);

    #if UNITY_EDITOR
                Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
    #endif

                return result;
            }
            set
            {
                Lua.PushStack(instance, this);
                Lua.PushStack(instance, value);
                LuaLibAPI.lua_setmetatable(instance.state, -2);
                Lua.PopStack(instance);
            }
        }

        public bool? CastToOptionalBool()
        {
            DataType dataType = (DataType)Lua.PushStack(instance, this);
            switch (dataType)
            {
                case DataType.Boolean:
                    return Lua.PopBool(instance);
                case DataType.Number:
                    return Lua.PopNumber(instance) != 0;
                case DataType.Nil:
                    Lua.PopStack(instance);
                    return null;
                default:
                    Lua.PopStack(instance);
                    return null;
            }
        }

        public bool CastToBool(bool _defaultValue = false)
        {
            DataType dataType = (DataType)Lua.PushStack(instance, this);
            switch (dataType)
            {
                case DataType.Boolean:
                    return Lua.PopBool(instance);
                case DataType.Number:
                    return Lua.PopNumber(instance) != 0;
                case DataType.Nil:
                    Lua.PopStack(instance);
                    return _defaultValue;
                default:
                    Lua.PopStack(instance);
                    return _defaultValue;
            }
        }

        public string ToPrintString()
        {
            return CastToString();
        }

        public string ToDebugPrintString()
        {
            return CastToString();
        }

        public string CastToString(string _defaultValue = "")
        {
            DataType dataType = (DataType)Lua.PushStack(instance, this);

            switch (dataType)
            {
                case DataType.String:
                    return Lua.PopString(instance);
                case DataType.Number:
                    return Lua.PopNumber(instance).ToString();
                case DataType.Boolean:
                    return Lua.PopBool(instance) ? "true" : "false";
                default:
                    Lua.PopStack(instance);
                    return _defaultValue;
            }
        }

        public float? CastToOptionalFloat()
        {
            DataType dataType = (DataType)Lua.PushStack(instance, this);
            switch (dataType)
            {
                case DataType.Number:
                    return (float)Lua.PopNumber(instance);
                case DataType.String:
                    {
                        float f;
                        string s = Lua.PopString(instance);
                        if (float.TryParse(s, out f))
                        {
                            return f;
                        }

                        return null;
                    }
                case DataType.Boolean:
                    return Lua.PopBool(instance) ? 1.0f : 0.0f;
                default:
                    Lua.PopStack(instance);
                    return null;
            }

        }

        public float CastToFloat(float _defaultValue = 0.0f)
        {
            DataType dataType = (DataType)Lua.PushStack(instance, this);

            switch (dataType)
            {
                case DataType.Number:
                    return (float)Lua.PopNumber(instance);
                case DataType.String:
                    {
                        float f;
                        string s = Lua.PopString(instance);
                        if (float.TryParse(s, out f))
                        {
                            return f;
                        }

                        return _defaultValue;
                    }
                case DataType.Boolean:
                    return Lua.PopBool(instance) ? 1.0f : 0.0f;
                default:
                    Lua.PopStack(instance);
                    return _defaultValue;
            }
        }

        public int CastToInt(int _defaultValue = 0)
        {
            DataType dataType = (DataType)Lua.PushStack(instance, this);

            switch (dataType)
            {
                case DataType.Number:
                    return (int)Lua.PopNumber(instance);
                case DataType.String:
                    {
                        int f;
                        string s = Lua.PopString(instance);
                        if (int.TryParse(s, out f))
                        {
                            return f;
                        }

                        return _defaultValue;
                    }
                case DataType.Boolean:
                    return Lua.PopBool(instance) ? 1 : 0;
                default:
                    Lua.PopStack(instance);
                    return _defaultValue;
            }
        }

        public double? CastToNumber()
        {
            DataType dataType = (DataType)Lua.PushStack(instance, this);

            switch (dataType)
            {
                case DataType.Number:
                    return Lua.PopNumber(instance);
                case DataType.String:
                    {
                        double f;
                        string s = Lua.PopString(instance);
                        if (double.TryParse(s, out f))
                        {
                            return f;
                        }

                        return 0.0;
                    }
                case DataType.Boolean:
                    return Lua.PopBool(instance) ? 1.0 : 0.0;
                case DataType.Nil:
                    Lua.PopStack(instance);
                    return null;
                default:
                    Lua.PopStack(instance);
                    return null;
            }
        }

        public T CheckUserDataType<T>(string _string) where T : class
        {
            T result = Object as T;
            if (result == null)
            {
                Debug.Log($"Could not convert to lua value to type: {_string}");
            }

            return result;
        }

        public T ToObject<T>()
        {
            return (T)Object;
        }

        public object ToObject(Type _type)
        {
            if (_type == typeof(double))
            {
                return CastToNumber();
            }
            else if (_type == typeof(float))
            {
                return CastToFloat();
            }
            else if (_type == typeof(int))
            {
                return (int)CastToNumber();
            }
            else if (_type == typeof(bool))
            {
                return CastToBool();
            }
            else if (_type == typeof(string))
            {
                return CastToString();
            }
            else
            {
                return null;
            }
        }

        public object ToObject()
        {
            switch (Type)
            {
                case DataType.Boolean:
                    return CastToBool();
                case DataType.Nil:
                    return null;
                case DataType.Number:
                    return Number;
                case DataType.String:
                    return String;
                case DataType.Table:
                    return Dict();
                case DataType.UserData:
                    return Object;
                default:
                    return null;
            }
        }

        public bLuaValue Call(params object[] args)
        {
            if (instance != null)
            {
                return instance.Call(this, args);
            }
            return null;
        }

        public int Length
        {
            get
            {
                return Lua.Length(instance, this);
            }
        }

        public bLuaValue this[int n]
        {
            get
            {
                return Lua.Index(instance, this, n + 1);
            }
        }

        public bLuaValue GetNonRaw(string key)
        {
            return Lua.GetTable(instance, this, key);
        }

        public bLuaValue GetNonRaw(object key)
        {
            return Lua.GetTable(instance, this, key);
        }

        //synonyms with RawGet
        public bLuaValue Get(string key)
        {
            return Lua.RawGetTable(instance, this, key);
        }

        public bLuaValue Get(object key)
        {
            return Lua.RawGetTable(instance, this, key);
        }

        public bLuaValue RawGet(object key)
        {
            return Lua.RawGetTable(instance, this, key);
        }

        public bLuaValue RawGet(string key)
        {
            return Lua.RawGetTable(instance, this, key);
        }

        public void Set(bLuaValue key, bLuaValue val)
        {
            Lua.SetTable(instance, this, key, val);
        }

        public void Set(string key, bLuaValue val)
        {
            Lua.SetTable(instance, this, key, val);
        }

        public void Set(string key, object val)
        {
            Lua.SetTable(instance, this, key, val);
        }

        public void Set(object key, object val)
        {
            Lua.SetTable(instance, this, key, val);
        }

        public void Remove(object key)
        {
            Lua.SetTable(instance, this, key, Nil);
        }

        public List<bLuaValue> List()
        {
            if (Type != DataType.Table)
            {
                return null;
            }

#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif

            Lua.PushStack(instance, this);
            var result = Lua.PopList(instance);

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif

            return result;
        }

        public List<string> ListOfStrings()
        {
            if (Type != DataType.Table)
            {
                return null;
            }

#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif

            Lua.PushStack(instance, this);
            var result = Lua.PopListOfStrings(instance);
#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif

            return result;
        }

        public Dictionary<string, bLuaValue> Dict()
        {
#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif

            Lua.PushStack(instance, this);
            var result = Lua.PopDict(instance);

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif

            return result;
        }

        public struct Pair
        {
            public bLuaValue Key;
            public bLuaValue Value;
        }

        public List<Pair> Pairs()
        {
#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif

            Lua.PushStack(instance, this);
            var result = Lua.PopFullDict(instance);

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif

            return result;
        }

        public List<bLuaValue> Keys
        {
            get
            {
#if UNITY_EDITOR
                int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif

                var result = Pairs();
                var values = new List<bLuaValue>();
                foreach (var p in result)
                {
                    values.Add(p.Key);
                }

#if UNITY_EDITOR
                Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif

                return values;
            }
        }

        public List<bLuaValue> Values
        {
            get
            {
                var result = Pairs();
                var values = new List<bLuaValue>();
                foreach (var p in result)
                {
                    values.Add(p.Value);
                }
                return values;
            }
        }

        public bool TableEmpty
        {
            get
            {
#if UNITY_EDITOR
                int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif

                Lua.PushStack(instance, this);
                var result = Lua.PopTableEmpty(instance);

#if UNITY_EDITOR
                Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif

                return result;
            }
        }

        //has only non-ints.
        public bool IsPureArray
        {
            get
            {
                Lua.PushStack(instance, this);
                return !Lua.PopTableHasNonInts(instance);
            }
        }

        public void Append(bLuaValue val)
        {
            Lua.AppendArray(instance, this, val);
        }

        public void Append(object val)
        {
            Lua.AppendArray(instance, this, val);
        }

        public static void RunDispose(List<bLuaValue> list)
        {
            foreach (var item in list)
            {
                item.Dispose();
            }
        }

        public static void RunDispose(Dictionary<string, bLuaValue> dict)
        {
            foreach (var item in dict)
            {
                item.Value.Dispose();
            }
        }

        public override bool Equals(object a)
        {
            bLuaValue other = a as bLuaValue;
            if (other == null)
            {
                return false;
            }

            if (Type == DataType.Nil
                && other.Type == DataType.Nil)
            {
                return true;
            }

            if ((this.instance != other.instance)
                || this.instance == null
                || other.instance == null)
            {
                // unable to do a raw equality check via lua if the values exist in different lua instances
                return false;
            }

#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif

            Lua.PushStack(instance, this);
            Lua.PushStack(instance, other);

            int res = LuaLibAPI.lua_rawequal(instance.state, -1, -2);
            Lua.LuaPop(instance.state, 2);

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif

            return res != 0;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
} // bLua namespace
