using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData(reliantUserData = new Type[1] { typeof(bLuaVector3) } )]
    public static class bLuaVector3ExtensionLibrary
    {
        public static bLuaVector3 Normalize(this bLuaVector3 v)
        {
            return v.__vector3.normalized;
        }
    }
} // bLua.ExampleUserData namespace
