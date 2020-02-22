using System.Runtime.InteropServices;

namespace Spreads.Threading
{
    /// <summary>
    /// Make all fields of inherited classes padded by 48 bytes,
    /// which together with the object header and the method
    /// table pointer give 64 bytes. 
    /// </summary>
    public class LeftPad48
    {
        private Padding48 _padding;
    }

    /// <summary>
    /// Make all fields of inherited classes padded by 112 bytes,
    /// which together with the object header and the method
    /// table pointer give 128 bytes. 
    /// </summary>
    public class LeftPad112 : LeftPad48
    {
        private Padding112 _padding;
    }

    // We could have used only the Size attribute
    
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 16)]
    internal readonly struct Padding16
    {
        private readonly long _padding0;
        private readonly long _padding1;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 32)]
    internal readonly struct Padding32
    {
        private readonly Padding16 _padding0;
        private readonly Padding16 _padding1;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 40)]
    internal readonly struct Padding40
    {
        private readonly Padding32 _padding32;
        private readonly long _padding8;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 48)]
    internal readonly struct Padding48
    {
        private readonly Padding32 _padding32;
        private readonly Padding16 _padding16;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 56)]
    internal readonly struct Padding56
    {
        private readonly Padding40 _padding40;
        private readonly Padding16 _padding16;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
    internal readonly struct Padding64
    {
        private readonly Padding32 _padding0;
        private readonly Padding32 _padding1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 112)]
    internal readonly struct Padding112
    {
        private readonly Padding64 _padding0;
        private readonly Padding48 _padding1;
    }
}