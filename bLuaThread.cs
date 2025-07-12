using System;

namespace bLua
{
    [Flags]
    public enum CoroutinePauseFlags
    {
        NONE = 0,
        BLUA_CSHARPASYNCAWAIT = 1,
    }
        
    public class LuaCoroutine
    {
        public IntPtr state;
        public int refId;
        public CoroutinePauseFlags pauseFlags;
    }
} // bLua namespace
