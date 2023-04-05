using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;
using bLua.NativeLua;
using System.Runtime.CompilerServices;

namespace bLua.Internal
{
    public static class bLuaMetamethods
    {
        public const string csharp_operator_addition = "op_Addition";
        public const string csharp_operator_subtraction = "op_Subtraction";
        public const string csharp_operator_multiplication = "op_Multiply";
        public const string csharp_operator_division = "op_Division";
        public const string csharp_operator_unaryNegation = "op_UnaryNegation";
        public const string csharp_operator_equality = "op_Equality";
        public const string csharp_operator_lessThan = "op_LessThan";
        public const string csharp_operator_lessThanOrEqual = "op_LessThanOrEqual";
        public const string lua_metamethod_addition = "__add";
        public const string lua_metamethod_subtraction = "__sub";
        public const string lua_metamethod_multiplication = "__mul";
        public const string lua_metamethod_division = "__div";
        public const string lua_metamethod_unaryNegation = "__unm";
        public const string lua_metamethod_equality = "__eq";
        public const string lua_metamethod_lessThan = "__lt";
        public const string lua_metamethod_lessThanOrEqual = "__le";
        public static string[][] metamethodCollection = new string[][]
        {
            new string[] { csharp_operator_addition,        lua_metamethod_addition,        bLuaError.error_operationAddition },
            new string[] { csharp_operator_subtraction,     lua_metamethod_subtraction,     bLuaError.error_operationSubtraction },
            new string[] { csharp_operator_multiplication,  lua_metamethod_multiplication,  bLuaError.error_operationMultiply },
            new string[] { csharp_operator_division,        lua_metamethod_division,        bLuaError.error_operationDivision },
            new string[] { csharp_operator_unaryNegation,   lua_metamethod_unaryNegation,   bLuaError.error_operationUnaryNegation },
            new string[] { csharp_operator_equality,        lua_metamethod_equality,        bLuaError.error_operationEquality },
            new string[] { csharp_operator_lessThan,        lua_metamethod_lessThan,        bLuaError.error_operationLessThan },
            new string[] { csharp_operator_lessThanOrEqual, lua_metamethod_lessThanOrEqual, bLuaError.error_operationLessThanOrEqual }
        };


        #region Helper functions
        static bool GetUserDataEntry(IntPtr _originalState, bLuaInstance _instance, out UserDataRegistryEntry _userDataEntry)
        {
            _userDataEntry = new UserDataRegistryEntry();

            int n = LuaLibAPI.lua_tointegerx(_originalState, Lua.UpValueIndex(1), IntPtr.Zero);
            if (n < 0 || n >= _instance.registeredEntries.Count)
            {
                _instance.Error($"{bLuaError.error_invalidTypeIndex}{n}");
                return false;
            }

            _userDataEntry = _instance.registeredEntries[n];
            return true;
        }

        static bool GetUserDataPropertyEntry(IntPtr _originalState, bLuaInstance _instance, UserDataRegistryEntry _userDataEntry, out UserDataRegistryEntry.PropertyEntry _userDataPropertyEntry, out string _propertyName)
        {
            _propertyName = Lua.GetString(_originalState, 2);
            if (!_userDataEntry.properties.TryGetValue(_propertyName, out _userDataPropertyEntry))
            {
                _instance.Error($"{bLuaError.error_invalidProperty}{_propertyName}");
                return false;
            }

            return true;
        }

        static bool GetMethodCallInfo(bLuaInstance _instance, UserDataRegistryEntry _userDataEntry, string _methodName,  out MethodCallInfo _methodCallInfo)
        {
            _methodCallInfo = new MethodCallInfo();

            UserDataRegistryEntry.PropertyEntry propertyEntry;
            if (!_userDataEntry.properties.TryGetValue(_methodName, out propertyEntry))
            {
                _instance.Error($"{bLuaError.error_invalidMethod}{_methodName}");
                return false;
            }

            if (propertyEntry.index < 0 || propertyEntry.index >= _instance.registeredMethods.Count)
            {
                _instance.Error($"{bLuaError.error_invalidMethod}{_methodName}");
                return false;
            }

            _methodCallInfo = _instance.registeredMethods[propertyEntry.index];
            return true;
        }

        static bool GetLiveObjectIndex(IntPtr _originalState, bLuaInstance _instance, out int _liveObjectIndex)
        {
            _liveObjectIndex = -1;

            int t = LuaLibAPI.lua_type(_originalState, 1);
            if (t != (int)DataType.UserData)
            {
                _instance.Error($"{bLuaError.error_objectIsNotUserdata}{(DataType)t}");
                return false;
            }

            LuaLibAPI.lua_checkstack(_originalState, 1);
            int res = LuaLibAPI.lua_getiuservalue(_originalState, 1, 1);
            if (res != (int)DataType.Number)
            {
                _instance.Error($"{bLuaError.error_objectNotProvided}");
                return false;
            }

            _liveObjectIndex = Lua.PopInteger(_instance);
            return true;
        }

        static bool GetLiveObjectInstance(IntPtr _originalState, bLuaInstance _instance, out object _object)
        {
            _object = null;

            int liveObjectIndex;
            if (!GetLiveObjectIndex(_originalState, _instance, out liveObjectIndex))
            {
                return false;
            }
            
            _object = _instance.liveObjects[liveObjectIndex];
            return true;
        }

        static bool GetMethodWithParams(object _userdataObject, string _methodName, out MethodInfo _method, params Type[] _paramTypeRequirementsOrdered)
        {
            _method = null;

            // Get all methods that have the expected name
            MethodInfo[] methodInfos = _userdataObject.GetType().GetMethods().Where((mi) => mi.Name == _methodName).ToArray();
            for (int m = 0; m < methodInfos.Length; m++)
            {
                ParameterInfo[] parameterInfos = methodInfos[m].GetParameters();

                bool parameterRequirementsMatch = true;
                for (int p = 0; p < parameterInfos.Length; p++)
                {
                    // if there isn't a requirement for this param, skip the type check
                    if (p >= _paramTypeRequirementsOrdered.Length)
                    {
                        continue;
                    }

                    // null means any type in this case, skip the type check
                    if (_paramTypeRequirementsOrdered[p] == null)
                    {
                        continue;
                    }

                    if (_paramTypeRequirementsOrdered[p] != parameterInfos[p].ParameterType)
                    {
                        parameterRequirementsMatch = false;
                        break;
                    }
                }
                if (!parameterRequirementsMatch)
                {
                    continue;
                }

                _method = methodInfos[m];
                return true;
            }

            return false;
        }

        static bool GetCallMetamethodUpvalues(IntPtr _originalState, bLuaInstance _instance, out MethodCallInfo _methodInfo, out object _liveObject)
        {
            _methodInfo = null;
            _liveObject = null;

            int m = LuaLibAPI.lua_tointegerx(_originalState, Lua.UpValueIndex(1), IntPtr.Zero);
            if (m < 0 || m >= _instance.registeredMethods.Count)
            {
                _instance.Error($"{bLuaError.error_invalidMethodIndex}{m}");
                return false;
            }

            int l = LuaLibAPI.lua_tointegerx(_originalState, Lua.UpValueIndex(2), IntPtr.Zero);
            if (l < 0 || l >= _instance.liveObjects.Length)
            {
                _instance.Error($"{bLuaError.error_invalidLiveObjectIndex}{m}");
                return false;
            }

            _methodInfo = _instance.registeredMethods[m];
            _liveObject = _instance.liveObjects[l];

            return true;
        }

        static bool PushSyntacticSugarProxy(bLuaInstance _instance, int _methodIndex, int _liveObjectIndex)
        {
            if (_liveObjectIndex < 0 || _liveObjectIndex > _instance.syntaxSugarProxies.Length)
            {
                _instance.Error($"{bLuaError.error_invalidLiveObjectIndex}{_liveObjectIndex}");
                return false;
            }

            LuaLibAPI.lua_newuserdatauv(_instance.state, new IntPtr(8), 1);
            object syntaxSugarProxy = Lua.PopStackIntoObject(_instance);
            Lua.PushOntoStack(_instance, syntaxSugarProxy);

            _instance.syntaxSugarProxies[_liveObjectIndex] = syntaxSugarProxy;

            bLuaValue metatable = Lua.NewMetaTable(_instance, _liveObjectIndex.ToString());
            metatable.Set("__call", bLuaValue.CreateClosure(
                _instance,
                bLuaMetamethods.Metamethod_Call,
                bLuaValue.CreateNumber(_instance, _methodIndex),
                bLuaValue.CreateNumber(_instance, _liveObjectIndex)));

            Lua.PushOntoStack(_instance, _instance.syntaxSugarProxies[_liveObjectIndex]);
            Lua.PushOntoStack(_instance, _methodIndex);
            LuaLibAPI.lua_setiuservalue(_instance.state, -2, 1);
            Lua.PushStack(_instance, metatable);
            LuaLibAPI.lua_setmetatable(_instance.state, -2);

            return true;
        }

        static bool PopStackIntoArgs(bLuaInstance _instance, MethodCallInfo _methodInfo, object _liveObject, out object[] _args)
        {
            bool isExtensionMethod = _methodInfo.methodInfo.IsDefined(typeof(ExtensionAttribute), true);

            _args = new object[_methodInfo.argTypes.Length];
            if (isExtensionMethod)
            {
                _args[0] = _liveObject;
            }
            for (int p = _methodInfo.argTypes.Length - (isExtensionMethod ? 2 : 1); p >=0 ; p--)
            {
                _args[p] = bLuaUserData.PopStackIntoParamType(_instance, _methodInfo.argTypes[p]);
            }

            return true;
        }
        #endregion // Helper functions

        public static int Metamethod_Call(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                MethodCallInfo methodInfo;
                object liveObject;
                if (!GetCallMetamethodUpvalues(_state, mainThreadInstance, out methodInfo, out liveObject))
                {
                    mainThreadInstance.Error($"{bLuaError.error_inMetamethodCall}nil");
                    return 0;
                }

                object[] args;
                if (!PopStackIntoArgs(mainThreadInstance, methodInfo, liveObject, out args))
                {
                    mainThreadInstance.Error($"{bLuaError.error_inMetamethodCall}nil");
                    return 0;
                }

                object result = methodInfo.methodInfo.Invoke(liveObject, args);
                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, methodInfo.returnType, result);
                return 1;
            }
            catch (Exception e)
            {
                mainThreadInstance.ExceptionError(e, bLuaError.error_objectNotProvided);
                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int Metamethod_Index(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                UserDataRegistryEntry userDataInfo;
                if (!GetUserDataEntry(_state, mainThreadInstance, out userDataInfo))
                {
                    return 0;
                }

                string propertyName;
                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (!GetUserDataPropertyEntry(_state, mainThreadInstance, userDataInfo, out propertyEntry, out propertyName))
                {
                    return 0;
                }

                switch (propertyEntry.propertyType)
                {
                    case UserDataRegistryEntry.PropertyEntry.Type.Method:
                        {
                            if (mainThreadInstance.FeatureEnabled(Feature.ImplicitSyntaxSugar))
                            {
                                int liveObjectIndex;
                                if (!GetLiveObjectIndex(_state, mainThreadInstance, out liveObjectIndex))
                                {
                                    return 0;
                                }

                                if (!PushSyntacticSugarProxy(mainThreadInstance, propertyEntry.index, liveObjectIndex))
                                {
                                    return 0;
                                }

                                return 1;
                            }
                            else
                            {
                                Lua.PushStack(mainThreadInstance, mainThreadInstance.registeredMethods[propertyEntry.index].closure);
                                return 1;
                            }
                        }
                    case UserDataRegistryEntry.PropertyEntry.Type.Property:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            PropertyCallInfo propertyInfo = mainThreadInstance.registeredProperties[propertyEntry.index];
                            object result = propertyInfo.propertyInfo.GetMethod.Invoke(obj, null);
                            bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, propertyInfo.propertyType, result);
                            return 1;
                        }
                    case UserDataRegistryEntry.PropertyEntry.Type.Field:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            FieldCallInfo fieldInfo = mainThreadInstance.registeredFields[propertyEntry.index];
                            object result = fieldInfo.fieldInfo.GetValue(obj);
                            bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, fieldInfo.fieldType, result);
                            return 1;
                        }
                }

                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            catch (Exception e)
            {
                mainThreadInstance.ExceptionError(e, bLuaError.error_objectNotProvided);
                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int Metamethod_NewIndex(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                UserDataRegistryEntry userDataInfo;
                if (!GetUserDataEntry(_state, mainThreadInstance, out userDataInfo))
                {
                    return 0;
                }

                string propertyName;
                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (!GetUserDataPropertyEntry(_state, mainThreadInstance, userDataInfo, out propertyEntry, out propertyName))
                {
                    return 0;
                }

                switch (propertyEntry.propertyType)
                {
                    case UserDataRegistryEntry.PropertyEntry.Type.Property:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            PropertyCallInfo propertyInfo = mainThreadInstance.registeredProperties[propertyEntry.index];
                            object[] args = new object[1] { bLuaUserData.PopStackIntoParamType(mainThreadInstance, propertyInfo.propertyType) };
                            propertyInfo.propertyInfo.SetMethod.Invoke(obj, args);
                            return 0;
                        }
                    case UserDataRegistryEntry.PropertyEntry.Type.Field:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            FieldCallInfo fieldInfo = mainThreadInstance.registeredFields[propertyEntry.index];
                            object arg = bLuaUserData.PopStackIntoParamType(mainThreadInstance, fieldInfo.fieldType);
                            fieldInfo.fieldInfo.SetValue(obj, arg);
                            return 0;
                        }
                }

                mainThreadInstance.Error($"{bLuaError.error_setProperty}{propertyName}");
                return 0;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int MetaMethod_GC(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));

            if (mainThreadInstance == null)
            {
                return 0;
            }

            LuaLibAPI.lua_checkstack(_state, 1);
            LuaLibAPI.lua_getiuservalue(_state, 1, 1);
            int n = LuaLibAPI.lua_tointegerx(mainThreadInstance.state, -1, IntPtr.Zero);
            mainThreadInstance.liveObjects[n] = null;
            mainThreadInstance.liveObjectsFreeList.Add(n);

            return 0;
        }

        public static int Metamethod_Operator(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                bLuaValue operandR = Lua.PopStackIntoValue(mainThreadInstance);
                bLuaValue operandL = Lua.PopStackIntoValue(mainThreadInstance);

                Type[] operationMethodParamRequirements = new Type[2] { operandL.ToObject().GetType(), operandR.ToObject().GetType() };

                string operationMethodName = Lua.GetString(revertState, Lua.UpValueIndex(2));
                string operationError = Lua.GetString(revertState, Lua.UpValueIndex(3));

                MethodInfo operationMethod;
                if (!GetMethodWithParams(operandL.ToObject(), operationMethodName, out operationMethod, operationMethodParamRequirements))
                {
                    if (!GetMethodWithParams(operandR.ToObject(), operationMethodName, out operationMethod, operationMethodParamRequirements))
                    {
                        mainThreadInstance.Error($"{operationError}{operandL.ToObject().GetType().Name}, {operandR.ToObject().GetType().Name}");
                        Lua.PushNil(mainThreadInstance);
                        return 1;
                    }
                }

                object[] args = new object[operationMethod.GetParameters().Length];
                if (args.Length >= 1) args[0] = operandL.ToObject();
                if (args.Length >= 2) args[1] = operandR.ToObject();

                object result = operationMethod.Invoke(null, args);
                Lua.PushOntoStack(mainThreadInstance, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int Metamethod_Concatenation(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                bLuaValue operandR = Lua.PopStackIntoValue(mainThreadInstance);
                bLuaValue operandL = Lua.PopStackIntoValue(mainThreadInstance);
                string lhs = operandL.CastToString();
                string rhs = operandR.CastToString();

                if (operandL.Type == DataType.UserData
                    && (string.IsNullOrEmpty(lhs) || operandL.ToObject().GetType().FullName == lhs))
                {
                    mainThreadInstance.Error($"{bLuaError.error_concatenation}{operandL.ToObject().GetType().Name}");
                    Lua.PushNil(mainThreadInstance);
                    return 1;
                }

                if (operandR.Type == DataType.UserData
                    && (string.IsNullOrEmpty(rhs) || operandR.ToObject().GetType().FullName == rhs))
                {
                    mainThreadInstance.Error($"{bLuaError.error_concatenation}{operandR.ToObject().GetType().Name}");
                    Lua.PushNil(mainThreadInstance);
                    return 1;
                }

                string result = lhs + rhs;
                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, MethodCallInfo.ParamType.Str, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int Metamethod_ToString(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                UserDataRegistryEntry userDataInfo;
                if (!GetUserDataEntry(_state, mainThreadInstance, out userDataInfo))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                MethodCallInfo methodCallInfo;
                if (!GetMethodCallInfo(mainThreadInstance, userDataInfo, "ToString", out methodCallInfo))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                object obj;
                if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                object result = methodCallInfo.methodInfo.Invoke(obj, new object[0]);
                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, MethodCallInfo.ParamType.Str, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }
    }
} // bLua.Internal namespace
