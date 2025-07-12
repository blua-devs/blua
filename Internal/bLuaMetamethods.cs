using System;
using System.Linq;
using System.Reflection;
using bLua.NativeLua;

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
        public static string[][] metamethodCollection =
        {
            new[] { csharp_operator_addition,        lua_metamethod_addition,        bLuaError.error_operationAddition },
            new[] { csharp_operator_subtraction,     lua_metamethod_subtraction,     bLuaError.error_operationSubtraction },
            new[] { csharp_operator_multiplication,  lua_metamethod_multiplication,  bLuaError.error_operationMultiply },
            new[] { csharp_operator_division,        lua_metamethod_division,        bLuaError.error_operationDivision },
            new[] { csharp_operator_unaryNegation,   lua_metamethod_unaryNegation,   bLuaError.error_operationUnaryNegation },
            new[] { csharp_operator_equality,        lua_metamethod_equality,        bLuaError.error_operationEquality },
            new[] { csharp_operator_lessThan,        lua_metamethod_lessThan,        bLuaError.error_operationLessThan },
            new[] { csharp_operator_lessThanOrEqual, lua_metamethod_lessThanOrEqual, bLuaError.error_operationLessThanOrEqual }
        };


#region Helper Functions
        private static bool GetUserDataEntry(IntPtr _originalState, bLuaInstance _instance, out UserDataRegistryEntry _userDataEntry)
        {
            _userDataEntry = new UserDataRegistryEntry();

            int n = LuaLibAPI.lua_tointegerx(_originalState, Lua.UpValueIndex(1), IntPtr.Zero);
            if (n < 0 || n >= _instance.registeredEntries.Count)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidTypeIndex}{n}");
                return false;
            }

            _userDataEntry = _instance.registeredEntries[n];
            return true;
        }

        private static bool GetUserDataPropertyEntry(IntPtr _originalState, bLuaInstance _instance, UserDataRegistryEntry _userDataEntry, out PropertyEntry _userDataPropertyEntry, out string _propertyName)
        {
            _propertyName = Lua.GetString(_originalState, 2);
            if (!_userDataEntry.properties.TryGetValue(_propertyName, out _userDataPropertyEntry))
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidProperty}{_propertyName}");
                return false;
            }

            return true;
        }

        private static bool GetMethodCallInfo(bLuaInstance _instance, UserDataRegistryEntry _userDataEntry, string _methodName,  out MethodCallInfo _methodCallInfo)
        {
            _methodCallInfo = new MethodCallInfo();

            if (!_userDataEntry.properties.TryGetValue(_methodName, out PropertyEntry propertyEntry))
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidMethod}{_methodName}");
                return false;
            }

            if (propertyEntry.index < 0 || propertyEntry.index >= _instance.registeredMethods.Count)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidMethod}{_methodName}");
                return false;
            }

            _methodCallInfo = _instance.registeredMethods[propertyEntry.index];
            return true;
        }

        private static bool GetLiveObjectIndex(IntPtr _originalState, bLuaInstance _instance, out int _liveObjectIndex)
        {
            _liveObjectIndex = -1;

            int t = LuaLibAPI.lua_type(_originalState, 1);
            if (t != (int)LuaType.UserData)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_objectIsNotUserdata}{(LuaType)t}");
                return false;
            }

            LuaLibAPI.lua_checkstack(_originalState, 1);
            int res = LuaLibAPI.lua_getiuservalue(_originalState, 1, 1);
            if (res != (int)LuaType.Number)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_objectNotProvided}");
                return false;
            }

            _liveObjectIndex = Lua.PopInteger(_instance);
            return true;
        }

        private static bool GetLiveObjectInstance(IntPtr _originalState, bLuaInstance _instance, out object _object)
        {
            _object = null;

            if (!GetLiveObjectIndex(_originalState, _instance, out int liveObjectIndex))
            {
                return false;
            }
            
            _object = _instance.liveObjects[liveObjectIndex];
            return true;
        }

        private static bool GetMethodWithParams(object _userdataObject, string _methodName, out MethodInfo _method, params Type[] _paramTypeRequirementsOrdered)
        {
            _method = null;

            // Get all methods that have the expected name
            MethodInfo[] methodInfos = _userdataObject.GetType().GetMethods().Where((mi) => mi.Name == _methodName).ToArray();
            foreach (MethodInfo methodInfo in methodInfos)
            {
                ParameterInfo[] parameterInfos = methodInfo.GetParameters();

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

                _method = methodInfo;
                return true;
            }

            return false;
        }

        private static bool PushSyntacticSugarProxy(bLuaInstance _instance, int _methodIndex, int _liveObjectIndex)
        {
            if (_liveObjectIndex < 0 || _liveObjectIndex > _instance.syntaxSugarProxies.Length)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidLiveObjectIndex}{_liveObjectIndex}");
                return false;
            }

            LuaLibAPI.lua_newuserdatauv(_instance.state, new IntPtr(8), 1);
            object syntaxSugarProxy = Lua.PopObject(_instance);
            Lua.PushObject(_instance, _instance.state, syntaxSugarProxy);

            _instance.syntaxSugarProxies[_liveObjectIndex] = syntaxSugarProxy;

            bLuaValue metatable = Lua.NewMetaTable(_instance, _liveObjectIndex.ToString());
            metatable.Set("__call", bLuaValue.CreateClosure(
                _instance,
                Metamethod_Call,
                bLuaValue.CreateNumber(_instance, _methodIndex),
                bLuaValue.CreateNumber(_instance, _liveObjectIndex)));

            Lua.PushObject(_instance, _instance.state, _instance.syntaxSugarProxies[_liveObjectIndex]);
            Lua.PushObject(_instance, _instance.state, _methodIndex);
            LuaLibAPI.lua_setiuservalue(_instance.state, -2, 1);
            Lua.PushValue(_instance, metatable);
            LuaLibAPI.lua_setmetatable(_instance.state, -2);

            return true;
        }
#endregion // Helper Functions

        [MonoPInvokeCallback]
        public static int Metamethod_Call(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                if (LuaLibAPI.lua_gettop(_state) == 0 // Stack size equals 0
                    || LuaLibAPI.lua_type(_state, 1) != (int)LuaType.UserData) // First arg passed isn't userdata
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_objectNotProvided}");
                    return 0;
                }
                
                if (!Lua.GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo methodCallInfo))
                {
                    return 0;
                }
                
                if (!Lua.GetLiveObjectUpvalue(_state, mainThreadInstance, out object liveObject))
                {
                    return 0;
                }
                
                if (!Lua.PopStackIntoArgs(mainThreadInstance, methodCallInfo, out object[] args, 1))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inMetamethodCall}nil");
                    return 0;
                }
                
                return (int)Lua.InvokeCSharpMethod(mainThreadInstance, _state, methodCallInfo, liveObject, args);
            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_objectNotProvided);
                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        [MonoPInvokeCallback]
        public static int Metamethod_Index(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                if (!GetUserDataEntry(_state, mainThreadInstance, out UserDataRegistryEntry userDataInfo))
                {
                    return 0;
                }

                if (!GetUserDataPropertyEntry(_state, mainThreadInstance, userDataInfo, out PropertyEntry propertyEntry, out string propertyName))
                {
                    return 0;
                }

                switch (propertyEntry.propertyType)
                {
                    case UserDataPropertyType.Method:
                        {
                            if (mainThreadInstance.GetIsFeatureEnabled(Features.ImplicitSyntaxSugar))
                            {
                                if (!GetLiveObjectIndex(_state, mainThreadInstance, out int liveObjectIndex))
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
                                Lua.PushValue(mainThreadInstance, mainThreadInstance.registeredMethods[propertyEntry.index].closure);
                                return 1;
                            }
                        }
                    case UserDataPropertyType.Property:
                        {
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out object obj))
                            {
                                return 0;
                            }

                            PropertyCallInfo propertyInfo = mainThreadInstance.registeredProperties[propertyEntry.index];
                            object result = propertyInfo.propertyInfo.GetMethod.Invoke(obj, null);
                            Lua.PushReturnType(mainThreadInstance, propertyInfo.propertyType, result);
                            return 1;
                        }
                    case UserDataPropertyType.Field:
                        {
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out object obj))
                            {
                                return 0;
                            }

                            FieldCallInfo fieldInfo = mainThreadInstance.registeredFields[propertyEntry.index];
                            object result = fieldInfo.fieldInfo.GetValue(obj);
                            Lua.PushReturnType(mainThreadInstance, fieldInfo.fieldType, result);
                            return 1;
                        }
                }

                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_objectNotProvided);
                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        [MonoPInvokeCallback]
        public static int Metamethod_NewIndex(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                if (!GetUserDataEntry(_state, mainThreadInstance, out UserDataRegistryEntry userDataInfo))
                {
                    return 0;
                }

                if (!GetUserDataPropertyEntry(_state, mainThreadInstance, userDataInfo, out PropertyEntry propertyEntry, out string propertyName))
                {
                    return 0;
                }

                switch (propertyEntry.propertyType)
                {
                    case UserDataPropertyType.Property:
                        {
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out object obj))
                            {
                                return 0;
                            }

                            PropertyCallInfo propertyInfo = mainThreadInstance.registeredProperties[propertyEntry.index];
                            object[] args = new object[] { Lua.PopObject(mainThreadInstance, propertyInfo.propertyType) };
                            propertyInfo.propertyInfo.SetMethod.Invoke(obj, args);
                            return 0;
                        }
                    case UserDataPropertyType.Field:
                        {
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out object obj))
                            {
                                return 0;
                            }

                            FieldCallInfo fieldInfo = mainThreadInstance.registeredFields[propertyEntry.index];
                            object arg = Lua.PopObject(mainThreadInstance, fieldInfo.fieldType);
                            fieldInfo.fieldInfo.SetValue(obj, arg);
                            return 0;
                        }
                }

                mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_setProperty}{propertyName}");
                return 0;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        [MonoPInvokeCallback]
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

        [MonoPInvokeCallback]
        public static int Metamethod_Operator(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                bLuaValue operandR = Lua.PopValue(mainThreadInstance);
                bLuaValue operandL = Lua.PopValue(mainThreadInstance);

                Type[] operationMethodParamRequirements = new Type[] { operandL.ToObject().GetType(), operandR.ToObject().GetType() };

                string operationMethodName = Lua.GetString(_state, Lua.UpValueIndex(2));
                string operationError = Lua.GetString(_state, Lua.UpValueIndex(3));

                if (!GetMethodWithParams(operandL.ToObject(), operationMethodName, out MethodInfo operationMethod, operationMethodParamRequirements))
                {
                    if (!GetMethodWithParams(operandR.ToObject(), operationMethodName, out operationMethod, operationMethodParamRequirements))
                    {
                        mainThreadInstance.ErrorFromCSharp($"{operationError}{operandL.ToObject().GetType().Name}, {operandR.ToObject().GetType().Name} ({operationMethodName})");
                        Lua.PushNil(mainThreadInstance);
                        return 1;
                    }
                }

                object[] args = new object[operationMethod.GetParameters().Length];
                if (args.Length >= 1) args[0] = operandL.ToObject();
                if (args.Length >= 2) args[1] = operandR.ToObject();

                object result = operationMethod.Invoke(null, args);
                Lua.PushObject(mainThreadInstance, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        [MonoPInvokeCallback]
        public static int Metamethod_Concatenation(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                bLuaValue operandR = Lua.PopValue(mainThreadInstance);
                bLuaValue operandL = Lua.PopValue(mainThreadInstance);
                string lhs = operandL.ToString();
                string rhs = operandR.ToString();

                if (operandL.luaType == LuaType.UserData
                    && (string.IsNullOrEmpty(lhs) || operandL.ToObject().GetType().FullName == lhs))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_concatenation}{operandL.ToObject().GetType().Name}");
                    Lua.PushNil(mainThreadInstance);
                    return 1;
                }

                if (operandR.luaType == LuaType.UserData
                    && (string.IsNullOrEmpty(rhs) || operandR.ToObject().GetType().FullName == rhs))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_concatenation}{operandR.ToObject().GetType().Name}");
                    Lua.PushNil(mainThreadInstance);
                    return 1;
                }

                string result = lhs + rhs;
                Lua.PushReturnType(mainThreadInstance, UserDataType.String, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        [MonoPInvokeCallback]
        public static int Metamethod_ToString(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                if (!GetUserDataEntry(_state, mainThreadInstance, out UserDataRegistryEntry userDataInfo))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                if (!GetMethodCallInfo(mainThreadInstance, userDataInfo, "ToString", out MethodCallInfo methodCallInfo))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                if (!GetLiveObjectInstance(_state, mainThreadInstance, out object obj))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                object result = methodCallInfo.methodInfo.Invoke(obj, new object[0]);
                Lua.PushReturnType(mainThreadInstance, UserDataType.String, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }
    }
} // bLua.Internal namespace
