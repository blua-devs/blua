using System;

namespace bLua
{
    /// <summary>
    /// Allows a class, struct, etc for being registered as userdata, allowing Lua to access it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class bLuaUserDataAttribute : Attribute
    {
        /// <summary>
        /// Any types added to this array will be registered just before this type is, making sure you don't accidentally
        /// register userdata that uses X type before X is registered (ex. bLuaGameObject might be reliant on Vector3
        /// because of the position property).
        /// </summary>
        public Type[] reliantUserData;
    }
    
    /// <summary>
    /// Prevents a method, property, field, etc from being registered as userdata, preventing Lua from accessing it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Constructor)]
    public class bLuaHiddenAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a parameter to be ignored when passing parameters in from Lua to the C# method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class bLuaParam_Ignored : Attribute
    {
    }
    
    /// <summary>
    /// Marks a parameter to automatically have the calling Lua State passed in as an IntPtr. Inherits from <see cref="bLuaParam_Ignored"/>.
    /// </summary>
    public class bLuaParam_State : bLuaParam_Ignored
    {
    }
} // bLua namespace
