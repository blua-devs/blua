using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using bLua.NativeLua;
using bLua.Internal;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace bLua
{
    /// <summary>
    /// Lua data types.
    /// </summary>
    public enum LuaType
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

    /// <summary>
    /// Features are specific libraries, Lua boilerplate, or C# systems that improve upon vanilla Lua usage or functionality.
    /// </summary>
    [Flags]
    public enum Features
    {
        /// <summary>
        /// No given features.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Basic Functions.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.1 </para>
        /// </summary>
        BasicLibrary = 1,
        
        /// <summary>
        /// Lua Coroutine library and boilerplate Lua code for calling coroutines from C#.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.2 </para>
        /// </summary>
        Coroutines = 2,
        
        /// <summary>
        /// Contains the `package` library for loading modules via paths.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.3 </para>
        /// </summary>
        Packages = 4,
        
        /// <summary>
        /// String Manipulation.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.4 </para>
        /// </summary>
        StringManipulation = 8,
        
        /// <summary>
        /// UTF-8 Support.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.5 </para>
        /// </summary>
        UTF8Support = 16,
        
        /// <summary>
        /// Table Manipulation.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.6 </para>
        /// </summary>
        Tables = 32,
        
        /// <summary>
        /// Mathematical Functions.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.7 </para>
        /// </summary>
        MathLibrary = 64,
        
        /// <summary>
        /// WARNING! IO access includes the ability to read and write files on the user's machine.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.8 </para>
        /// </summary>
        IO = 128,
        
        /// <summary>
        /// WARNING! OS access includes the ability to delete files on the user's machine as well as execute C functions on the operating system shell.
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.9 </para>
        /// </summary>
        OS = 256,
        
        /// <summary>
        /// WARNING! (From the Lua 5.4 manual:) "You should exert care when using this library. Several of its functions violate basic assumptions about 
        /// Lua code (e.g., that variables local to a function cannot be accessed from outside; that userdata metatables cannot be changed by Lua code; that Lua 
        /// programs do not crash) and therefore can compromise otherwise secure code."
        /// <para> https://www.lua.org/manual/5.4/manual.html#6.10 </para>
        /// </summary>
        Debug = 512,
        
        /// <summary>
        /// Includes `coroutine.wait(t)` and `coroutine.spawn(fn, ...)` function(s) that can be used for better coroutine control in Lua.
        /// coroutine.wait(t) will yield the coroutine it is called in and continue to yield any further resumes until the given duration (in seconds) has passed.
        /// coroutine.spawn(fn, ...) will create and resume a new coroutine and return it.
        /// </summary>
        /// <remarks>
        /// NOTE: Feature.Coroutines needs to be enabled for this feature to work.
        /// </remarks>
        CoroutineHelpers = 1024,
        
        /// <summary>
        /// Overrides `print(s)` function(s) with a C# delegate that can be bound to and implemented as desired.
        /// </summary>
        CSharpPrintOverride = 2048,
        
        /// <summary>
        /// Makes the syntax sugar `:` optional when calling instance methods on userdata. The symbols `.` and `:` become interchangeable in all cases
        /// where Lua is calling userdata methods.
        /// </summary>
        /// <remarks>
        /// <para> WARNING! This feature is EXPERIMENTAL and may cause errors when accessing userdata methods via '.' or ':'. </para>
        /// <para> WARNING! This may affect performance based on how often your Lua runs userdata methods. </para>
        /// </remarks>
        ImplicitSyntaxSugar = 4096,
        
        /// <summary>
        /// When enabled, bLua will run its own GC system to clean up Lua userdata that isn't normally cleaned up by Lua's GC.
        /// </summary>
        /// <remarks>
        /// WARNING! If NOT enabled, you may experience leftover Lua userdata (and bLuaValue objects in C#) that NEVER gets garbage collected. It is
        /// highly recommended to keep this setting on as a lightweight backup to prevent potentially consuming all available memory.
        /// </remarks>
        CSharpGarbageCollection = 8192
    }

    /// <summary>
    /// Contains settings for the bLua runtime.
    /// </summary>
    public class bLuaSettings
    {
        /// <summary>
        /// No additional Lua or bLua features.
        /// </summary>
        public static Features SANDBOX_NONE = Features.None;

        /// <summary>
        /// Includes all the features Lua and bLua have to offer.
        /// </summary>
        /// <remarks>
        /// WARNING! Some of these features include developer warnings, please review the remarks on individual features.
        /// </remarks>
        public static Features SANDBOX_ALL = Features.BasicLibrary
            | Features.Coroutines
            | Features.Packages
            | Features.StringManipulation
            | Features.UTF8Support
            | Features.Tables
            | Features.MathLibrary
            | Features.IO
            | Features.OS
            | Features.Debug
            | Features.CoroutineHelpers
            | Features.CSharpPrintOverride
            | Features.CSharpGarbageCollection;

        /// <summary>
        /// Includes all the features Lua and bLua have to offer plus experimental bLua features.
        /// </summary>
        /// <remarks>
        /// WARNING! Some of these features include developer warnings, please review the remarks on individual features.
        /// </remarks>
        public static Features SANDBOX_ALL_EXPERIMENTAL = SANDBOX_ALL
            | Features.ImplicitSyntaxSugar; // There is at least one known issue with ImplicitSyntaxSugar uncovered in the bLua example Unit Tests

        /// <summary>
        /// Includes most Lua and bLua features, specifically ones that might be used commonly in modding.
        /// </summary>
        /// <remarks>
        /// WARNING! Some of these features include developer warnings, please review the remarks on individual features.
        /// </remarks>
        public static Features SANDBOX_BASICMODDING = Features.BasicLibrary
            | Features.Coroutines
            | Features.StringManipulation
            | Features.UTF8Support
            | Features.Tables
            | Features.MathLibrary
            | Features.IO
            | Features.CoroutineHelpers
            | Features.CSharpPrintOverride
            | Features.CSharpGarbageCollection;

        /// <summary>
        /// Includes basic Lua and bLua features, avoiding ones that could be potentially used maliciously.
        /// </summary>
        public static Features SANDBOX_SAFE = Features.BasicLibrary
            | Features.Coroutines
            | Features.StringManipulation
            | Features.UTF8Support
            | Features.Tables
            | Features.MathLibrary
            | Features.CoroutineHelpers
            | Features.CSharpPrintOverride
            | Features.CSharpGarbageCollection;

        /// <summary>
        /// The selected sandbox (set of features).
        /// </summary>
        public Features features = SANDBOX_SAFE;

        public enum TickBehavior
        {
            /// <summary>
            /// Never tick automatically; use <see cref="bLuaInstance.ManualTick"/> to tick instead.
            /// </summary>
            Manual,
            
            /// <summary>
            /// Always tick at the <see cref="bLuaSettings.tickInterval"/> in the settings.
            /// </summary>
            TickAtInterval
        }

        /// <summary>
        /// Controls the ticking behavior. Read summaries of <see cref="TickBehavior"/> options for more information.
        /// </summary>
        public TickBehavior tickBehavior = TickBehavior.TickAtInterval;

        /// <summary>
        /// The millisecond delay between ticks.
        /// </summary>
        public int tickInterval = 10; // 10 = 100 ticks per second

        /// <summary>
        /// The millisecond delay between bLua's internal C# garbage collection. If the CSharpGarbageCollection feature
        /// isn't enabled, or set to 0, this does nothing.
        /// </summary>
        public int cSharpGarbageCollectionInterval = 10000; // 10,000 = 1 collection per 10 seconds

        public enum AutoRegisterTypes
        {
            /// <summary>
            /// Do not automatically register any types.
            /// </summary>
            None,
            
            /// <summary>
            /// Upon initializing, register all types with the <see cref="bLuaUserDataAttribute"/> attribute as Lua userdata.
            /// </summary>
            BLua
        }

        /// <summary>
        /// Controls user data behavior. Read summaries of <see cref="AutoRegisterTypes"/> options for more information.
        /// </summary>
        public AutoRegisterTypes autoRegisterTypes = AutoRegisterTypes.BLua;

        public enum CoroutineBehaviour
        {
            /// <summary>
            /// Default native Lua behaviour. Do not automatically resume any coroutines, follow Lua instructions only.
            /// </summary>
            Manual,
            
            /// <summary>
            /// Automatically resume all coroutines on every tick.
            /// </summary>
            ResumeOnTick
        }
        
        /// <summary>
        /// Controls the behaviour of coroutines and how they yield and resume.
        /// </summary>
        public CoroutineBehaviour coroutineBehaviour = CoroutineBehaviour.Manual;
        
        public enum InternalErrorVerbosity
        {
            /// <summary>
            /// <para> (No Traces) </para>
            /// Shows the error message sent from the Lua library. Never show the engine trace for errors.
            /// </summary>
            Minimal = 1,
            
            /// <summary>
            /// <para> (Lua Traces) </para>
            /// Shows the error message sent from the Lua library as well as the engine trace (debug.traceback) for the exception.
            /// </summary>
            /// <remarks>
            /// WARNING! You must have the <see cref="Features.Debug"/> feature enabled to grant bLua access to the debug library!
            /// </remarks>
            Normal = 2,
            
            /// <summary>
            /// <para> (Lua + C# Traces) </para>
            /// Shows the error message sent from the Lua library as well as the engine trace (debug.traceback) for the
            /// exception. Additionally, shows the CSharp stack trace for any exceptions inside C# code run from Lua.
            /// </summary>
            /// <remarks>
            /// WARNING! You must have the <see cref="Features.Debug"/> feature enabled to grant bLua access to the debug library!
            /// </remarks>
            Verbose = 3
        }

        /// <summary>
        /// Controls how detailed the error messages from internal bLua are. Read summaries of <see cref="InternalErrorVerbosity"/> options for more info.
        /// </summary>
        public InternalErrorVerbosity internalVerbosity = InternalErrorVerbosity.Normal;
    }

    public class bLuaInstance : IDisposable
    {
        public readonly bLuaSettings settings = new();

        public UnityEvent<bLuaValue[]> OnPrint { get; private set; } = new();
        public UnityEvent<string, string> OnError { get; private set; } = new();

        /// <summary>
        /// Contains the current Lua state.
        /// <para> https://www.lua.org/manual/5.4/manual.html#lua_newstate </para>
        /// </summary>
        public IntPtr state;

        public int stringCacheHit = 0, stringCacheMiss = 0;
        public StringCacheEntry[] stringCache = new StringCacheEntry[997];
        private Dictionary<string, bLuaValue> internedStrings = new();

        public List<MethodCallInfo> registeredMethods = new();
        public List<PropertyCallInfo> registeredProperties = new();
        public List<FieldCallInfo> registeredFields = new();
        public List<UserDataRegistryEntry> registeredEntries = new();
        public Dictionary<string, int> typenameToEntryIndex = new();

        public object[] liveObjects = new object[65536];
        public object[] syntaxSugarProxies = new object[65536];
        public List<int> liveObjectsFreeList = new();
        public int nextLiveObject = 1;

        public int numLiveObjects
        {
            get
            {
                return (nextLiveObject - 1) - liveObjectsFreeList.Count;
            }
        }

        private const string unnamedChunkName = "anonymous";
        
        private const string luaConst_bluaInternalError = "blua_internal_error";
        private const string luaConst_bluaInternalCoroutine = "blua_internal_coroutine";
        private const string luaConst_bluaInternalCoroutineCreate = "blua_internal_coroutine_create";
        private const string luaConst_bluaInternalCoroutineYield = "blua_internal_coroutine_yield";
        private const string luaConst_bluaInternalCoroutineResume = "blua_internal_coroutine_resume";
        private const string luaConst_bluaInternalCoroutineClose = "blua_internal_coroutine_close";
        private const string luaConst_bluaInternalCoroutineMacros = "blua_internal_coroutineMacros";
        private const string luaConst_bluaInternalWait = "blua_internal_wait";
        private const string luaConst_bluaInternalSpawn = "blua_internal_spawn";
        private const string luaConst_bluaInternalCollectGarbage = "blua_internal_garbagecollection";
        private const string luaConst_bluaInternalPrint = "blua_internal_print";
        private const string luaConst_bluaInternalPrintNew = "blua_internal_printNew";
        

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
            if (initialized)
            {
                DeInit();
            }
        }

#region Initialization
        private bool initialized;
        private bool reinitializing;


        /// <summary>
        /// Initialize Lua and handle enabling/disabled features based on the current sandbox.
        /// </summary>
        private void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            SceneManager.activeSceneChanged -= OnActiveSceneChanged; // This can be done safely
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            // Create a new state for this instance
            state = LuaXLibAPI.luaL_newstate();
            instanceRegistry.Add(state, this);

            // Initialize all bLua userdata
            if (settings.autoRegisterTypes.HasFlag(bLuaSettings.AutoRegisterTypes.BLua))
            {
                bLuaUserData.RegisterAllBLuaUserData(this);
            }

            string callInternalLuaError = "";
            if (settings.internalVerbosity >= bLuaSettings.InternalErrorVerbosity.Normal)
            {
                callInternalLuaError = $"{luaConst_bluaInternalError}(error, debug.traceback(co))";
                SetGlobal<Action<string, string>>(luaConst_bluaInternalError, bLuaInternal_ErrorDebug);

                if (!GetIsFeatureEnabled(Features.Debug))
                {
                    Debug.LogError($"You cannot use the {settings.internalVerbosity} level of {settings.internalVerbosity.GetType().Name} unless you have the {Features.Debug} feature enabled! Expect errors/issues.\nThis is because bLua internally uses debug.traceback() to get a proper stack trace - using Lua.Trace() instead will give a stack trace pointing to bLua code, not the user code.");
                }
            }
            else
            {
                callInternalLuaError = $"{luaConst_bluaInternalError}(error)";
                SetGlobal<Action<string>>(luaConst_bluaInternalError, bLuaInternal_Error);
            }

#region Feature Handling
            if (GetIsFeatureEnabled(Features.BasicLibrary))
            {
                LuaLibAPI.luaopen_base(state);
            }

            if (GetIsFeatureEnabled(Features.Coroutines))
            {
                LuaLibAPI.luaopen_coroutine(state);
                LuaLibAPI.lua_setglobal(state, "coroutine");

                SetGlobal<Func<IntPtr, bLuaValue, bLuaValue>>(luaConst_bluaInternalCoroutineCreate, bLuaInternal_CoroutineCreate);
                SetGlobal<Func<IntPtr, bLuaValue, bLuaValue[], bool>>(luaConst_bluaInternalCoroutineResume, bLuaInternal_CoroutineResume);
                SetGlobal<Action<IntPtr>>(luaConst_bluaInternalCoroutineYield, bLuaInternal_CoroutineYield);
                
                DoString($@"coroutine.create = {luaConst_bluaInternalCoroutineCreate}
                    coroutine.resume = function(fn, ...)
                        {luaConst_bluaInternalCoroutineResume}(fn, {"{...}"})
                    end
                    coroutine.yield = {luaConst_bluaInternalCoroutineYield}",
                    luaConst_bluaInternalCoroutine);
            }

            if (GetIsFeatureEnabled(Features.Packages))
            {
                LuaLibAPI.luaopen_package(state);
                LuaLibAPI.lua_setglobal(state, "package");
            }

            if (GetIsFeatureEnabled(Features.StringManipulation))
            {
                LuaLibAPI.luaopen_string(state);
                LuaLibAPI.lua_setglobal(state, "string");
            }

            if (GetIsFeatureEnabled(Features.UTF8Support))
            {
                LuaLibAPI.luaopen_utf8(state);
                LuaLibAPI.lua_setglobal(state, "utf8");
            }

            if (GetIsFeatureEnabled(Features.Tables))
            {
                LuaLibAPI.luaopen_table(state);
                LuaLibAPI.lua_setglobal(state, "table");
            }

            if (GetIsFeatureEnabled(Features.MathLibrary))
            {
                LuaLibAPI.luaopen_math(state);
                LuaLibAPI.lua_setglobal(state, "math");
            }

            if (GetIsFeatureEnabled(Features.IO))
            {
                LuaLibAPI.luaopen_io(state);
                LuaLibAPI.lua_setglobal(state, "io");
            }

            if (GetIsFeatureEnabled(Features.OS))
            {
                LuaLibAPI.luaopen_os(state);
                LuaLibAPI.lua_setglobal(state, "os");
            }

            if (GetIsFeatureEnabled(Features.Debug))
            {
                LuaLibAPI.luaopen_debug(state);
                LuaLibAPI.lua_setglobal(state, "debug");
            }

            if (GetIsFeatureEnabled(Features.CoroutineHelpers))
            {
                SetGlobal<Func<IntPtr, float, Task>>(luaConst_bluaInternalWait, bLuaInternal_Wait);
                SetGlobal<Func<IntPtr, bLuaValue, bLuaValue[], bLuaValue>>(luaConst_bluaInternalSpawn, bLuaInternal_Spawn);
                
                DoString(@$"coroutine.wait = {luaConst_bluaInternalWait}
                    coroutine.spawn = function(fn, ...)
                        return {luaConst_bluaInternalSpawn}(fn, {"{...}"})
                    end",
                    luaConst_bluaInternalCoroutineMacros);
            }

            if (GetIsFeatureEnabled(Features.CSharpPrintOverride))
            {
                SetGlobal<Action<bLuaValue[]>>(luaConst_bluaInternalPrintNew, bLuaInternal_Print);
                
                DoString(@$"print = function(...)
                        return {luaConst_bluaInternalPrintNew}({"{...}"})
                    end",
                    luaConst_bluaInternalPrint);
            }

            if (GetIsFeatureEnabled(Features.CSharpGarbageCollection))
            {
                internalLua_collectGarbage = DoString(@"return function() collectgarbage() end",
                    luaConst_bluaInternalCollectGarbage);
                StartGarbageCollecting();
            }
#endregion // Feature Handling

            if (settings.tickBehavior != bLuaSettings.TickBehavior.Manual)
            {
                // Start the threading needed for running bLua without a MonoBehaviour
                StartTicking();
            }

            if (settings.coroutineBehaviour == bLuaSettings.CoroutineBehaviour.ResumeOnTick)
            {
                OnTick?.RemoveListener(TickCoroutines);
                OnTick?.AddListener(TickCoroutines);
            }
        }

        private void DeInit()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            OnTick?.RemoveAllListeners();
            StopTicking();
            StopGarbageCollecting();

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

        private void ReInit()
        {
            reinitializing = true;
            DeInit();
            Init();
            reinitializing = false;
        }

        private void OnActiveSceneChanged(Scene _previous, Scene _new)
        {
            if (_previous.IsValid())
            {
                Dispose();
            }
        }
        
        public bool GetIsFeatureEnabled(Features _feature)
        {
            return ((Features)(int)settings.features).HasFlag(_feature);
        }
#endregion // Initialization

#region Tick
        /// <summary>
        /// This event is called whenever bLua ticks. Allows for bLua features (or developers) to listen for when ticking takes place.
        /// </summary>
        public UnityEvent OnTick { get; private set; } = new();

        private bool ticking;
        private bool requestStartTicking;
        private bool requestStopTicking;


        public void ManualTick()
        {
            InternalTick();
        }

        private async void Tick()
        {
            // Don't have two instances of the Tick thread; if this value is already set, don't continue
            if (ticking)
            {
                return;
            }

            ticking = true;
            requestStartTicking = false;

            // Only continue ticking while this value is set. This allows us to close the tick thread from outside of it when we need to
            while (!requestStopTicking
                && initialized)
            {
                if (settings.tickBehavior == bLuaSettings.TickBehavior.TickAtInterval)
                {
                    InternalTick();
                }

                await Task.Delay(Mathf.Max(settings.tickInterval, 1));
            }

            ticking = false;
            requestStopTicking = false;

            // If we've already re-requested to start ticking again, go ahead and handle that here as the previous Tick() call would have failed
            if (requestStartTicking
                && initialized)
            {
                StartTicking();
            }
        }

        private void InternalTick()
        {
            OnTick?.Invoke();
        }

        private void StartTicking()
        {
            Tick();
            requestStartTicking = true;
        }

        private void StopTicking()
        {
            requestStopTicking = true;
        }
#endregion // Tick
        
#region Coroutines
        private List<LuaCoroutine> coroutines = new();
        
        
        private void TickCoroutines()
        {
            using (profile_luaCo.Auto())
            {
                foreach (LuaCoroutine coroutine in coroutines.ToArray())
                {
                    // If there are any pause flags active, don't attempt to automatically resume the coroutine
                    if (coroutine.pauseFlags != CoroutinePauseFlags.NONE)
                    {
                        continue;
                    }

                    bLuaValue coroutineValue = new bLuaValue(this, coroutine.refId);
                    ResumeCoroutine(coroutineValue);
                    
                    // Remove coroutines that have completed, are dead, or in case of errors
                    if (Lua.IsDead(this, coroutine.state))
                    {
                        Lua.Unreference(this, coroutine.refId);
                        coroutines.Remove(coroutine);
                    }
                }
            }
        }

        public bLuaValue CallAsCoroutine(bLuaValue _fn, params object[] _args)
        {
            if (!GetIsFeatureEnabled(Features.Coroutines))
            {
                Debug.LogError($"Can't use {nameof(CallAsCoroutine)} when the {nameof(Features.Coroutines)} feature is disabled. Using {nameof(Call)} instead, and returning null.");

                Call(_fn, _args);
                return null;
            }

            bLuaValue coroutineValue = CreateCoroutine(_fn);
            
            ResumeCoroutine(coroutineValue, _args);

            return coroutineValue;
        }

        public bLuaValue CreateCoroutine(bLuaValue _fn)
        {
            IntPtr coroutineState = Lua.NewThread(state);

            // Copy the coroutine thread and pop a reference
            LuaLibAPI.lua_pushvalue(state, -1);
            int coroutineRefId = LuaXLibAPI.luaL_ref(state, Lua.LUA_REGISTRYINDEX);

            Lua.PushValue(state, _fn); // Push the function we want to call to the main stack
            LuaLibAPI.lua_xmove(state, coroutineState, 1); // Move the function to the coroutine state
            
            bLuaValue coroutineValue = new bLuaValue(this, coroutineRefId);
            
            // Track the created coroutine
            LuaCoroutine coroutine = new LuaCoroutine()
            {
                state = coroutineState,
                refId = coroutineValue.referenceId
            };
            coroutines.Add(coroutine);

            return coroutineValue;
        }
        
        public bool ResumeCoroutine(bLuaValue _coroutine, params object[] _args)
        {
            if (_coroutine == null)
            {
                Debug.LogError("Failed to resume coroutine because it was null.");
                return false;
            }
            
            LuaCoroutine luaCoroutine = coroutines.Find(c => c.refId == _coroutine.referenceId);
            if (luaCoroutine == null)
            {
                Debug.LogError("Failed to resume coroutine because it was not tracked by the bLua scheduler.");
                return false;
            }

            return Lua.ResumeThread(this, luaCoroutine.state, state, _args);
        }

        public void SetCoroutinePauseFlag(IntPtr _coroutineState, CoroutinePauseFlags _flag, bool _value)
        {
            LuaCoroutine coroutine = coroutines.Find(c => c.state == _coroutineState);
            if (coroutine == null)
            {
                Debug.LogError("Failed to set pause flag on coroutine because coroutine couldn't be found.");
                return;
            }

            if (_value)
            {
                coroutine.pauseFlags |= _flag;
            }
            else
            {
                coroutine.pauseFlags &= ~_flag;
            }
        }
#endregion // Coroutines

#region C# Garbage Collection
        private ConcurrentQueue<int> deleteQueue = new();

        private bLuaValue internalLua_collectGarbage;

        private bool garbageCollecting;
        private bool requestStartGarbageCollecting;
        private bool requestStopGarbageCollecting;


        public void ManualCollectGarbage()
        {
            InternalCollectGarbage();
        }

        private async void CollectGarbage()
        {
            // Don't have two instances of the CollectGarbage thread; if this value is already set, don't continue
            if (garbageCollecting)
            {
                return;
            }

            garbageCollecting = true;
            requestStartGarbageCollecting = false;

            // Only continue collecting while this value is set. This allows us to close the garbage collection thread from outside of it when we need to
            while (!requestStopGarbageCollecting
                && initialized)
            {
                InternalCollectGarbage();

                await Task.Delay(Mathf.Max(settings.cSharpGarbageCollectionInterval, 1));
            }

            garbageCollecting = false;
            requestStopGarbageCollecting = false;

            // If we've already re-requested to start garbage collecting again, go ahead and handle that here as the previous CollectGarbage() call would have failed
            if (requestStartGarbageCollecting
                && initialized)
            {
                StartGarbageCollecting();
            }
        }

        private void InternalCollectGarbage()
        {
            Call(internalLua_collectGarbage);

            while (deleteQueue.TryDequeue(out int refid))
            {
                Lua.Unreference(this, refid);
            }
        }

        private void StartGarbageCollecting()
        {
            if (!GetIsFeatureEnabled(Features.CSharpGarbageCollection))
            {
                Debug.LogError($"Can't use {nameof(StartGarbageCollecting)} when the {nameof(Features.CSharpGarbageCollection)} feature is disabled.");
                return;
            }
            
            if (settings.cSharpGarbageCollectionInterval <= 0)
            {
                Debug.LogError($"Can't use {nameof(StartGarbageCollecting)} when this instance's setting for {nameof(settings.cSharpGarbageCollectionInterval)} is less than 0.");
                return;
            }

            if (requestStopGarbageCollecting)
            {
                Debug.LogError($"Can't use {nameof(StartGarbageCollecting)} when this instance's {nameof(requestStopGarbageCollecting)} is true.");
                return;
            }
            
            CollectGarbage();
            requestStartGarbageCollecting = true;
        }

        private void StopGarbageCollecting()
        {
            requestStopGarbageCollecting = true;
        }

        public void MarkForCSharpGarbageCollection(int _referenceID)
        {
            deleteQueue.Enqueue(_referenceID);
        }
#endregion // C# Garbage Collection

#region Globals
        public bLuaValue GetGlobal(string _key)
        {
            int resType = LuaLibAPI.lua_getglobal(state, _key);
            bLuaValue result = Lua.PopValue(this);
            return result;
        }

        public void SetGlobal<T>(string _key, T _object)
        {
            Lua.PushObject(this, _object);
            LuaLibAPI.lua_setglobal(state, _key);
        }
#endregion // Globals

#region Errors
        private void ErrorInternal(string _message, string _luaStackTrace = null, string _cSharpStackTrace = null)
        {
            OnError?.Invoke(_message, _luaStackTrace != null ? _luaStackTrace : "");

            if (settings.internalVerbosity >= bLuaSettings.InternalErrorVerbosity.Normal
                && !string.IsNullOrEmpty(_luaStackTrace))
            {
                _luaStackTrace = _luaStackTrace.TrimStart();
                _message += "\n" + _luaStackTrace; 
            }

            if (settings.internalVerbosity >= bLuaSettings.InternalErrorVerbosity.Verbose
                && !string.IsNullOrEmpty(_cSharpStackTrace))
            {
                _cSharpStackTrace = _cSharpStackTrace.TrimStart();
                _message += "\n" + _cSharpStackTrace;
            }

            if (settings.internalVerbosity >= bLuaSettings.InternalErrorVerbosity.Minimal)
            {
                Debug.LogError(_message);
            }
        }

        public void ErrorFromLua(string _message, string _luaStackTrace = null)
        {
            if (string.IsNullOrEmpty(_luaStackTrace))
            {
                _luaStackTrace = Lua.TraceMessage(this);
            }

            ErrorInternal(_message, _luaStackTrace);
        }

        public void ErrorFromCSharp(Exception _exception, string _prependedErrorInfo = "")
        {
            Exception ex = _exception;
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            ErrorFromCSharp($"{_prependedErrorInfo}{ex.Message}", $"{ex.StackTrace}");
        }
        
        public void ErrorFromCSharp(string _message, string _cSharpStackTrace = null)
        {
            string luaStackTrace = Lua.TraceMessage(this);

            ErrorInternal(_message, luaStackTrace, _cSharpStackTrace);
        }
#endregion // Errors

        /// <summary>
        /// Loads the given string as a Lua chunk and runs it.
        /// </summary>
        /// <param name="_name"> The chunk name, used for debug information and error messages </param>
        /// <param name="_code"> The Lua code to load </param>
        public bLuaValue DoString(string _code, string _name = unnamedChunkName, bLuaValue _environment = null)
        {
            LoadString(_code, _name, _environment);
            
            using (profile_luaCallInner.Auto())
            {
                int result = LuaLibAPI.lua_pcallk(state, 0, 1, 0, 0, IntPtr.Zero);
                
                if (result != 0)
                {
                    string error = Lua.GetString(state, -1);
                    Lua.Pop(state, 1);
                    ErrorFromLua($"{bLuaError.error_inBuffer}", $"{bLuaError.error_stackTracebackPrepend}{error}");
                    return null;
                }
                
                return Lua.PopValue(this);
            }
        }

        /// <summary>
        /// Loads the given string as a Lua chunk. This function only loads the chunk; it does not run it.
        /// </summary>
        /// <param name="_code"> The Lua code to load </param>
        /// <param name="_name"> The chunk name, used for debug information and error messages </param>
        /// <param name="_environment"> The table to use as this chunk's global table </param> 
        public void LoadString(string _code, string _name = unnamedChunkName, bLuaValue _environment = null)
        {
            if (!initialized)
            {
                Debug.LogError($"Attempt to {nameof(LoadString)} when instance is not initialized.");
                return;
            }

            using (profile_luaCall.Auto())
            {
                int result = LuaXLibAPI.luaL_loadbufferx(state, _code, (ulong)_code.Length, _name, null);
                if (result != 0)
                {
                    string error = Lua.GetString(state, -1);
                    Lua.Pop(state, 1);
                    ErrorFromLua($"{bLuaError.error_inBuffer}", $"{bLuaError.error_stackTracebackPrepend}{error}");
                    return;
                }

                if (_environment != null)
                {
                    Lua.PushValue(this, _environment); // Pushes the given table
                    LuaLibAPI.lua_createtable(state, 0, 0); // Pushes an empty table
                    LuaLibAPI.lua_getglobal(state, "_G"); // Pushes _G
                    LuaLibAPI.lua_setfield(state, -2, Lua.StringToIntPtr("__index")); // Sets the stack at index to the -1 stack index's value, and pops the value
                    LuaLibAPI.lua_setmetatable(state, -2); // Pops -1 stack index and sets it to the stack value at index
                    LuaLibAPI.lua_setupvalue(state, -2, 1); // Assigns -1 stack index to the upvalue for value in stack at index
                }
            }
        }

        /// <summary>
        /// Calls a passed Lua function.
        /// </summary>
        /// <param name="_fn"> The Lua function being called </param>
        /// <param name="_args"> Arguments that will be passed into the called Lua function </param>
        /// <returns> The output from the called Lua function </returns>
        public bLuaValue Call(bLuaValue _fn, params object[] _args)
        {
            if (!initialized)
            {
                Debug.LogError("Attempt to Call function when instance is not initialized.");
                return null;
            }

            using (profile_luaCall.Auto())
            {
                Lua.PushValue(this, _fn);

                foreach (var arg in _args)
                {
                    Lua.PushObject(this, arg);
                }

                int result;
                using (profile_luaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(state, _args.Length, 1, 0, 0L, IntPtr.Zero);
                }
                if (result != 0)
                {
                    string error = Lua.GetString(state, -1);
                    Lua.Pop(state, 1);
                    ErrorFromLua($"{bLuaError.error_inFunctionCall}", $"{error}");
                    return null;
                }

                return Lua.PopValue(this);
            }
        }

#region C Functions called from Lua
        private void bLuaInternal_Error(string e)
        {
            // Reformat the original error message (which shows a line number etc) to just the error msg, and
            // let the error handler grab a full trace and parameterize it properly
            string[] splitMessage = e.Split(':');
            if (splitMessage.Length >= 3)
            {
                string messageStartingAfterTrace = "";
                for (int i = 2; i < splitMessage.Length; i++)
                {
                    messageStartingAfterTrace += splitMessage[i];
                }
                e = messageStartingAfterTrace.TrimStart();
            }

            string stackTrace = "";
            if (settings.internalVerbosity >= bLuaSettings.InternalErrorVerbosity.Normal)
            {
                stackTrace = Lua.TraceMessage(this);
            }

            ErrorFromLua(e, stackTrace);
        }

        private void bLuaInternal_ErrorDebug(string e, string t)
        {
            // Reformat the original error message (which shows a line number etc) to just the error msg, and
            // let the error handler grab a full trace and parameterize it properly
            string[] splitMessage = e.Split(':');
            if (splitMessage.Length >= 3)
            {
                string messageStartingAfterTrace = "";
                for (int i = 2; i < splitMessage.Length; i++)
                {
                    messageStartingAfterTrace += splitMessage[i];
                }
                e = messageStartingAfterTrace.TrimStart();
            }

            ErrorFromLua(e, t);
        }

        private bLuaValue bLuaInternal_CoroutineCreate([bLuaParam_State] IntPtr _state, bLuaValue _fn)
        {
            return CreateCoroutine(_fn);
        }
        
        private void bLuaInternal_CoroutineYield([bLuaParam_State] IntPtr _state)
        {
            Lua.YieldThread(this, _state, 0);
        }
        
        private bool bLuaInternal_CoroutineResume([bLuaParam_State] IntPtr _state, bLuaValue _thread, bLuaValue[] _args)
        {
            // Safely convert bLuaValue[] to object[]
            object[] args = new object[_args.Length];
            for (int i = 0; i < _args.Length; i++)
            {
                args[i] = _args[i];
            }
            
            return Lua.ResumeThread(this, _thread.ToPointer(), _state, args);
        }
        
        private void bLuaInternal_Print(bLuaValue[] _args)
        {
            OnPrint?.Invoke(_args);
        }

        private float bLuaInternal_Time()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)EditorApplication.timeSinceStartup - Time.time;
            }

            return Time.time;
#else
            return Time.time;
#endif // UNITY_EDITOR
        }

        private async Task bLuaInternal_Wait([bLuaParam_State] IntPtr _state, float _t)
        {
            await Task.Delay((int)(_t * 1000f));
        }
        
        private bLuaValue bLuaInternal_Spawn([bLuaParam_State] IntPtr _state, bLuaValue _fn, bLuaValue[] _args)
        {
            bLuaValue coroutineValue = bLuaInternal_CoroutineCreate(_state, _fn);
            bLuaInternal_CoroutineResume(_state, coroutineValue, _args);

            return coroutineValue;
        }
#endregion // C Functions called from Lua
        
#region Userdata
        /// <summary>
        /// Registers all C# types in all assemblies with the <see cref="bLuaUserDataAttribute"/> attribute as Lua userdata for this <see cref="bLuaInstance"/>.
        /// </summary>
        public void RegisterAllBLuaUserData()
        {
            bLuaUserData.RegisterAllBLuaUserData(this);
        }

        /// <summary>
        /// Registers a given C# type as Lua userdata on this particular instance. Does not require the <see cref="bLuaUserDataAttribute"/> attribute.
        /// See <see cref="bLuaUserData.Register"/> for a more detailed userdata type register function.
        /// </summary>
        public void RegisterUserData(Type _type)
        {
            bLuaUserData.Register(this, _type);
        }
#endregion // Userdata

#region Internal Classes
        public struct StringCacheEntry
        {
            public string key;
            public bLuaValue value;
        }
#endregion // Internal Classes

#region Static
        private static Dictionary<IntPtr, bLuaInstance> instanceRegistry = new();

        private static ProfilerMarker profile_luaGC = new("Lua.GC");
        private static ProfilerMarker profile_luaCo = new("Lua.Coroutine");
        private static ProfilerMarker profile_luaCall = new("Lua.Call");
        private static ProfilerMarker profile_luaCallInner = new("Lua.CallInner");
        
        
        public static bLuaInstance GetInstanceByState(IntPtr _state)
        {
            instanceRegistry.TryGetValue(_state, out bLuaInstance instance);
            return instance;
        }

        public static int GetInstanceCount()
        {
            return instanceRegistry.Count;
        }
#endregion // Static
    }
} // bLua namespace
