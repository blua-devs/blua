using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

            if (GUILayout.Button("Hot Reload"))
            {
                bLuaComponent component = target as bLuaComponent;
                if (component != null)
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
    const string defaultInstanceKey = "default";
    static Dictionary<string, bLuaInstance> instanceByKey = new Dictionary<string, bLuaInstance>();
    static bLuaInstance GetOrCreateInstance(string _key)
    {
        if (string.IsNullOrWhiteSpace(_key))
        {
            _key = defaultInstanceKey;
        }

        if (!instanceByKey.ContainsKey(_key)
            || instanceByKey[_key] == null)
        {
            instanceByKey[_key] = new bLuaInstance();
        }
        return instanceByKey[_key];
    }

    /// <summary> When true, this bLuaComponent will run its code when Unity's Start event fires. If this is set to false, you will need to 
    /// tell the bLuaComponent when to run the code via RunCode(). </summary>
    [SerializeField]
    bool runCodeOnStart = true;

    /// <summary> bLuaComponents that share an instance key with both exist in the same bLuaInstance. There are large performance benefits to
    /// putting all of your game's Lua in the bLuaInstance. In rare cases where you want unrelated parts of your game that don't interact with the
    /// same content to have their own bLuaInstance, you would set this value to differ. </summary>
    [SerializeField]
    string instanceKey;

    /// <summary> The name of the Lua chunk. Used for debug information and error messages. </summary>
    [SerializeField]
    string chunkName = "default_component";

    /// <summary> The name of the environment to load the Lua chunk into. Set to null/empty/whitespace to load into the global Lua environment. </summary>
    [SerializeField]
    string environmentName;

    /// <summary> The code that will be run on this component. </summary>
    [SerializeField]
    [TextArea(2, 512)]
    string code;

    bLuaInstance instance;


    private void Awake()
    {
        instance = GetOrCreateInstance(instanceKey);
    }

    private void Start()
    {
        if (runCodeOnStart)
        {
            RunCode();
        }
    }


    public void HotReload()
    {
        ranCode = false;
        RunCode();
    }

    bool ranCode = false;
    public void RunCode()
    {
        if (!ranCode)
        {
            instance.ExecBuffer(chunkName, code, 0, environmentName);
            ranCode = true;
        }
    }
}
