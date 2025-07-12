using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData]
    public struct bLuaVector3
    {
        [bLuaHidden]
        public Vector3 __vector3;

        public float x
        {
            get
            {
                return __vector3.x;
            }
            set
            {
                __vector3.x = value;
            }
        }

        public float y
        {
            get
            {
                return __vector3.y;
            }
            set
            {
                __vector3.y = value;
            }
        }

        public float z
        {
            get
            {
                return __vector3.z;
            }
            set
            {
                __vector3.z = value;
            }
        }


        public bLuaVector3(Vector3 _vector3)
        {
            __vector3 = _vector3;
        }

        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }
        
        public static implicit operator string(bLuaVector3 v)
        {
            return v.ToString();
        }
        
        public static implicit operator Vector3(bLuaVector3 v)
        {
            return v.__vector3;
        }
        
        public static implicit operator bLuaVector3(Vector3 v)
        {
            return new bLuaVector3(v);
        }
        
        public static bLuaVector3 operator +(bLuaVector3 a, bLuaVector3 b)
        {
            return a.__vector3 + b.__vector3;
        }
        
        public static bLuaVector3 operator -(bLuaVector3 a, bLuaVector3 b)
        {
            return a.__vector3 - b.__vector3;
        }
        
        public static bLuaVector3 operator -(bLuaVector3 v)
        {
            return -v.__vector3;
        }
        
        public static bLuaVector3 operator *(bLuaVector3 a, bLuaVector3 b)
        {
            return Vector3.Cross(a, b);
        }
        
        public static bLuaVector3 operator *(bLuaVector3 v, float f)
        {
            return v.__vector3 * f;
        }
        
        public static bLuaVector3 operator *(float f, bLuaVector3 v)
        {
            return v.__vector3 * f;
        }
        
        public static bLuaVector3 operator *(bLuaVector3 v, double d)
        {
            return v * (float)d;
        }
        
        public static bLuaVector3 operator *(double d, bLuaVector3 v)
        {
            return v * (float)d;
        }
        
        public static bool operator ==(bLuaVector3 a, bLuaVector3 b)
        {
            return a.Equals(b);
        }
        
        public static bool operator !=(bLuaVector3 a, bLuaVector3 b)
        {
            return !(a == b);
        }
        
        public bool Equals(bLuaVector3 v)
        {
            return __vector3.Equals(v.__vector3);
        }
        
        public override bool Equals(object o)
        {
            return o is bLuaVector3 v && Equals(v);
        }
        
        public override int GetHashCode()
        {
            return __vector3.GetHashCode();
        }
    }
} // bLua.ExampleUserData namespace
