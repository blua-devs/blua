using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using bLua;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary> A library of helpful functions that can be used in bLua. </summary>
[bLuaUserData]
public class bLuaGlobalLibrary
{
    /// <summary> Called whenever the print() function is called in this library. </summary>
    public static UnityEvent<string> OnLog = new UnityEvent<string>();


    #region Fields
    /// <summary> Returns a Lua-accessible version of Unity's Time.time. Also works when not in play mode. </summary>
    public static float time
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)EditorApplication.timeSinceStartup - Time.time;
            }

            return Time.time;
#else
            return Time.time;
#endif
        }
    }
    #endregion // Fields

    #region Methods
    /// <summary> Prints a string to Unity's logs. </summary>
    public static void print(string _string)
    {
        Debug.Log(_string);

        OnLog.Invoke(_string);
    }
    #endregion // Methods
}