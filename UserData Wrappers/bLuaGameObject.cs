using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;

[bLuaUserData]
public class bLuaGameObject
{
    [bLuaHidden]
    public GameObject __gameObject;


    public string name
    {
        get
        {
            return __gameObject.name;
        }
        set
        {
            __gameObject.name = value;
        }
    }

    public bLuaGameObject(GameObject _gameObject)
    {
        __gameObject = _gameObject;
    }
}
