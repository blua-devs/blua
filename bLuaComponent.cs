using System;
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
    public static bLuaInstance instance = new bLuaInstance();

    /// <summary> When true, this bLuaComponent will run its code when Unity's Start event fires. If this is set to false, you will need to 
    /// tell the bLuaComponent when to run the code via RunCode(). </summary>
    [SerializeField]
    bool runCodeOnStart = true;

    /// <summary> The name of the Lua chunk. Used for debug information and error messages. </summary>
    [SerializeField]
    string chunkName = "default_component";

    /// <summary> The code that will be run on this component. </summary>
    [SerializeField]
    [TextArea(2, 512)]
    string code;


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
            // Register any userdata that is needed
            instance.RegisterUserData(typeof(bLuaGameObject));

            // Setup the global environment with any properties and functions we want
            bLuaValue env = bLuaValue.CreateTable(instance);
            env.Set("gameObject", new bLuaGameObject(this.gameObject));

            // Run the code
            instance.DoBuffer(chunkName, code, env);
            ranCode = true;
        }
    }
}
