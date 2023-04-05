using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using bLua.NativeLua;
using bLua.Internal;

namespace bLua
{
    public class bLuaValue : IDisposable
    {
        public static int totalCreated = 0;

        public static int NOREF = -2;
        public static int REFNIL = -1;

        public int referenceID = NOREF;

        public DataType dataType = DataType.Unknown;

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

        public static bool IsNilOrNull(bLuaValue _value)
        {
            return _value == null || _value.referenceID == REFNIL;
        }

        public static bool NotNilOrNull(bLuaValue val)
        {
            return val != null && val.referenceID != REFNIL;
        }

        public bool IsNil()
        {
            return referenceID == REFNIL;
        }

        public bool IsNotNil()
        {
            return referenceID != REFNIL;
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
                uint n = hash % (uint)_instance.stringCache.Length;
                var entry = _instance.stringCache[n];
                if (entry.key == _string)
                {
                    Assert.AreEqual(entry.key, entry.value.String);
                    ++_instance.stringCacheHit;
                    return entry.value;
                } else
                {
                    Lua.PushOntoStack(_instance, _string);
                    bLuaValue result = Lua.PopStackIntoValue(_instance);

                    entry.key = _string;
                    entry.value = result;
                    _instance.stringCache[n] = entry;
                    ++_instance.stringCacheMiss;
                    return result;
                }
            }

            Lua.PushOntoStack(_instance, _string);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateNumber(bLuaInstance _instance, double _double)
        {
            Lua.PushOntoStack(_instance, _double);
            return Lua.PopStackIntoValue(_instance);
        }

        public static bLuaValue CreateBool(bLuaInstance _instance, bool _bool)
        {
            Lua.PushOntoStack(_instance, _bool);
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
            Lua.PushOntoStack(_instance, _object);
            return Lua.PopStackIntoValue(_instance);
        }

        public bLuaValue()
        {
            FinishConstruction();
            referenceID = REFNIL;

            instance = null;
        }

        public bLuaValue(bLuaInstance _instance)
        {
            FinishConstruction();
            referenceID = REFNIL;
            instance = _instance;
        }

        public bLuaValue(bLuaInstance _instance, int _refid)
        {
            FinishConstruction();
            referenceID = _refid;
            instance = _instance;
        }

        void FinishConstruction()
        {
            ++totalCreated;
        }

        ~bLuaValue()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        void Dispose(bool _deterministic)
        {
            if (referenceID != NOREF && referenceID != REFNIL)
            {
                if (_deterministic)
                {
                    Lua.DestroyDynValue(instance, referenceID);
                }
                // remnant from C#-managed GC
                /*
                else
                {
                    deleteQueue.Enqueue(refid);
                }
                */
                referenceID = NOREF;
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
                case DataType.UserData:
                    bLuaValue v = Lua.PopStackIntoValue(instance);
                    if (v.Object.GetType().GetMethod("ToString") != null)
                    {
                        return v.Object.ToString();
                    }
                    goto default;
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

        public bLuaValue Call(params object[] _args)
        {
            if (instance != null)
            {
                return instance.Call(this, _args);
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

        public bLuaValue Get<T>(T _key)
        {
            return Lua.GetTable(instance, this, _key);
        }

        public void Set<TKey, TValue>(TKey _key, TValue _value)
        {
            Lua.SetTable(instance, this, _key, _value);
        }

        public void Remove(object _key)
        {
            Lua.SetTable(instance, this, _key, Nil);
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

        public void Append(bLuaValue _value)
        {
            Lua.AppendArray(instance, this, _value);
        }

        public void Append(object _value)
        {
            Lua.AppendArray(instance, this, _value);
        }

        public static void RunDispose(List<bLuaValue> _list)
        {
            foreach (var item in _list)
            {
                item.Dispose();
            }
        }

        public static void RunDispose(Dictionary<string, bLuaValue> _dict)
        {
            foreach (var item in _dict)
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
