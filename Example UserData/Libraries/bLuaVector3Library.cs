using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData(reliantUserData = new Type[1] { typeof(bLuaVector3) })]
    public class bLuaVector3Library
    {
        public static bLuaVector3 zero
        {
            get
            {
                return Vector3.zero;
            }
        }


        public static bLuaVector3 New(float _x, float _y, float _z)
        {
            return new Vector3(_x, _y, _z);
        }
    }
} // bLua.ExampleUserData namespace
