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
#pragma warning disable 169
        private Padding48 _padding;
#pragma warning restore 169
    }

    /// <summary>
    /// Make all fields of inherited classes padded by 112 bytes,
    /// which together with the object header and the method
    /// table pointer give 128 bytes.
    /// </summary>
    public class LeftPad112 : LeftPad48
    {
#pragma warning disable 169
        private Padding112 _padding;
#pragma warning restore 169
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal readonly struct Padding16
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    internal readonly struct Padding24
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    internal readonly struct Padding32
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 40)]
    internal readonly struct Padding40
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    internal readonly struct Padding48
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 56)]
    internal readonly struct Padding56
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal readonly struct Padding64
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 80)]
    internal readonly struct Padding80
    {
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 112)]
    internal readonly struct Padding112
    {
    }
}
