using UnityEngine;
using bLua;
using bLua.ExampleUserData;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

#if UNITY_EDITOR
[CustomEditor(typeof(bLuaComponent))]
public class bLuaComponentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (EditorApplication.isPlaying)
        {
            GUILayout.Space(20);

            bLuaComponent component = target as bLuaComponent;
            if (component != null)
            {
                if (!component.HasRanCode())
                {
                    if (GUILayout.Button("Run Code"))
                    {
                        component.RunCode();
                    }                    
                }
                
                if (GUILayout.Button("Hot Reload"))
                {
                    component.HotReload();
                }
            }
        }
    }
}
#endif // UNITY_EDITOR

public class bLuaComponent : MonoBehaviour
{
    public static bLuaInstance instance;
    private bLuaValue environment;

    /// <summary>
    /// When true, run code when Unity's Start event fires. If this is set to false, you will need to manually call RunCode() to run the code.
    /// </summary>
    [SerializeField] private bool runCodeOnStart = true;

    /// <summary>
    /// When true, attempt to run Lua functions with the following names: "Start, Update, OnDestroy" when their respective Unity functions
    /// are called by Unity. You do not need to add any or all of these functions if you don't want to.
    /// </summary>
    [SerializeField] private bool runMonoBehaviourEvents = true;

    /// <summary>
    /// The name of the Lua chunk. Used for debug information and error messages.
    /// </summary>
    [SerializeField] private string chunkName = "default_component";

    /// <summary>
    /// The code that will be run on this component.
    /// </summary>
    [SerializeField]
    [TextArea(2, 512)]
    private string code;


    private void Awake()
    {
        if (instance == null)
        {
            instance = new bLuaInstance(new bLuaSettings()
            {
                features = bLuaSettings.SANDBOX_ALL_EXPERIMENTAL,
                tickBehavior = bLuaSettings.TickBehavior.Manual, // We tick the instance on Update
                coroutineBehaviour = bLuaSettings.CoroutineBehaviour.ResumeOnTick // We resume all coroutines on Tick
            });
            
            // Override print to log in Unity
            instance.OnPrint.AddListener(args =>
            {
                string print = "";
                
                foreach (bLuaValue arg in args)
                {
                    print += arg.ToString();
                }
                
                Debug.Log(print);
            });
        }

        if (environment == null)
        {
            // Set up the global environment with any properties and functions we want
            environment = bLuaValue.CreateTable(instance);
            environment.Set("Vector3", new bLuaVector3Library());
            environment.Set("GameObject", new bLuaGameObjectLibrary());
            environment.Set("gameObject", new bLuaGameObject(gameObject));
        }
    }

    private void Start()
    {
        if (runCodeOnStart)
        {
            RunCode();
        }

        if (runMonoBehaviourEvents)
        {
            RunEvent("Start");
        }
    }

    private void Update()
    {
        instance.ManualTick();

        if (runMonoBehaviourEvents)
        {
            RunEvent("Update");
        }
    }

    private void OnDestroy()
    {
        if (runMonoBehaviourEvents)
        {
            RunEvent("OnDestroy");
        }
    }


    public void HotReload()
    {
        ranCode = false;
        RunCode();
    }

    private bool ranCode;
    public void RunCode()
    {
        if (!ranCode)
        {
            instance.DoString(code, chunkName, environment);
            ranCode = true;
        }
    }

    public bool HasRanCode()
    {
        return ranCode;
    }

    public void RunEvent(string _name)
    {
        if (ranCode)
        {
            bLuaValue func = environment.Get(_name);
            if (func != null
                && func.luaType == LuaType.Function)
            {
                instance.CallAsCoroutine(func);
            }
        }
    }
}
