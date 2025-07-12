using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua
{
    public class bLuaException : Exception
    {
        public bLuaException(string message) : base(message)
        {
        }
    }
} // bLua namespace
