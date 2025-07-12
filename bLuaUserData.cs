using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using bLua.NativeLua;
using bLua.Internal;

namespace bLua
{
    public enum UserDataType
    {
        Void,
        UserDataClass,
        LuaValue,
        Bool,
        Double,
        Integer,
        Float,
        String,
        List,
        Dictionary,
        Params,
        Array
    }
    
    public enum UserDataPropertyType
    {
        Method,
        Property,
        Field
    }
    
    public class PropertyEntry
    {
        public UserDataPropertyType propertyType;
        public int index;
    }
    
    public class UserDataRegistryEntry
    {
        public string name;
        public bLuaValue metatable;
        public Dictionary<string, PropertyEntry> properties = new();
    }

    public class MethodCallInfo
    {
        public MethodInfo methodInfo;
        public UserDataType returnType;
        public UserDataType[] argTypes;
        public object[] defaultArgs;
        public bLuaValue closure;
    }

    public class GlobalMethodCallInfo : MethodCallInfo
    {
        public object objectInstance;
    }

    public class DelegateCallInfo : MethodCallInfo
    {
        public MulticastDelegate multicastDelegate;
    }

    public class PropertyCallInfo
    {
        public PropertyInfo propertyInfo;
        public UserDataType propertyType;
    }

    public class FieldCallInfo
    {
        public FieldInfo fieldInfo;
        public UserDataType fieldType;
    }

    public static class bLuaUserData
    {
        /// <summary>
        /// Searches all assemblies for types marked with the <see cref="bLuaUserDataAttribute"/> attribute and registers them to the given instance.
        /// </summary>
        public static void RegisterAllBLuaUserData(bLuaInstance _instance)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                RegisterAssemblyBLuaUserData(_instance, asm);
            }
        }

        private static void RegisterAssemblyBLuaUserData(bLuaInstance _instance, Assembly _assembly)
        {
            foreach (TypeInfo t in _assembly.DefinedTypes)
            {
                if (t.IsClass && IsUserData(t))
                {
                    Register(_instance, t);
                }
            }
        }
        
        /// <summary>
        /// Registers a type as Lua userdata. Does not require the <see cref="bLuaUserDataAttribute"/> attribute unless specified.
        /// </summary>
        public static void Register(bLuaInstance _instance, Type _type)
        {
            if (_type.IsUserDataRegistered(_instance))
            {
                // Can't register the same type multiple times.
                return;
            }

            bool isStaticClass = _type.IsClass && _type.IsAbstract && _type.IsSealed;

            Dictionary<string, PropertyEntry> baseProperties = new Dictionary<string, PropertyEntry>();
            if (_type.IsClass && !isStaticClass && _type.BaseType != null && _type.BaseType != _type)
            {
                if (_type.BaseType.IsClass && !_type.BaseType.IsUserDataRegistered(_instance))
                {
                    Register(_instance, _type.BaseType);
                }

                if (_instance.typenameToEntryIndex.TryGetValue(_type.BaseType.Name, out int value))
                {
                    baseProperties = new Dictionary<string, PropertyEntry>(_instance.registeredEntries[value].properties);
                }
            }

            bLuaUserDataAttribute attribute = _type.GetCustomAttribute<bLuaUserDataAttribute>();
            if (attribute != null
                && attribute.reliantUserData != null
                && attribute.reliantUserData.Length > 0)
            {
                foreach (Type t in attribute.reliantUserData)
                {
                    Register(_instance, t);
                }
            }

            int typeIndex = _instance.registeredEntries.Count;

            UserDataRegistryEntry entry = new UserDataRegistryEntry
            {
                name = _type.Name,
                properties = baseProperties,
                metatable = Lua.NewMetaTable(_instance, _type.Name)
            };
            entry.metatable.Set("__index",    bLuaValue.CreateClosure(_instance, bLuaMetamethods.Metamethod_Index,    bLuaValue.CreateNumber(_instance, typeIndex)));
            entry.metatable.Set("__newindex", bLuaValue.CreateClosure(_instance, bLuaMetamethods.Metamethod_NewIndex, bLuaValue.CreateNumber(_instance, typeIndex)));
            entry.metatable.Set("__gc",       bLuaValue.CreateClosure(_instance, bLuaMetamethods.MetaMethod_GC,       bLuaValue.CreateNumber(_instance, typeIndex)));
            foreach (string[] metamethod in bLuaMetamethods.metamethodCollection)
            {
                if (_type.GetMethods().FirstOrDefault((mi) => mi.Name == metamethod[0]) != null)
                {
                    entry.metatable.Set(metamethod[1],
                        bLuaValue.CreateClosure(_instance,
                            bLuaMetamethods.Metamethod_Operator,
                            bLuaValue.CreateNumber(_instance, typeIndex),
                            bLuaValue.CreateString(_instance, metamethod[0]),   // method name string
                            bLuaValue.CreateString(_instance, metamethod[2]))); // error string
                }
            }
            if (_type.GetMethod("ToString") != null)
            {
                entry.metatable.Set("__concat",   bLuaValue.CreateClosure(_instance, bLuaMetamethods.Metamethod_Concatenation, bLuaValue.CreateNumber(_instance, typeIndex)));
                entry.metatable.Set("__tostring", bLuaValue.CreateClosure(_instance, bLuaMetamethods.Metamethod_ToString,      bLuaValue.CreateNumber(_instance, typeIndex)));
            }

            _instance.typenameToEntryIndex[_type.Name] = typeIndex;

            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            if (!_type.IsBaseTypeRegistered(_instance))
            {
                bindingFlags |= BindingFlags.DeclaredOnly;
            }

            MethodInfo[] methods = _type.GetMethods(bindingFlags);
            foreach (MethodInfo methodInfo in methods)
            {
                Attribute hiddenAttr = methodInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                ParameterInfo[] methodParams = methodInfo.GetParameters();

                bool isExtensionMethod = methodInfo.IsDefined(typeof(ExtensionAttribute), true);
                if (isExtensionMethod && !methodParams[0].ParameterType.IsUserDataRegistered(_instance))
                {
                    Debug.LogError($"Tried to register extension method ({methodInfo.Name}) but the type it extends ({methodParams[0].ParameterType.Name}) isn't registered.");
                    continue;
                }

                List<UserDataType> argTypes = new();
                List<object> defaultArgs = new();
                for (int i = 0; i < methodParams.Length; ++i)
                {
                    if (methodParams[i].GetCustomAttributes().FirstOrDefault(a => a is bLuaParam_Ignored) != null)
                    {
                        continue;
                    }

                    UserDataType paramType = methodParams[i].ParameterType.ToUserDataType(_instance);
                    if (i == methodParams.Length - 1 && methodParams[i].GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
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

                UserDataType returnType = methodInfo.ReturnType.ToUserDataType(_instance);

                PropertyEntry propertyEntry = new PropertyEntry()
                {
                    propertyType = UserDataPropertyType.Method,
                    index = _instance.registeredMethods.Count,
                };

                if (isExtensionMethod)
                {
                    if (_instance.typenameToEntryIndex.TryGetValue(methodParams[0].ParameterType.Name, out int extensionTypeIndex) == false)
                    {
                        Debug.LogError($"Tried to register extension method ({methodInfo.Name}) but the type it extends ({methodParams[0].ParameterType.Name}) isn't registered.");
                        continue;
                    }
                    UserDataRegistryEntry extensionTypeEntry = _instance.registeredEntries[extensionTypeIndex];
                    _instance.registeredEntries[extensionTypeIndex].properties[methodInfo.Name] = propertyEntry;
                }
                else
                {
                    entry.properties[methodInfo.Name] = propertyEntry;
                }

                LuaCFunction fn;
                if (methodInfo.IsStatic || isExtensionMethod)
                {
                    fn = Lua.CallStaticUserDataFunction;
                }
                else
                {
                    fn = Lua.CallUserDataFunction;
                }

                _instance.registeredMethods.Add(new MethodCallInfo()
                {
                    methodInfo = methodInfo,
                    returnType = returnType,
                    argTypes = argTypes.ToArray(),
                    defaultArgs = defaultArgs.ToArray(),
                    closure = bLuaValue.CreateClosure(_instance, fn, bLuaValue.CreateNumber(_instance, _instance.registeredMethods.Count)),
                });
            }

            PropertyInfo[] properties = _type.GetProperties(bindingFlags);
            foreach (PropertyInfo propertyInfo in properties)
            {
                Attribute hiddenAttr = propertyInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                UserDataType returnType = propertyInfo.PropertyType.ToUserDataType(_instance);
                if (returnType == UserDataType.Void)
                {
                    Debug.LogWarning($"Failed to register property {propertyInfo.Name} on {_type.Name} because its type ({propertyInfo.PropertyType}) is not registered.");
                    continue;
                }

                entry.properties[propertyInfo.Name] = new PropertyEntry()
                {
                    propertyType = UserDataPropertyType.Property,
                    index = _instance.registeredProperties.Count,
                };

                _instance.registeredProperties.Add(new PropertyCallInfo()
                {
                    propertyInfo = propertyInfo,
                    propertyType = returnType,
                });
            }

            FieldInfo[] fields = _type.GetFields(bindingFlags);
            foreach (FieldInfo fieldInfo in fields)
            {
                Attribute hiddenAttr = fieldInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                UserDataType returnType = fieldInfo.FieldType.ToUserDataType(_instance);
                if (returnType == UserDataType.Void)
                {
                    Debug.LogWarning($"Failed to register field {fieldInfo.Name} on {_type.Name} because its type ({fieldInfo.FieldType}) is not registered.");
                    continue;
                }

                entry.properties[fieldInfo.Name] = new PropertyEntry()
                {
                    propertyType = UserDataPropertyType.Field,
                    index = _instance.registeredFields.Count,
                };

                _instance.registeredFields.Add(new FieldCallInfo()
                {
                    fieldInfo = fieldInfo,
                    fieldType = returnType,
                });
            }

            _instance.registeredEntries.Add(entry);
        }

        /// <summary>
        /// Registers all methods on a class as global functions in Lua. If _environment is null, the methods will be registered
        /// as global in the global environment.
        /// </summary>
        public static void RegisterAllMethodsAsGlobal(bLuaInstance _instance, object _object, bLuaValue _environment = null)
        {
            if (_object == null)
            {
                Debug.LogError("Can't register all methods as global, object is null");
                return;
            }

            if (_environment == null)
            {
                Debug.LogError("Can't register all methods as global, environment is null");
                return;
            }

            MethodInfo[] methods = _object.GetType().GetMethods(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static);
            foreach (MethodInfo method in methods)
            {
                GlobalMethodCallInfo info = CreateGlobalMethodCallInfo(_instance, method, _object);
                if (info == null)
                {
                    continue;
                }

                _environment.Set(method.Name, info);
            }
        }

        private static GlobalMethodCallInfo CreateGlobalMethodCallInfo(bLuaInstance _instance, MethodInfo _methodInfo, object _object)
        {
            Attribute hiddenAttr = _methodInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
            if (hiddenAttr != null)
            {
                return null;
            }

            ParameterInfo[] methodParams = _methodInfo.GetParameters();

            bool isExtensionMethod = _methodInfo.IsDefined(typeof(ExtensionAttribute), true);
            if (isExtensionMethod)
            {
                Debug.LogError($"Tried to register extension method ({_methodInfo.Name}) as a global method. This is not allowed.");
                return null;
            }

            List<UserDataType> argTypes = new();
            List<object> defaultArgs = new();
            for (int i = 0; i < methodParams.Length; ++i)
            {
                if (methodParams[i].GetCustomAttributes().FirstOrDefault(a => a is bLuaParam_Ignored) != null)
                {
                    continue;
                }

                UserDataType paramType = methodParams[i].ParameterType.ToUserDataType(_instance);
                if (i == methodParams.Length - 1 && methodParams[i].GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
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

            UserDataType returnType = _methodInfo.ReturnType.ToUserDataType(_instance);

            GlobalMethodCallInfo methodCallInfo = new GlobalMethodCallInfo()
            {
                methodInfo = _methodInfo,
                returnType = returnType,
                argTypes = argTypes.ToArray(),
                defaultArgs = defaultArgs.ToArray(),
                objectInstance = _object
            };

            return methodCallInfo;
        }
        
        public static UserDataType ToUserDataType(this Type _type, bLuaInstance _instance)
        {
            if (_type == typeof(void))
            {
                return UserDataType.Void;
            }
            else if (_type == typeof(int))
            {
                return UserDataType.Integer;
            }
            else if (_type == typeof(double))
            {
                return UserDataType.Double;
            }
            else if (_type == typeof(float))
            {
                return UserDataType.Float;
            }
            else if (_type == typeof(string))
            {
                return UserDataType.String;
            }
            else if (_type == typeof(bool))
            {
                return UserDataType.Bool;
            }
            else if (_type.IsArray)
            {
                return UserDataType.Array;
            }
            else if (_type.IsGenericType && (_type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                return UserDataType.List;
            }
            else if (_type.IsGenericType && (_type.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
            {
                return UserDataType.Dictionary;
            }
            else if (_type == typeof(bLuaValue))
            {
                return UserDataType.LuaValue;
            }
            else if (_type.IsClass && (_type.IsUserDataRegistered(_instance) || IsUserData(_type)))
            {
                return UserDataType.UserDataClass;
            }
            else if (_type.IsStruct() && (_type.IsUserDataRegistered(_instance) || IsUserData(_type)))
            {
                return UserDataType.UserDataClass;
            }
            else
            {
                return UserDataType.Void;
            }
        }
        
        public static bool IsUserData(this Type _type)
        {
            return _type.GetCustomAttribute(typeof(bLuaUserDataAttribute)) != null;
        }

        public static bool IsUserDataRegistered(this Type _type, bLuaInstance _instance)
        {
            return _instance.typenameToEntryIndex.ContainsKey(_type.Name);
        }

        private static bool IsBaseTypeRegistered(this Type _type, bLuaInstance _instance)
        {
            if (!_type.IsClass)
            {
                return true;
            }

            Type checkingType = _type;
            while (checkingType != null && checkingType != checkingType.BaseType && checkingType.IsClass)
            {
                if (!checkingType.IsUserDataRegistered(_instance))
                {
                    return false;
                }
                checkingType = checkingType.BaseType;
            }
            return true;
        }

        public static bool IsStruct(this Type _type)
        {
            return _type.IsValueType && !_type.IsEnum;
        }
        
        public static bool IsTuple(this Type _type)
        {
            if (!_type.IsGenericType)
            {
                return false;
            }
            
            Type genericType = _type.GetGenericTypeDefinition();
            if (genericType == null)
            {
                return false;
            }

            if (genericType == typeof(ValueTuple<>)
                || genericType == typeof(ValueTuple<,>)
                || genericType == typeof(ValueTuple<,,>)
                || genericType == typeof(ValueTuple<,,,>)
                || genericType == typeof(ValueTuple<,,,,>)
                || genericType == typeof(ValueTuple<,,,,,>)
                || genericType == typeof(ValueTuple<,,,,,,>)
                || genericType == typeof(ValueTuple<,,,,,,,>)
                || genericType == typeof(Tuple<>)
                || genericType == typeof(Tuple<,>)
                || genericType == typeof(Tuple<,,>)
                || genericType == typeof(Tuple<,,,>)
                || genericType == typeof(Tuple<,,,,>)
                || genericType == typeof(Tuple<,,,,,>)
                || genericType == typeof(Tuple<,,,,,,>)
                || genericType == typeof(Tuple<,,,,,,,>))
            {
                return true;
            }

            return false;
        }
    }
} // bLua namespace
