using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;

namespace bLua.ExampleUserData
{
    [bLuaUserData(reliantUserData = new Type[1] { typeof(bLuaVector3) })]
    public class bLuaGameObject
    {
        [bLuaHidden]
        public GameObject __gameObject;

        public string name
        {
            get
            {
                if (__gameObject != null)
                {
                    return __gameObject.name;
                }
                return string.Empty;
            }
            set
            {
                if (__gameObject != null)
                {
                    __gameObject.name = value;
                }
            }
        }

        public bLuaVector3 position
        {
            get
            {
                if (__gameObject != null
                    && __gameObject.transform != null)
                {
                    return __gameObject.transform.position;
                }
                return Vector3.zero;
            }
            set
            {
                if (__gameObject != null
                    && __gameObject.transform != null)
                {
                    __gameObject.transform.position = value;
                }
            }
        }


        public bLuaGameObject(GameObject _gameObject)
        {
            __gameObject = _gameObject;
        }


        public bLuaGameObject Duplicate()
        {
            if (__gameObject != null)
            {
                GameObject duplicatedGameObject = MonoBehaviour.Instantiate(__gameObject);
                return new bLuaGameObject(duplicatedGameObject);
            }
            return null;
        }

        public void Destroy()
        {
            if (__gameObject != null)
            {
                MonoBehaviour.Destroy(__gameObject);
            }
        }
    }
} // bLua.ExampleUserData namespace
