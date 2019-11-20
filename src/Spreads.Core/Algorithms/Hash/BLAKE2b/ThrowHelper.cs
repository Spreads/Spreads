using System;
using System.Runtime.CompilerServices;

namespace Spreads.Algorithms.Hash.BLAKE2b
{
    internal static class ThrowHelper
    {
        public static void ThrowIfIsRefOrContainsRefs<T>()
        {
            if (
#if BUILTIN_SPAN
				RuntimeHelpers.IsReferenceOrContainsReferences<T>()
#else
                Native.VecTypeHelper<T>.RuntimeVecInfo.IsReferenceOrContainsReferences
#endif
            )

                throw new NotSupportedException("This method may only be used with value types that do not contain reference type fields.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HashFinalized() => throw new InvalidOperationException("Hash has already been finalized.");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void NoBigEndian() => Spreads.ThrowHelper.FailFast("Big-endian platforms not supported");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DigestInvalidLength(int max) => throw new ArgumentOutOfRangeException("digestLength", $"Value must be between 1 and {max}");

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void KeyTooLong(int max) => throw new ArgumentException($"Key must be between 0 and {max} bytes in length", "key");
    }
}
