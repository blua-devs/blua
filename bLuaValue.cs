using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using bLua.NativeLua;

namespace bLua
{
    public class bLuaValue : IDisposable
    {
        public struct bLuaValuePair
        {
            public bLuaValue Key;
            public bLuaValue Value;
        }


        public int referenceId { get; private set; } = NOREF;

        public LuaType luaType
        {
            get
            {
                if (__type == LuaType.Unknown)
                {
                    Lua.PushValue(instance, this);
                    __type = Lua.InspectTypeOnTopOfStack(instance);
                    Lua.Pop(instance);
                }

                return __type;
            }
            private set
            {
                __type = value;
            }
        }
        private LuaType __type = LuaType.Unknown;
        
        public bLuaInstance instance { get; private set; }


        public bLuaValue()
        {
            referenceId = REFNIL;
            instance = null;
        }

        public bLuaValue(bLuaInstance _instance)
        {
            referenceId = REFNIL;
            instance = _instance;
        }

        public bLuaValue(bLuaInstance _instance, int _refid)
        {
            referenceId = _refid;
            instance = _instance;
        }

        ~bLuaValue()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool _deterministic)
        {
            if (referenceId != NOREF && referenceId != REFNIL)
            {
                if (_deterministic)
                {
                    Lua.Unreference(instance, referenceId);
                }
                else
                {
                    instance.MarkForCSharpGarbageCollection(referenceId);
                }
                referenceId = NOREF;
            }
        }

        public bool IsNil()
        {
            return referenceId == REFNIL;
        }

        public bLuaValue MetaTable
        {
            get
            {
                Lua.PushValue(instance, this);
                int res = LuaLibAPI.lua_getmetatable(instance.state, -1);
                if (res == 0)
                {
                    Lua.Pop(instance);
                    return Nil;
                }

                var result = Lua.PopValue(instance);
                Lua.Pop(instance);

                return result;
            }
            set
            {
                Lua.PushValue(instance, this);
                Lua.PushValue(instance, value);
                LuaLibAPI.lua_setmetatable(instance.state, -2);
                Lua.Pop(instance);
            }
        }

        public bool ToBool()
        {
            LuaType pushedLuaType = (LuaType)Lua.PushValue(instance, this);
            switch (pushedLuaType)
            {
                case LuaType.Boolean:
                    return Lua.PopBool(instance);
                case LuaType.Number:
                    return Lua.PopNumber(instance) != 0;
                case LuaType.Nil:
                    Lua.Pop(instance);
                    return false;
                default:
                    Lua.Pop(instance);
                    return false;
            }
        }

        public double ToNumber()
        {
            LuaType pushedLuaType = (LuaType)Lua.PushValue(instance, this);
            switch (pushedLuaType)
            {
                case LuaType.Number:
                    return Lua.PopNumber(instance);
                case LuaType.String:
                    {
                        string s = Lua.PopString(instance);
                        if (double.TryParse(s, out double f))
                        {
                            return f;
                        }

                        return 0.0;
                    }
                case LuaType.Boolean:
                    return Lua.PopBool(instance) ? 1.0 : 0.0;
                default:
                    Lua.Pop(instance);
                    return 0.0;
            }
        }
        
        public int ToInt()
        {
            LuaType pushedLuaType = (LuaType)Lua.PushValue(instance, this);
            switch (pushedLuaType)
            {
                case LuaType.Number:
                    return Lua.PopInteger(instance);
                case LuaType.String:
                {
                    string s = Lua.PopString(instance);
                    if (int.TryParse(s, out int f))
                    {
                        return f;
                    }

                    return 0;
                }
                case LuaType.Boolean:
                    return Lua.PopBool(instance) ? 1 : 0;
                default:
                    Lua.Pop(instance);
                    return 0;
            }
        }

        public float ToFloat()
        {
            LuaType pushedLuaType = (LuaType)Lua.PushValue(instance, this);
            switch (pushedLuaType)
            {
                case LuaType.Number:
                    return (float)Lua.PopNumber(instance);
                case LuaType.String:
                {
                    string s = Lua.PopString(instance);
                    if (float.TryParse(s, out float f))
                    {
                        return f;
                    }

                    return 0.0f;
                }
                case LuaType.Boolean:
                    return Lua.PopBool(instance) ? 1.0f : 0.0f;
                default:
                    Lua.Pop(instance);
                    return 0.0f;
            }
        }
        
        public override string ToString()
        {
            LuaType pushedLuaType = (LuaType)Lua.PushValue(instance, this);
            switch (pushedLuaType)
            {
                case LuaType.Nil:
                    return "nil";
                case LuaType.Boolean:
                    return ToBool() ? "true" : "false";
                case LuaType.Number:
                    return ToNumber().ToString();
                case LuaType.String:
                    return Lua.PopString(instance);
                case LuaType.UserData:
                    object value = ToObject();
                    if (value.GetType().GetMethod("ToString") != null)
                    {
                        return value.ToString();
                    }
                    goto default;
                default:
                    return $"{pushedLuaType.ToString().ToLower()}: {ToPointer().ToString()}";
            }
        }

        public List<bLuaValue> ToList()
        {
            if (luaType != LuaType.Table)
            {
                return null;
            }

            Lua.PushValue(instance, this);
            var result = Lua.PopList(instance);

            return result;
        }

        public Dictionary<string, bLuaValue> ToDictionary()
        {
            if (luaType != LuaType.Table)
            {
                return null;
            }
            
            Lua.PushValue(instance, this);
            Dictionary<string, bLuaValue> result = Lua.PopDictionary(instance);

            return result;
        }

        public IntPtr ToPointer()
        {
            LuaType pushedLuaType = (LuaType)Lua.PushValue(instance, this);
            switch (pushedLuaType)
            {
                case LuaType.Function:
                case LuaType.Table:
                case LuaType.Thread:
                    return Lua.PopPointer(instance);
                default:
                    Lua.Pop(instance);
                    return IntPtr.Zero;
            }
        }

        public object ToObject()
        {
            switch (luaType)
            {
                case LuaType.Nil:
                    return null;
                case LuaType.Boolean:
                    return ToBool();
                case LuaType.Number:
                    return ToNumber();
                case LuaType.String:
                    return ToString();
                case LuaType.Table:
                    return ToDictionary();
                case LuaType.UserData:
                    Lua.PushValue(instance, this);
                    object result = Lua.PopUserDataObject(instance, - 1);
                    Lua.Pop(instance);
                    return result;
                default:
                    return null;
            }
        }

        public T ToUserData<T>() where T : class
        {
            object result = ToObject();
            if (typeof(T).IsAssignableFrom(result.GetType()))
            {
                return result as T;
            }

            return null;
        }
        
        public T IsUserDataType<T>() where T : class
        {
            object result = ToObject();
            if (!typeof(T).IsAssignableFrom(result.GetType()))
            {
                Debug.Log($"Could not convert to {nameof(bLuaValue)} to type: {typeof(T).FullName}");
            }

            return result as T;
        }

        public bLuaValue Get<T>(T _key)
        {
            return Lua.GetTable(instance, this, _key);
        }

        public void Set<TKey, TValue>(TKey _key, TValue _value)
        {
            Lua.SetTable(instance, this, _key, _value);
        }

        public void Remove<TKey>(TKey _key)
        {
            Lua.SetTable(instance, this, _key, Nil);
        }

        public bLuaValue this[int n]
        {
            get
            {
                return Lua.Index(instance, this, n + 1);
            }
        }

        public List<bLuaValuePair> GetDictionaryPairs()
        {
            if (luaType != LuaType.Table)
            {
                return null;
            }
            
            Lua.PushValue(instance, this);
            List<bLuaValuePair> result = Lua.PopDictionaryPairs(instance);

            return result;
        }

        public List<bLuaValue> GetDictionaryKeys()
        {
            if (luaType != LuaType.Table)
            {
                return null;
            }
            
            List<bLuaValue> keys = new List<bLuaValue>();
            
            List<bLuaValuePair> pairs = GetDictionaryPairs();
            if (pairs != null)
            {
                foreach (bLuaValuePair pair in pairs)
                {
                    keys.Add(pair.Key);
                }
            }

            return keys;
        }

        public List<bLuaValue> GetDictionaryValues()
        {
            if (luaType != LuaType.Table)
            {
                return null;
            }
            
            List<bLuaValue> values = new List<bLuaValue>();
            
            List<bLuaValuePair> pairs = GetDictionaryPairs();
            if (pairs != null)
            {
                foreach (bLuaValuePair pair in pairs)
                {
                    values.Add(pair.Value);
                }
            }
            
            return values;
        }
        
        public int GetTableLength()
        {
            if (luaType != LuaType.Table)
            {
                return 0;
            }
            
            return Lua.Length(instance, this);
        }

        public bool GetIsTableEmpty()
        {
            if (luaType != LuaType.Table)
            {
                return false;
            }
            
            Lua.PushValue(instance, this);
            bool result = Lua.PopIsTableEmpty(instance);

            return result;
        }

        public void TableAppend(bLuaValue _value)
        {
            if (luaType != LuaType.Table)
            {
                return;
            }
            
            Lua.AppendArray(instance, this, _value);
        }

        public void TableAppend(object _object)
        {
            if (luaType != LuaType.Table)
            {
                return;
            }
            
            Lua.AppendArray(instance, this, _object);
        }
        
#region Overrides
        public override bool Equals(object a)
        {
            if (a is not bLuaValue other)
            {
                return false;
            }

            if (luaType == LuaType.Nil
                && other.luaType == LuaType.Nil)
            {
                return true;
            }

            if ((instance != other.instance)
                || instance == null
                || other.instance == null)
            {
                // Unable to do a raw equality check via Lua if the values exist in different instances
                return false;
            }

#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(instance.state);
#endif // UNITY_EDITOR

            Lua.PushValue(instance, this);
            Lua.PushValue(instance, other);

            int res = LuaLibAPI.lua_rawequal(instance.state, -1, -2);
            Lua.Pop(instance.state, 2);

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(instance.state));
#endif // UNITY_EDITOR

            return res != 0;
        }

        protected bool Equals(bLuaValue other)
        {
            return Equals(this, other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(referenceId, instance);
        }
#endregion // Overrides
        
#region Static
        public static int NOREF = -2;
        public static int REFNIL = -1;
                
                
        public static bLuaValue Nil = new()
        {
            luaType = LuaType.Nil
        };

        public static bool IsNilOrNull(bLuaValue _value)
        {
            return _value == null || _value.IsNil();
        }
        
        public static bLuaValue CreateNil()
        {
            return Nil;
        }

        public static bLuaValue CreateBool(bLuaInstance _instance, bool _bool)
        {
            Lua.PushObject(_instance, _bool);
            return Lua.PopValue(_instance);
        }

        public static bLuaValue CreateNumber(bLuaInstance _instance, double _double)
        {
            Lua.PushObject(_instance, _double);
            return Lua.PopValue(_instance);
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
                    Assert.AreEqual(entry.key, entry.value.ToString());
                    ++_instance.stringCacheHit;
                    return entry.value;
                }
                else
                {
                    Lua.PushObject(_instance, _string);
                    bLuaValue result = Lua.PopValue(_instance);

                    entry.key = _string;
                    entry.value = result;
                    _instance.stringCache[n] = entry;
                    ++_instance.stringCacheMiss;
                    return result;
                }
            }

            Lua.PushObject(_instance, _string);
            return Lua.PopValue(_instance);
        }

        public static bLuaValue CreateTable(bLuaInstance _instance, int _reserveArray = 0, int _reserveTable = 0)
        {
            Lua.PushNewTable(_instance, _reserveArray, _reserveTable);
            return Lua.PopValue(_instance);
        }

        public static bLuaValue CreateFunction(bLuaInstance _instance, LuaCFunction _fn)
        {
            Lua.PushCFunction(_instance, _instance.state, _fn);
            return Lua.PopValue(_instance);
        }

        public static bLuaValue CreateClosure(bLuaInstance _instance, LuaCFunction _fn, params bLuaValue[] _upvalues)
        {
            Lua.PushClosure(_instance, _instance.state, _fn, _upvalues);
            return Lua.PopValue(_instance);
        }

        public static bLuaValue CreateUserData(bLuaInstance _instance, object _object)
        {
            if (_object == null)
            {
                return Nil;
            }
            Lua.PushNewUserData(_instance, _object);
            return Lua.PopValue(_instance);
        }
#endregion // Static
    }
} // bLua namespace
