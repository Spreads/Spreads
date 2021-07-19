// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;
#if HAS_INTRINSICS
using System.Runtime.Intrinsics;
using X86 = System.Runtime.Intrinsics.X86;
using Arm = System.Runtime.Intrinsics.Arm;
#endif

namespace Spreads.Utils
{
    /// <summary>
    /// Bit operations utils.
    /// </summary>
    public static class BitUtils
    {
        /// <summary>
        /// Find the closest positive power of 2 greater or equal to an integer value.
        /// </summary>
        /// <param name="value">The value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NextPow2(int value)
        {
            unchecked
            {
                return 1 << (32 - LeadingZeroCount(value - 1));
            }
        }

        /// <summary>
        /// Find the closest positive power of 2 greater or equal to an integer value.
        /// </summary>
        /// <param name="value">The value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long NextPow2(long value)
        {
            unchecked
            {
                return 1L << (64 - LeadingZeroCount(value - 1L));
            }
        }

        /// <summary>
        /// Find the closest positive power of 2 value less or equal to an integer value.
        /// </summary>
        /// <param name="value">The value</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PrevPow2(int value)
        {
            unchecked
            {
                return 1 << (31 - LeadingZeroCount(value));
            }
        }

        /// <summary>
        /// Align a value to the next multiple up of alignment.
        /// If the value equals an alignment multiple then it is returned unchanged.
        /// <para>
        /// This method executes without branching. This code is designed to be use in the fast path and should not
        /// be used with negative numbers. Negative numbers will result in undefined behavior.
        ///
        /// </para>
        /// </summary>
        /// <param name="value">     to be aligned up. </param>
        /// <param name="alignment"> to be used. </param>
        /// <returns> the value aligned to the next boundary. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Align(int value, int alignment)
        {
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Align(long value, long alignment)
        {
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        /// <summary>
        /// Is a number even.
        /// </summary>
        /// <param name="value"> to check. </param>
        /// <returns> true if the number is even otherwise false. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEven(int value)
        {
            return (value & 1) == 0;
        }

        /// <summary>
        /// Is an address aligned on a boundary.
        /// </summary>
        /// <param name="address">   to be tested. </param>
        /// <param name="alignment"> boundary the address is tested against. </param>
        /// <returns> true if the address is on the aligned boundary otherwise false. </returns>
        /// <exception cref="ArgumentException"> if the alignment is not a power of 2` </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAligned(long address, int alignment)
        {
            if (AdditionalCorrectnessChecks.Enabled && !IsPow2((uint) alignment))
                ThrowHelper.ThrowArgumentException($"Alignment must be a power of 2: alignment={alignment}");

            return (address & (alignment - 1)) == 0;
        }

        /// <summary>
        /// Count the number of trailing zero bits in an integer value.
        /// Similar in behavior to the x86 instruction TZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(int value)
        {
            unchecked
            {
#if HAS_INTRINSICS
                if (X86.Bmi1.IsSupported)
                    // TZCNT contract is 0->32
                    return (int) X86.Bmi1.TrailingZeroCount((uint) value);

                if (Arm.ArmBase.IsSupported)
                    return Arm.ArmBase.LeadingZeroCount(Arm.ArmBase.ReverseElementBits(value));
#endif
#if HAS_BITOPERATIONS
                return System.Numerics.BitOperations.TrailingZeroCount(value);
#else
                // HD, Figure 5-14
                int y;
                if (value == 0)
                {
                    return 32;
                }

                int n = 31;
                y = value << 16;
                if (y != 0)
                {
                    n = n - 16;
                    value = y;
                }

                y = value << 8;
                if (y != 0)
                {
                    n = n - 8;
                    value = y;
                }

                y = value << 4;
                if (y != 0)
                {
                    n = n - 4;
                    value = y;
                }

                y = value << 2;
                if (y != 0)
                {
                    n = n - 2;
                    value = y;
                }

                return n - ((int) ((uint) (value << 1) >> 31));
#endif
            }
        }

        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(int value)
        {
            unchecked
            {
#if HAS_INTRINSICS
                if (X86.Lzcnt.IsSupported)
                    // LZCNT contract is 0->32
                    return (int) X86.Lzcnt.LeadingZeroCount((uint) value);

                if (Arm.ArmBase.IsSupported)
                    return Arm.ArmBase.LeadingZeroCount(value);
#endif
#if HAS_BITOPERATIONS
                return System.Numerics.BitOperations.LeadingZeroCount((uint) value);
#else
                // HD, Figure 5-6
                if (value == 0)
                {
                    return 32;
                }

                int n = 1;
                if ((int) ((uint) value >> 16) == 0)
                {
                    n += 16;
                    value <<= 16;
                }

                if ((int) ((uint) value >> 24) == 0)
                {
                    n += 8;
                    value <<= 8;
                }

                if ((int) ((uint) value >> 28) == 0)
                {
                    n += 4;
                    value <<= 4;
                }

                if ((int) ((uint) value >> 30) == 0)
                {
                    n += 2;
                    value <<= 2;
                }

                n -= (int) ((uint) value >> 31);
                return n;

#endif
            }
        }

        /// <summary>
        /// Count the number of leading zero bits in a mask.
        /// Similar in behavior to the x86 instruction LZCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(long value)
        {
#if HAS_INTRINSICS
            if (X86.Lzcnt.X64.IsSupported)
                return (int) X86.Lzcnt.X64.LeadingZeroCount((ulong) value);

            if (Arm.ArmBase.Arm64.IsSupported)
                return Arm.ArmBase.Arm64.LeadingZeroCount((ulong) value);
#endif
#if HAS_BITOPERATIONS
            return System.Numerics.BitOperations.LeadingZeroCount((ulong) value);
#else
            unchecked
            {
                // HD, Figure 5-6
                if (value == 0L)
                {
                    return 64;
                }

                int n = 1;
                if ((long) ((ulong) value >> 32) == 0)
                {
                    n += 32;
                    value <<= 32;
                }

                if ((long) ((ulong) value >> 48) == 0)
                {
                    n += 16;
                    value <<= 16;
                }

                if ((long) ((ulong) value >> 56) == 0)
                {
                    n += 8;
                    value <<= 8;
                }

                if ((long) ((ulong) value >> 60) == 0)
                {
                    n += 4;
                    value <<= 4;
                }

                if ((long) ((ulong) value >> 62) == 0)
                {
                    n += 2;
                    value <<= 2;
                }

                n -= (int) ((ulong) value >> 63);
                return n;
            }
#endif
        }

        /// <summary>
        /// Returns the population count (number of bits set) of a mask.
        /// Similar in behavior to the x86 instruction POPCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(uint value)
        {
#if HAS_INTRINSICS
            if (X86.Popcnt.IsSupported)
                return (int) X86.Popcnt.PopCount(value);

            if (Arm.AdvSimd.Arm64.IsSupported)
            {
                // PopCount works on vector so convert input value to vector first.

                Vector64<uint> input = Vector64.CreateScalar(value);
                Vector64<byte> aggregated = Arm.AdvSimd.Arm64.AddAcross(Arm.AdvSimd.PopCount(input.AsByte()));
                return aggregated.ToScalar();
            }
#endif
            return SoftwareFallback(value);

            static int SoftwareFallback(uint value)
            {
                const uint c1 = 0x_55555555u;
                const uint c2 = 0x_33333333u;
                const uint c3 = 0x_0F0F0F0Fu;
                const uint c4 = 0x_01010101u;

                value -= (value >> 1) & c1;
                value = (value & c2) + ((value >> 2) & c2);
                value = (((value + (value >> 4)) & c3) * c4) >> 24;

                return (int) value;
            }
        }

        /// <summary>
        /// Returns the population count (number of bits set) of a mask.
        /// Similar in behavior to the x86 instruction POPCNT.
        /// </summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(ulong value)
        {
#if HAS_INTRINSICS
            if (X86.Popcnt.X64.IsSupported)
                return (int) X86.Popcnt.X64.PopCount(value);

            if (Arm.AdvSimd.Arm64.IsSupported)
            {
                // PopCount works on vector so convert input value to vector first.
                Vector64<ulong> input = Vector64.Create(value);
                Vector64<byte> aggregated = Arm.AdvSimd.Arm64.AddAcross(Arm.AdvSimd.PopCount(input.AsByte()));
                return aggregated.ToScalar();
            }

            if (X86.Popcnt.IsSupported || Arm.AdvSimd.Arm64.IsSupported)
            {
                return PopCount((uint) value) // lo
                       + PopCount((uint) (value >> 32)); // hi
            }

#endif
            return SoftwareFallback(value);

            static int SoftwareFallback(ulong value)
            {
                const ulong c1 = 0x_55555555_55555555ul;
                const ulong c2 = 0x_33333333_33333333ul;
                const ulong c3 = 0x_0F0F0F0F_0F0F0F0Ful;
                const ulong c4 = 0x_01010101_01010101ul;

                value -= (value >> 1) & c1;
                value = (value & c2) + ((value >> 2) & c2);
                value = (((value + (value >> 4)) & c3) * c4) >> 56;

                return (int) value;
            }
        }

        /// <summary>
        /// Is a value a positive power of two.
        /// </summary>
        /// <param name="value"> to be checked. </param>
        /// <returns> true if the number is a positive power of two otherwise false. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPow2(uint value)
        {
            unchecked
            {
#if HAS_INTRINSICS
                if (X86.Popcnt.IsSupported)
                    return 1 == X86.Popcnt.PopCount(value);

                if (Arm.AdvSimd.Arm64.IsSupported)
                {
                    // PopCount works on vector so convert input value to vector first.
                    Vector64<uint> input = Vector64.CreateScalar(value);
                    Vector64<byte> aggregated = Arm.AdvSimd.Arm64.AddAcross(Arm.AdvSimd.PopCount(input.AsByte()));
                    return 1 == aggregated.ToScalar();
                }
#endif
                return ((value & (value - 1)) == value) && value != 0;
            }
        }

        /// <summary>
        /// Is a value a positive power of two.
        /// </summary>
        /// <param name="value"> to be checked. </param>
        /// <returns> true if the number is a positive power of two otherwise false. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPow2(int value)
        {
            // TODO update with BitOperations final implementation dotnet/runtime/pull/36163
            if (value <= 0)
                return false;

            unchecked
            {
#if HAS_INTRINSICS
                if (X86.Popcnt.IsSupported)
                    return 1 == X86.Popcnt.PopCount((uint) value);

                if (Arm.AdvSimd.Arm64.IsSupported)
                {
                    // PopCount works on vector so convert input value to vector first.
                    Vector64<uint> input = Vector64.CreateScalar((uint) value);
                    Vector64<byte> aggregated = Arm.AdvSimd.Arm64.AddAcross(Arm.AdvSimd.PopCount(input.AsByte()));
                    return 1 == aggregated.ToScalar();
                }
#endif
                return ((value & (value - 1)) == value);
            }
        }
    }
}
