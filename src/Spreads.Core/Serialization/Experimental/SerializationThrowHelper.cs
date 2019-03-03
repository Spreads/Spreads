using System.Runtime.CompilerServices;

namespace Spreads.Serialization.Experimental
{
    /// <summary>
    /// Throw helper specific to serialization.
    /// </summary>
    internal static class SerializationThrowHelper
    {
        // Note: Cannot confirm that, but having verbatim string in a method
        // prevents inlining because sting becomes a part of method body.

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowBadTypeEnum(byte typeEnumValue)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException($"TypeEnum value is out of range 0 - 126: {typeEnumValue}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowFixedSizeOutOfRange(byte typeSizeValue)
        {
            ThrowHelper.ThrowArgumentOutOfRangeException($"Type size value is out of range 1 - 128: {typeSizeValue}");
        }
    }
}