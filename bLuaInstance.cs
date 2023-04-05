using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using bLua.NativeLua;
using bLua.Internal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace bLua
{
    /// <summary> Lua data types. </summary>
    public enum DataType
    {
        Nil = 0,
        Boolean = 1,
        Number = 3,
        String = 4,
        Table = 5,
        Function = 6,
        UserData = 7,
        Thread = 8,
        Unknown = 9,
    }

    /// <summary> Features are specific libraries, Lua boilerplate, or C# systems that improve upon vanilla Lua usage or functionality. </summary>
    [Flags]
    public enum Feature
    {
        /// <summary> No given features. </summary>
        None = 0,
        /// <summary> Basic Function (https://www.lua.org/manual/5.4/manual.html#6.1). </summary>
        BasicLibrary = 1,
        /// <summary> Lua Coroutine library and boilerplate Lua code for calling coroutines from C# (https://www.lua.org/manual/5.4/manual.html#6.2). </summary>
        Coroutines = 2,
        /// <summary> Contains the `package` library for loading modules via paths (https://www.lua.org/manual/5.4/manual.html#6.3). </summary>
        Packages = 4,
        /// <summary> String Manipulation (https://www.lua.org/manual/5.4/manual.html#6.4) </summary>
        StringManipulation = 8,
        /// <summary> UTF-8 Support (https://www.lua.org/manual/5.4/manual.html#6.5). </summary>
        UTF8Support = 16,
        /// <summary> Table Manipulation (https://www.lua.org/manual/5.4/manual.html#6.6). </summary>
        Tables = 32,
        /// <summary> Mathematical Functions (https://www.lua.org/manual/5.4/manual.html#6.7). </summary>
        MathLibrary = 64,
        /// <remarks> WARNING! IO access includes the ability to read and write files on the user's machine. </remarks>
        /// <summary> Input and Output Facilities (https://www.lua.org/manual/5.4/manual.html#6.8). </summary>
        IO = 128,
        /// <remarks> WARNING! OS access includes the ability to delete files on the user's machine as well as execute C functions on the operating system shell. </remarks>
        /// <summary> Operating System Facilities (https://www.lua.org/manual/5.4/manual.html#6.9). </summary>
        OS = 256,
        /// <remarks> WARNING! (From the Lua 5.4 manual:) "You should exert care when using this library. Several of its functions violate basic assumptions about 
        /// Lua code (e.g., that variables local to a function cannot be accessed from outside; that userdata metatables cannot be changed by Lua code; that Lua 
        /// programs do not crash) and therefore can compromise otherwise secure code." </remarks>
        /// <summary> The Debug Library (https://www.lua.org/manual/5.4/manual.html#6.10). </summary>
        Debug = 512,
        /// <summary> Includes `wait(t)`, `spawn(fn)`, and `delay(t, fn)` function(s) that can be used for better threading and coroutine control in Lua. NOTE: 
        /// Feature.Coroutines needs to be enabled for this feature to work. </summary>
        ThreadMacros = 1024,
        /// <summary> Includes `print(s)` function(s) as globals. </summary>
        HelperMacros = 2048,
        /// <remarks> WARNING! This may affect performance based on how often your Lua runs userdata methods. </remarks>
        /// <summary> Makes the syntax sugar `:` optional when calling instance methods on userdata. The symbols `.` and `:` become interchangeable in all cases
        /// where Lua is calling a userdata method. </summary>
        ImplicitSyntaxSugar = 4096
    }

    /// <summary> Sandboxes are groupings of features that let you select premade feature lists for your bLua environment. </summary>
    public enum Sandbox
    {
        /// <summary> No additional Lua or bLua features. </summary>
        None = Feature.None,
        /// <remarks> WARNING! Some of these features include developer warnings, please review the remarks on individual features. </remarks>
        /// <summary> Includes all of the features Lua and bLua have to offer. </summary>
        AllFeatures = Feature.BasicLibrary
            | Feature.Coroutines
            | Feature.Packages
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.IO
            | Feature.OS
            | Feature.Debug
            | Feature.ThreadMacros
            | Feature.HelperMacros
            | Feature.ImplicitSyntaxSugar,
        /// <remarks> WARNING! Some of these features include developer warnings, please review the remarks on individual features. </remarks>
        /// <summary> Includes most Lua and bLua features, specifically ones that might be used commonly in modding. </summary>
        BasicModding = Feature.BasicLibrary
            | Feature.Coroutines
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.IO
            | Feature.ThreadMacros
            | Feature.HelperMacros,
        /// <summary> Includes basic Lua and bLua features, avoiding ones that could be potentially used maliciously. </summary>
        Safe = Feature.BasicLibrary
            | Feature.Coroutines
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.ThreadMacros
            | Feature.HelperMacros
    }

    /// <summary> Contains settings for the bLua runtime. </summary>
    public class bLuaSettings
    {
        /// <summary> The selected sandbox (set of features) for bLua. </summary>
        public Sandbox sandbox = Sandbox.Safe;

        public enum TickBehavior
        {
            /// <summary> Always tick at the tickInterval in the settings. </summary>
            AlwaysTick,
            /// <summary> Never tick automatically; use ManualTick() to tick instead. </summary>
            Manual,
            /// <summary> Tick at the tickInterval interval in the settings only when there is at least one active coroutines running in the instance. </summary>
            TickOnlyWhenCoroutinesActive
        }

        /// <summary> Controls the ticking behavior. Read summaries of TickBehavior options for more information. </summary>
        public TickBehavior tickBehavior = TickBehavior.AlwaysTick;

        /// <summary> The millisecond delay between bLua ticks. </summary>
        public int tickInterval = 10; // 10 = 100 ticks per second

        public enum AutoRegisterTypes
        {
            /// <summary> Do not automatically register any types. </summary>
            None,
            /// <summary> Upon initializing, register all types with the [bLuaUserData] attribute as Lua userdata. </summary>
            BLua
        }

        /// <summary> Controls user data behavior. Read summaries of UserDataBehavior options for more information. </summary>
        public AutoRegisterTypes autoRegisterTypes = AutoRegisterTypes.BLua;
    }

    public class bLuaInstance : IDisposable
    {
        static Dictionary<IntPtr, bLuaInstance> instanceRegistry = new Dictionary<IntPtr, bLuaInstance>();

        public static bLuaInstance GetInstanceByState(IntPtr _state)
        {
            bLuaInstance instance = null;
            instanceRegistry.TryGetValue(_state, out instance);
            return instance;
        }

        public static int GetInstanceCount()
        {
            return instanceRegistry.Count;
        }

        public readonly bLuaSettings settings = new bLuaSettings();

        UnityEvent<string> OnPrint = new UnityEvent<string>();

        /// <summary> Contains the current Lua state (https://www.lua.org/manual/5.4/manual.html#lua_newstate). </summary>
        public IntPtr state;

        public int stringCacheHit = 0, stringCacheMiss = 0;
        public StringCacheEntry[] stringCache = new StringCacheEntry[997];
        Dictionary<string, bLuaValue> internedStrings = new Dictionary<string, bLuaValue>();
        Dictionary<string, bLuaValue> lookups = new Dictionary<string, bLuaValue>();

        public List<MethodCallInfo> registeredMethods = new List<MethodCallInfo>();
        public List<PropertyCallInfo> registeredProperties = new List<PropertyCallInfo>();
        public List<FieldCallInfo> registeredFields = new List<FieldCallInfo>();
        public List<UserDataRegistryEntry> registeredEntries = new List<UserDataRegistryEntry>();
        public Dictionary<string, int> typenameToEntryIndex = new Dictionary<string, int>();

        public object[] liveObjects = new object[65536];
        public object[] syntaxSugarProxies = new object[65536];
        public List<int> liveObjectsFreeList = new List<int>();
        public int nextLiveObject = 1;

        public int numLiveObjects
        {
            get
            {
                return (nextLiveObject - 1) - liveObjectsFreeList.Count;
            }
        }


        public bLuaInstance()
        {
            settings = new bLuaSettings();
            Init();
        }

        public bLuaInstance(bLuaSettings _settings)
        {
            settings = _settings;
            Init();
        }

        ~bLuaInstance()
        {
            Dispose();
        }

        public void Dispose()
        {
            DeInit();
        }

        #region Initialization
        /// <summary> Whether or not bLua has been initialized. </summary>
        bool initialized = false;
        bool reinitializing = false;


        /// <summary> Initialize Lua and handle enabling/disabled features based on the current sandbox. </summary>
        void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            SceneManager.activeSceneChanged -= OnActiveSceneChanged; // This can be done safely
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            OnPrint.AddListener((s) => Debug.Log(s));

            // Create a new state for this instance
            state = LuaXLibAPI.luaL_newstate();
            instanceRegistry.Add(state, this);

            // Initialize all bLua userdata
            if (settings.autoRegisterTypes.HasFlag(bLuaSettings.AutoRegisterTypes.BLua))
            {
                bLuaUserData.RegisterAllBLuaUserData(this);
            }

            #region Feature Handling
            if (FeatureEnabled(Feature.BasicLibrary))
            {
                LuaLibAPI.luaopen_base(state);
            }

            if (FeatureEnabled(Feature.Coroutines))
            {
                SetGlobal<Func<float>>("blua_internal_time", () => {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        return (float)EditorApplication.timeSinceStartup - Time.time;
                    }

                    return Time.time;
#else
                return Time.time;
#endif
                });

                LuaLibAPI.luaopen_coroutine(state);
                LuaLibAPI.lua_setglobal(state, "coroutine");

                if (OnTick != null)
                {
                    OnTick.RemoveListener(TickCoroutines);
                    OnTick.AddListener(TickCoroutines);
                }

                DoBuffer("builtin_coroutines", @"builtin_coroutines = {}");
                callco = DoBuffer("callco",
                    @"return function(fn, a, b, c, d, e, f, g, h)
                        local co = coroutine.create(fn)
                        local res, error = coroutine.resume(co, a, b, c, d, e, f, g, h)
                        --print('COROUTINE:: call co: %s -> %s -> %s', type(co), type(fn), coroutine.status(co))
                        if not res then
                            print(string.format('error in co-routine: %s', error))
                        end
                        if coroutine.status(co) ~= 'dead' then
                            builtin_coroutines[#builtin_coroutines+1] = co
                        end
                    end");
                updateco = DoBuffer("updateco",
                    @"return function()
                        local allRunning = true
                        for _,co in ipairs(builtin_coroutines) do
                            local res, error = coroutine.resume(co)
                            if not res then
                                print(string.format('error in co-routine: %s', error))
                            end
                            if coroutine.status(co) == 'dead' then
                                allRunning = false
                            end
                        end

                        if not allRunning then
                            local new_coroutines = {}
                            for _,co in ipairs(builtin_coroutines) do
                                if coroutine.status(co) ~= 'dead' then
                                    new_coroutines[#new_coroutines+1] = co
                                end
                            end

                            builtin_coroutines = new_coroutines
                        end
                    end");
                cancelcos = DoBuffer("cancelcos",
                    @"return function()
                        for _,co in ipairs(builtin_coroutines) do
                            local res, error = coroutine.close(co)
                            if not res then
                                print(string.format('error closing co-routine: %s', error))
                            end
                        end
                        builtin_coroutines = {}
                    end");
            }

            if (FeatureEnabled(Feature.Packages))
            {
                LuaLibAPI.luaopen_package(state);
                LuaLibAPI.lua_setglobal(state, "package");
            }

            if (FeatureEnabled(Feature.StringManipulation))
            {
                LuaLibAPI.luaopen_string(state);
                LuaLibAPI.lua_setglobal(state, "string");
            }

            if (FeatureEnabled(Feature.UTF8Support))
            {
                LuaLibAPI.luaopen_utf8(state);
                LuaLibAPI.lua_setglobal(state, "utf8");
            }

            if (FeatureEnabled(Feature.Tables))
            {
                LuaLibAPI.luaopen_table(state);
                LuaLibAPI.lua_setglobal(state, "table");
            }

            if (FeatureEnabled(Feature.MathLibrary))
            {
                LuaLibAPI.luaopen_math(state);
                LuaLibAPI.lua_setglobal(state, "math");
            }

            if (FeatureEnabled(Feature.IO))
            {
                LuaLibAPI.luaopen_io(state);
                LuaLibAPI.lua_setglobal(state, "io");
            }

            if (FeatureEnabled(Feature.OS))
            {
                LuaLibAPI.luaopen_os(state);
                LuaLibAPI.lua_setglobal(state, "os");
            }

            if (FeatureEnabled(Feature.Debug))
            {
                LuaLibAPI.luaopen_debug(state);
                LuaLibAPI.lua_setglobal(state, "debug");
            }

            if (FeatureEnabled(Feature.ThreadMacros))
            {
                DoBuffer("thread_macros",
                    @"function wait(t)
                        local startTime = blua_internal_time()
                        while blua_internal_time() < startTime + t do
                            coroutine.yield()
                        end
                    end

                    function spawn(fn, a, b, c, d, e, f, g, h)
                        local co = coroutine.create(fn)
                        local res, error = coroutine.resume(co, a, b, c, d, e, f, g, h)
                        if not res then
                            print(string.format('error in co-routine spawn: %s', error))
                        end
                        if coroutine.status(co) ~= 'dead' then
                            builtin_coroutines[#builtin_coroutines+1] = co
                        end
                    end

                    function delay(t, fn)
                        spawn(function()
                            wait(t)
                            fn()
                        end)
                    end");
            }

            if (FeatureEnabled(Feature.HelperMacros))
            {
                SetGlobal<Action<bLuaValue>>("print", (s) => OnPrint.Invoke(s.CastToString()));
            }
            #endregion // Feature Handling

            if (settings.tickBehavior != bLuaSettings.TickBehavior.Manual)
            {
                // Start the threading needed for running bLua without a MonoBehaviour
                StartTicking();
            }
        }

        void DeInit()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            OnTick.RemoveAllListeners();
            StopTicking();

            instanceRegistry.Remove(state);
            if (state != IntPtr.Zero)
            {
                LuaLibAPI.lua_close(state); // Closes the current Lua environment and releases all objects, threads, and dynamic memory
            }
            state = IntPtr.Zero;

            initialized = false;

            if (!reinitializing)
            {
                GC.ReRegisterForFinalize(this);
            }
        }

        void ReInit()
        {
            reinitializing = true;
            DeInit();
            Init();
            reinitializing = false;
        }

        void OnActiveSceneChanged(Scene _previous, Scene _new)
        {
            if (_previous.IsValid())
            {
                Dispose();
            }
        }
        #endregion // Initialization

        #region Tick
        /// <summary> This event is called whenever bLua ticks. Allows for bLua features (or developers) to listen for when ticking takes place. </summary>
        public UnityEvent OnTick = new UnityEvent();

        bool ticking = false;
        bool requestStartTicking = false;
        bool requestStopTicking = false;


        public void ManualTick()
        {
            InternalTick();
        }

        async void Tick()
        {
            // Don't have two instances of the Tick thread; if this value is already set, don't continue
            if (ticking)
            {
                return;
            }

            ticking = true;
            requestStartTicking = false;

            // Only continue ticking while this value is set. This allows us to close the tick thread from outside of it when we need to
            while (!requestStopTicking)
            {
                if (settings.tickBehavior != bLuaSettings.TickBehavior.TickOnlyWhenCoroutinesActive
                    || (settings.tickBehavior == bLuaSettings.TickBehavior.TickOnlyWhenCoroutinesActive && runningCoroutines > 0))
                {
                    InternalTick();
                }

                await Task.Delay(settings.tickInterval);
            }

            ticking = false;
            requestStopTicking = false;

            // If we've already re-requested to start ticking again, go ahead and handle that here as the previous Tick() call would have failed
            if (requestStartTicking)
            {
                StartTicking();
            }
        }

        void InternalTick()
        {
            if (OnTick != null)
            {
                OnTick.Invoke();
            }
        }

        public void StartTicking()
        {
            Tick();
            requestStartTicking = true;
        }

        public void StopTicking()
        {
            requestStopTicking = true;
        }
        #endregion // Tick

        #region Coroutines
        struct ScheduledCoroutine
        {
            public bLuaValue fn;
            public object[] args;
            public int debugTag;
        }

        bLuaValue callco = null;
        bLuaValue updateco = null;
        bLuaValue cancelcos = null;

        List<ScheduledCoroutine> scheduledCoroutines = new List<ScheduledCoroutine>();

        int coroutineID = 0;


        void TickCoroutines()
        {
            using (Lua.profile_luaCo.Auto())
            {
                Call(updateco);

                while (scheduledCoroutines.Count > 0)
                {
                    var co = scheduledCoroutines[0];
                    scheduledCoroutines.RemoveAt(0);

                    CallCoroutine(co.fn, co.args);
                }
            }
        }

        void ScheduleCoroutine(bLuaValue _fn, params object[] _args)
        {
            ++coroutineID;
            scheduledCoroutines.Add(new ScheduledCoroutine()
            {
                fn = _fn,
                args = _args,
                debugTag = coroutineID,
            });
        }

        int runningCoroutines
        {
            get
            {
                LuaLibAPI.lua_getglobal(state, "builtin_coroutines");
                int len = (int)LuaLibAPI.lua_rawlen(state, -1);
                Lua.PopStack(this);
                return len;
            }
        }

        public void CallCoroutine(bLuaValue _fn, params object[] _args)
        {
            int nargs = _args != null ? _args.Length : 0;

            object[] a = new object[nargs + 1];
            a[0] = _fn;
            if (nargs > 0)
            {
                for (int i = 0; i != _args.Length; ++i)
                {
                    a[i + 1] = _args[i];
                }
            }

            Call(callco, a);
        }
        #endregion // Coroutines

        #region Globals
        public bLuaValue GetGlobal(string _key)
        {
            int resType = LuaLibAPI.lua_getglobal(state, _key);
            var result = Lua.PopStackIntoValue(this);
            result.dataType = (DataType)resType;
            return result;
        }

        public void SetGlobal<T>(string _key, T _object)
        {
            Lua.PushOntoStack(this, _object);
            LuaLibAPI.lua_setglobal(state, _key);
        }
        #endregion // Globals

        #region Errors
        public delegate void LuaErrorDelegate(string _message, string _engineTrace = null);
        /// <summary> This delegate is called whenever a Lua Error happens. Allows for bLua features (or developers) to listen. </summary>
        public LuaErrorDelegate LuaErrorHandler;

        public void Error(string _message, string _engineTrace = null)
        {
            if (LuaErrorHandler != null)
            {
                LuaErrorHandler(_message, _engineTrace);
            }

            string msg = Lua.TraceMessage(this, _message);
            if (_engineTrace != null)
            {
                msg += "\n\n---\nEngine error details:\n" + _engineTrace;
            }

            Debug.LogError(msg);
        }

        public void ExceptionError(Exception _exception, string _prependedErrorInfo = "")
        {
            Exception ex = _exception;
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }
            Error($"{_prependedErrorInfo}{ex.Message}", $"{ex.StackTrace}");
        }
        #endregion // Errors

        /// <summary> Loads a string of Lua code and runs it. </summary>
        /// <param name="_code">The string of code to run.</param>
        public bLuaValue DoString(string _code, bLuaValue _environment = null)
        {
            return DoBuffer("code", _code, _environment);
        }

        /// <summary> Loads a buffer as a Lua chunk and runs it. </summary>
        /// <param name="_name">The chunk name, used for debug information and error messages.</param>
        /// <param name="_text">The Lua code to load.</param>
        public bLuaValue DoBuffer(string _name, string _text, bLuaValue _environment = null)
        {
            ExecBuffer(_name, _text, 1, _environment);
            return Lua.PopStackIntoValue(this);
        }

        /// <summary> Loads a buffer as a Lua chunk and runs it. </summary>
        /// <param name="_name">The chunk name, used for debug information and error messages.</param>
        /// <param name="_text">The Lua code to load.</param>
        public void ExecBuffer(string _name, string _text, int _nresults = 0, bLuaValue _environment = null)
        {
            if (!initialized)
            {
                Debug.LogError("Attempt to ExecBuffer when instance is not initialized.");
                return;
            }

            using (Lua.profile_luaCall.Auto())
            {
                int result = LuaXLibAPI.luaL_loadbufferx(state, _text, (ulong)_text.Length, _name, null); // S: (buffer)
                if (result != 0)
                {
                    string msg = Lua.GetString(state, -1);
                    Lua.LuaPop(state, 1);
                    throw new LuaException(msg);
                }

                if (_environment != null)
                {
                    Lua.PushStack(this, _environment); // pushes the given table. S: (buffer)(env)
                    LuaLibAPI.lua_createtable(state, 0, 0); // pushes an empty table. S: (buffer)(env)(emptyTable)
                    LuaLibAPI.lua_getglobal(state, "_G"); // pushes _G. S: (buffer)(newEenvnv)(emptyTable)(_G)
                    LuaLibAPI.lua_setfield(state, -2, Lua.StringToIntPtr("__index")); // sets the stack at index to the -1 stack index's value. pops the value. S: (buffer)(env)(emptyTableWith_GAs__index)
                    LuaLibAPI.lua_setmetatable(state, -2); // pops -1 stack index and sets it to the stack value at index. S: (buffer)(envWith_GAs__index)
                    LuaLibAPI.lua_setupvalue(state, -2, 1); // assigns -1 stack index to the upvalue for value in stack at index. S: (bufferWithEnvUpvalue)
                }

                using (Lua.profile_luaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(state, 0, _nresults, 0, 0, IntPtr.Zero);
                }

                if (result != 0)
                {
                    string msg = Lua.GetString(state, -1);
                    Lua.LuaPop(state, 1);
                    throw new LuaException(msg);
                }
            }
        }

        /// <summary> Calls a passed Lua function. </summary>
        /// <param name="_fn">The Lua function being called.</param>
        /// <param name="_args">Arguments that will be passed into the called Lua function.</param>
        /// <returns>The output from the called Lua function.</returns>
        public bLuaValue Call(bLuaValue _fn, params object[] _args)
        {
            if (!initialized)
            {
                Debug.LogError("Attempt to Call function when instance is not initialized.");
                return null;
            }

            using (Lua.profile_luaCall.Auto())
            {
                Lua.PushStack(this, _fn);

                foreach (var arg in _args)
                {
                    Lua.PushOntoStack(this, arg);
                }

                int result;
                //TODO set the error handler to get the stack trace.
                using (Lua.profile_luaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(state, _args.Length, 1, 0, 0L, IntPtr.Zero);
                }
                if (result != 0)
                {
                    string error = Lua.GetString(state, -1);
                    Lua.LuaPop(state, 1);
                    Error($"{bLuaError.error_inFunctionCall}{error}");
                    throw new LuaException(error);
                }

                return Lua.PopStackIntoValue(this);
            }
        }

        public bLuaValue FullLookup(bLuaValue _value, string _key)
        {
            bLuaValue fn;
            if (lookups.TryGetValue(_key, out fn) == false)
            {
                fn = DoBuffer("lookup", $"return function(obj) return obj.{_key} end");
                lookups.Add(_key, fn);
            }

            return Call(fn, _value);
        }

        public bLuaValue InternString(string s)
        {
            bLuaValue result;
            if (internedStrings.TryGetValue(s, out result))
            {
                return result;
            }

            result = bLuaValue.CreateString(this, s);
            internedStrings.Add(s, result);
            return result;
        }

        /// <summary> Returns true if this instance's sandbox has the passed feature enabled. </summary>
        public bool FeatureEnabled(Feature _feature)
        {
            return ((Feature)(int)settings.sandbox).HasFlag(_feature);
        }

        #region C Functions called from Lua
        public static int CallDelegate(IntPtr _state)
        {
            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int stackSize = LuaLibAPI.lua_gettop(_state);

                int n = LuaLibAPI.lua_tointegerx(_state, Lua.UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.registeredMethods.Count)
                {
                    mainThreadInstance.Error($"{bLuaError.error_invalidMethodIndex}{n}");
                    return 0;
                }

                MethodCallInfo methodCallInfo = mainThreadInstance.registeredMethods[n];
                DelegateCallInfo info = methodCallInfo as DelegateCallInfo;

                object[] parms = null;
                int parmsIndex = 0;

                int parametersLength = info.argTypes.Length;
                if (parametersLength > 0 && info.argTypes[parametersLength - 1] == MethodCallInfo.ParamType.Params)
                {
                    parametersLength--;
                    if (stackSize > parametersLength)
                    {
                        parms = new object[(stackSize) - parametersLength];
                        parmsIndex = parms.Length - 1;
                    }
                }

                object[] args = new object[info.argTypes.Length];
                int argIndex = args.Length - 1;

                // Set the last args index to be the parameters array
                if (parms != null)
                {
                    args[argIndex--] = parms;
                }

                while (argIndex > stackSize - 1)
                {
                    // Backfill any arguments with defaults.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 1 > argIndex)
                {
                    // Backfill the parameters with values from the Lua stack
                    if (parms != null)
                    {
                        parms[parmsIndex--] = Lua.PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        Lua.PopStack(mainThreadInstance);
                    }
                    --stackSize;
                }

                while (stackSize > 0)
                {
                    args[argIndex] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, info.argTypes[argIndex]);

                    --stackSize;
                    --argIndex;
                }

                object result = info.multicastDelegate.DynamicInvoke(args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;

            }
            catch (Exception e)
            {
                mainThreadInstance.ExceptionError(e, bLuaError.error_callingDelegate);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        public static int CallUserDataFunction(IntPtr _state)
        {
            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int stackSize = LuaLibAPI.lua_gettop(_state);
                if (stackSize == 0 || LuaLibAPI.lua_type(_state, 1) != (int)DataType.UserData)
                {
                    mainThreadInstance.Error($"{bLuaError.error_objectNotProvided}");
                    return 0;
                }

                int n = LuaLibAPI.lua_tointegerx(_state, Lua.UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.registeredMethods.Count)
                {
                    mainThreadInstance.Error($"{bLuaError.error_invalidMethodIndex}{n}");
                    return 0;
                }

                MethodCallInfo info = mainThreadInstance.registeredMethods[n];

                object[] parms = null;
                int parmsIndex = 0;

                int len = info.argTypes.Length;
                if (len > 0 && info.argTypes[len - 1] == MethodCallInfo.ParamType.Params)
                {
                    len--;
                    if (stackSize - 1 > len)
                    {
                        parms = new object[(stackSize - 1) - len];
                        parmsIndex = parms.Length - 1;
                    }
                }

                object[] args = new object[info.argTypes.Length];
                int argIndex = args.Length - 1;

                if (parms != null)
                {
                    args[argIndex--] = parms;
                }

                while (argIndex > stackSize - 2)
                {
                    // Backfill any arguments with defaults.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 2 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = Lua.PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        Lua.PopStack(mainThreadInstance);
                    }
                    --stackSize;
                }

                while (stackSize > 1)
                {
                    args[argIndex] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, info.argTypes[argIndex]);

                    --stackSize;
                    --argIndex;
                }

                if (LuaLibAPI.lua_gettop(_state) < 1)
                {
                    mainThreadInstance.Error($"{bLuaError.error_stackIsEmpty}");
                    return 0;
                }

                int t = LuaLibAPI.lua_type(_state, 1);
                if (t != (int)DataType.UserData)
                {
                    mainThreadInstance.Error($"{bLuaError.error_objectIsNotUserdata}{(DataType)t}");
                    return 0;
                }

                LuaLibAPI.lua_checkstack(_state, 1);
                int res = LuaLibAPI.lua_getiuservalue(_state, 1, 1);
                if (res != (int)DataType.Number)
                {
                    mainThreadInstance.Error($"{bLuaError.error_objectNotProvided}");
                    return 0;
                }
                int liveObjectIndex = LuaLibAPI.lua_tointegerx(_state, -1, IntPtr.Zero);
                object obj = mainThreadInstance.liveObjects[liveObjectIndex];

                object result = info.methodInfo.Invoke(obj, args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;
            }
            catch (Exception e)
            {
                mainThreadInstance.ExceptionError(e, bLuaError.error_callingFunction);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        public static int CallStaticUserDataFunction(IntPtr _state)
        {
            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int n = LuaLibAPI.lua_tointegerx(_state, Lua.UpValueIndex(1), IntPtr.Zero);
                MethodCallInfo info = mainThreadInstance.registeredMethods[n];

                int stackSize = LuaLibAPI.lua_gettop(_state);

                object[] parms = null;
                int parmsIndex = 0;

                int len = info.argTypes.Length;
                if (len > 0 && info.argTypes[len - 1] == MethodCallInfo.ParamType.Params)
                {
                    len--;
                    if (stackSize > len)
                    {
                        parms = new object[stackSize - len];
                        parmsIndex = parms.Length - 1;
                    }
                }

                object[] args = new object[info.argTypes.Length];
                int argIndex = args.Length - 1;

                if (parms != null)
                {
                    args[argIndex--] = parms;
                }

                while (argIndex > stackSize - 1)
                {
                    // Backfill any arguments with nulls.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 1 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = Lua.PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        Lua.PopStack(mainThreadInstance);
                    }
                    --stackSize;
                }

                while (stackSize > 0)
                {
                    args[argIndex] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, info.argTypes[argIndex]);

                    --stackSize;
                    --argIndex;
                }

                object result = info.methodInfo.Invoke(null, args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;
            }
            catch (Exception e)
            {
                mainThreadInstance.ExceptionError(e, bLuaError.error_callingFunction);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }
        #endregion // C Functions called from Lua

        #region Userdata
        /// <summary> Registers all C# types in all assemblies with the [bLuaUserData] attribute as Lua userdata on this particular instance. </summary>
        public void RegisterAllBLuaUserData()
        {
            bLuaUserData.RegisterAllBLuaUserData(this);
        }

        /// <summary> Registers a given C# type as Lua userdata on this particular instance. Does not require the [bLuaUserData] attribute. See `bLuaUserData.Register`
        /// for a more detailed userdata type register function. </summary>
        public void RegisterUserData(Type _type)
        {
            bLuaUserData.Register(this, _type);
        }
        #endregion
    }
} // bLua namespace
