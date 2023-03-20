using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using bLua.NativeLua;

namespace bLua
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
    public class bLuaHiddenAttribute : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Class)]
    public class bLuaUserDataAttribute : Attribute
    {

    }

    public class UserDataRegistryEntry
    {
        public bLuaValue metatable;

        public class PropertyEntry
        {
            public enum Type
            {
                Method,
                Property,
                Field,
            };
            public Type propertyType;
            public int index;
        }

        public Dictionary<string, PropertyEntry> properties = new Dictionary<string, PropertyEntry>();
    }

    public class MethodCallInfo
    {
        public MethodInfo methodInfo;

        public enum ParamType
        {
            Void,
            UserDataClass,
            LuaValue,
            Int,
            Str,
            Double,
            Bool,
            Float,
            Params
        }

        public ParamType returnType;

        public ParamType[] argTypes;
        public object[] defaultArgs;

        public bLuaValue closure;
    }

    public class DelegateCallInfo : MethodCallInfo
    {
        public MulticastDelegate multicastDelegate;
    }

    public class PropertyCallInfo
    {
        public PropertyInfo propertyInfo;

        public MethodCallInfo.ParamType propertyType;
    }

    public class FieldCallInfo
    {
        public FieldInfo fieldInfo;

        public MethodCallInfo.ParamType fieldType;
    }

    public static class bLuaUserData
    {


        public static void PushReturnTypeOntoStack(bLuaInstance _instance, MethodCallInfo.ParamType _returnType, object _result)
        {
            switch (_returnType)
            {
                case MethodCallInfo.ParamType.Void:
                    Lua.PushNil(_instance);
                    return;
                case MethodCallInfo.ParamType.LuaValue:
                    Lua.PushStack(_instance, _result as bLuaValue);
                    return;
                case MethodCallInfo.ParamType.Bool:
                    Lua.PushOntoStack(_instance, (bool)_result);
                    return;
                case MethodCallInfo.ParamType.Int:
                    Lua.PushOntoStack(_instance, (int)_result);
                    return;
                case MethodCallInfo.ParamType.Float:
                    Lua.PushOntoStack(_instance, (float)_result);
                    return;
                case MethodCallInfo.ParamType.Double:
                    Lua.PushOntoStack(_instance, (double)_result);
                    return;
                case MethodCallInfo.ParamType.Str:
                    Lua.PushOntoStack(_instance, (string)_result);
                    return;
                case MethodCallInfo.ParamType.UserDataClass:
                    PushNewUserData(_instance, _result);
                    return;
                default:
                    Lua.PushNil(_instance);
                    return;
            }
        }

        public static object GetUserDataObject(bLuaInstance _instance, int _nstack)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            int ntype = LuaLibAPI.lua_getiuservalue(_instance.state, _nstack, 1);
            if (ntype != (int)DataType.Number)
            {
                _instance.Error($"Could not find valid user data object");
                Lua.PopStack(_instance);
                return null;
            }

            int liveObjectIndex = LuaLibAPI.lua_tointegerx(_instance.state, -1, IntPtr.Zero);

            object obj = _instance.s_liveObjects[liveObjectIndex];

            Lua.PopStack(_instance);

            return obj;
        }

#if UNITY_EDITOR
        struct DebugEntry
        {
            public int count;
            public string typeName;
        }

        public static void DebugInfoReport(bLuaInstance _instance)
        {
            List<DebugEntry> entries = new List<DebugEntry>();
            Dictionary<string, int> types = new Dictionary<string, int>();
            foreach (object p in _instance.s_liveObjects)
            {
                if (p == null)
                {
                    continue;
                }
                string typeName = p.GetType().Name;
                int count = 0;
                if (types.ContainsKey(typeName))
                {
                    count = types[typeName];
                }

                ++count;
                types[typeName] = count;
            }

            foreach (KeyValuePair<string, int> p in types)
            {
                entries.Add(new DebugEntry()
                {
                    typeName = p.Key,
                    count = p.Value,
                });
            }

            entries.Sort((a,b) =>
            {
                return a.count.CompareTo(b.count);
            });

            foreach (DebugEntry entry in entries)
            {
                Debug.Log($"LiveObject: {entry.typeName} -> {entry.count}");
            }
        }
#endif

        public static void PushNewUserData(bLuaInstance _instance, object _object)
        {
            if (_object == null)
            {
                Lua.PushNil(_instance);
                return;
            }
            int typeIndex;
            if (_instance.s_typenameToEntryIndex.TryGetValue(_object.GetType().Name, out typeIndex) == false)
            {
                _instance.Error($"Type {_object.GetType().Name} is not marked as a user data. Add [bLuaUserData] to its definition.");
                Lua.PushNil(_instance);
                return;
            }

            UserDataRegistryEntry entry = _instance.s_entries[typeIndex];

            // Work out the index of the new object
            int objIndex;
            if (_instance.s_liveObjectsFreeList.Count > 0)
            {
                objIndex = _instance.s_liveObjectsFreeList[_instance.s_liveObjectsFreeList.Count - 1];
                _instance.s_liveObjectsFreeList.RemoveAt(_instance.s_liveObjectsFreeList.Count - 1);
            }
            else
            {
                if (_instance.s_nNextLiveObject >= _instance.s_liveObjects.Length)
                {
                    object[] liveObjects = new object[_instance.s_liveObjects.Length * 2];
                    for (int i = 0; i != _instance.s_liveObjects.Length; ++i)
                    {
                        liveObjects[i] = _instance.s_liveObjects[i];
                    }

                    _instance.s_liveObjects = liveObjects;
                }

                objIndex = _instance.s_nNextLiveObject;
                _instance.s_nNextLiveObject++;
            }

            LuaLibAPI.lua_newuserdatauv(_instance.state, new IntPtr(8), 1);
            Lua.PushOntoStack(_instance, objIndex);
            LuaLibAPI.lua_setiuservalue(_instance.state, -2, 1);
            Lua.PushStack(_instance, entry.metatable);
            LuaLibAPI.lua_setmetatable(_instance.state, -2);

            string msg = Lua.TraceMessage(_instance, "live object");

            _instance.s_liveObjects[objIndex] = _object;
        }

        public static void RegisterAllAssemblies(bLuaInstance _instance)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                RegisterAssembly(_instance, asm);
            }
        }

        public static void RegisterAssembly(bLuaInstance _instance, Assembly _assembly)
        {
            foreach (TypeInfo t in _assembly.DefinedTypes)
            {
                if (t.IsClass && t.GetCustomAttribute(typeof(bLuaUserDataAttribute)) != null) {
                    Register(_instance, t);
                }
            }
        }

        public static void Register(bLuaInstance _instance, Type _type)
        {
            if (_instance.s_typenameToEntryIndex.ContainsKey(_type.Name))
            {
                // Can't register the same type multiple times.
                return;
            }

            Dictionary<string, UserDataRegistryEntry.PropertyEntry> baseProperties = new Dictionary<string, UserDataRegistryEntry.PropertyEntry>();

            if (_type.BaseType != null && _type.BaseType != _type)
            {
                if (_type.BaseType.IsClass && _type.BaseType.GetCustomAttribute(typeof(bLuaUserDataAttribute)) != null)
                {
                    Register(_instance, _type.BaseType);

                    baseProperties = new Dictionary<string, UserDataRegistryEntry.PropertyEntry>(_instance.s_entries[_instance.s_typenameToEntryIndex[_type.BaseType.Name]].properties);
                }
            }

            int typeIndex = _instance.s_entries.Count;

            UserDataRegistryEntry entry = new UserDataRegistryEntry()
            {
                properties = baseProperties,
            };
            entry.metatable = Lua.NewMetaTable(_instance, _type.Name);
            entry.metatable.Set("__index", bLuaValue.CreateClosure(_instance, bLuaInstance.IndexFunction, bLuaValue.CreateNumber(_instance, typeIndex)));
            entry.metatable.Set("__newindex", bLuaValue.CreateClosure(_instance, bLuaInstance.SetIndexFunction, bLuaValue.CreateNumber(_instance, typeIndex)));
            entry.metatable.Set("__gc", _instance._gc);

            _instance.s_typenameToEntryIndex[_type.Name] = typeIndex;

            _instance.s_entries.Add(entry);

            MethodInfo[] methods = _type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            foreach (MethodInfo methodInfo in methods)
            {
                Attribute hiddenAttr = methodInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                ParameterInfo[] methodParams = methodInfo.GetParameters();

                MethodCallInfo.ParamType[] argTypes = new MethodCallInfo.ParamType[methodParams.Length];
                object[] defaultArgs = new object[methodParams.Length];
                for (int i = 0; i != methodParams.Length; ++i)
                {
                    argTypes[i] = SystemTypeToParamType(methodParams[i].ParameterType);
                    if (i == methodParams.Length - 1 && methodParams[i].GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
                    {
                        argTypes[i] = MethodCallInfo.ParamType.Params;
                    }

                    if (methodParams[i].HasDefaultValue)
                    {
                        defaultArgs[i] = methodParams[i].DefaultValue;
                    }
                    else if (argTypes[i] == MethodCallInfo.ParamType.LuaValue)
                    {
                        defaultArgs[i] = bLuaValue.Nil;
                    }
                    else
                    {
                        defaultArgs[i] = null;
                    }
                }

                MethodCallInfo.ParamType returnType = SystemTypeToParamType(methodInfo.ReturnType);

                entry.properties[methodInfo.Name] = new UserDataRegistryEntry.PropertyEntry()
                {
                    propertyType = UserDataRegistryEntry.PropertyEntry.Type.Method,
                    index = _instance.s_methods.Count,
                };

                LuaCFunction fn;
                if (methodInfo.IsStatic)
                {
                    fn = bLuaInstance.CallStaticUserDataFunction;
                }
                else
                {
                    fn = bLuaInstance.CallUserDataFunction;
                }

                _instance.s_methods.Add(new MethodCallInfo()
                {
                    methodInfo = methodInfo,
                    returnType = returnType,
                    argTypes = argTypes,
                    defaultArgs = defaultArgs,
                    closure = bLuaValue.CreateClosure(_instance, fn, bLuaValue.CreateNumber(_instance, _instance.s_methods.Count)),
                });
            }

            PropertyInfo[] properties = _type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (PropertyInfo propertyInfo in properties)
            {
                Attribute hiddenAttr = propertyInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                MethodCallInfo.ParamType returnType = SystemTypeToParamType(propertyInfo.PropertyType);
                if (returnType == MethodCallInfo.ParamType.Void)
                {
                    continue;
                }

                entry.properties[propertyInfo.Name] = new UserDataRegistryEntry.PropertyEntry()
                {
                    propertyType = UserDataRegistryEntry.PropertyEntry.Type.Property,
                    index = _instance.s_properties.Count,
                };

                _instance.s_properties.Add(new PropertyCallInfo()
                {
                    propertyInfo = propertyInfo,
                    propertyType = returnType,
                });
            }

            FieldInfo[] fields = _type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo fieldInfo in fields)
            {
                Attribute hiddenAttr = fieldInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                MethodCallInfo.ParamType returnType = SystemTypeToParamType(fieldInfo.FieldType);
                if (returnType == MethodCallInfo.ParamType.Void)
                {
                    continue;
                }

                entry.properties[fieldInfo.Name] = new UserDataRegistryEntry.PropertyEntry()
                {
                    propertyType = UserDataRegistryEntry.PropertyEntry.Type.Field,
                    index = _instance.s_fields.Count,
                };

                _instance.s_fields.Add(new FieldCallInfo()
                {
                    fieldInfo = fieldInfo,
                    fieldType = returnType,
                });
            }
        }

        public static object PopStackIntoParamType(bLuaInstance _instance, MethodCallInfo.ParamType _paramType)
        {
            switch (_paramType)
            {
                case MethodCallInfo.ParamType.Bool:
                    return Lua.PopBool(_instance);
                case MethodCallInfo.ParamType.Double:
                    return Lua.PopNumber(_instance);
                case MethodCallInfo.ParamType.Float:
                    return (float)Lua.PopNumber(_instance);
                case MethodCallInfo.ParamType.Int:
                    return Lua.PopInteger(_instance);
                case MethodCallInfo.ParamType.Str:
                    return Lua.PopString(_instance);
                case MethodCallInfo.ParamType.LuaValue:
                    return Lua.PopStackIntoValue(_instance);
                case MethodCallInfo.ParamType.UserDataClass:
                    return Lua.PopStackIntoValue(_instance);
                default:
                    Lua.PopStack(_instance);
                    return null;
            }
        }

        public static MethodCallInfo.ParamType SystemTypeToParamType(Type _type)
        {
            if (_type == typeof(void))
            {
                return MethodCallInfo.ParamType.Void;
            }
            else if (_type == typeof(int))
            {
                return MethodCallInfo.ParamType.Int;
            }
            else if (_type == typeof(double))
            {
                return MethodCallInfo.ParamType.Double;
            }
            else if (_type == typeof(float))
            {
                return MethodCallInfo.ParamType.Float;
            }
            else if (_type == typeof(string))
            {
                return MethodCallInfo.ParamType.Str;
            }
            else if (_type == typeof(bool))
            {
                return MethodCallInfo.ParamType.Bool;
            }
            else if (_type == typeof(bLuaValue))
            {
                return MethodCallInfo.ParamType.LuaValue;
            }
            else if (_type.IsClass && _type.GetCustomAttribute(typeof(bLuaUserDataAttribute)) != null)
            {
                return MethodCallInfo.ParamType.UserDataClass;
            }
            else
            {
                return MethodCallInfo.ParamType.Void;
            }
        }
    }
} // bLua namespace
