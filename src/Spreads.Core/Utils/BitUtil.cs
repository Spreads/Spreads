// Mix of:
// https://github.com/dotnet/runtime/blob/master/src/libraries/System.Private.CoreLib/src/System/Numerics/BitOperations.cs
// https://raw.githubusercontent.com/AdaptiveConsulting/Aeron.NET/master/src/Adaptive.Agrona/BitUtil.cs

using System;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable InconsistentNaming

namespace Spreads.Utils
{
    /// <summary>
    /// Miscellaneous useful functions for dealing with low level bits and bytes.
    /// </summary>
    public class BitUtil
    {
        // C# no-alloc optimization that directly wraps the data section of the dll (similar to string constants)
        // https://github.com/dotnet/roslyn/pull/24621

        private static ReadOnlySpan<byte> TrailingZeroCountDeBruijn => new byte[32]
        {
            00, 01, 28, 02, 29, 14, 24, 03,
            30, 22, 20, 15, 25, 17, 04, 08,
            31, 27, 13, 23, 21, 19, 16, 07,
            26, 12, 18, 06, 11, 05, 10, 09
        };

        private static ReadOnlySpan<byte> Log2DeBruijn => new byte[32]
        {
            00, 09, 01, 10, 13, 21, 02, 29,
            11, 14, 16, 18, 22, 25, 03, 30,
            08, 12, 20, 28, 15, 17, 24, 07,
            19, 27, 23, 06, 26, 05, 04, 31
        };

        private static ReadOnlySpan<byte> HexDigitTable => new byte[]
        {
            (byte) '0', (byte) '1', (byte) '2', (byte) '3', (byte) '4', (byte) '5', (byte) '6', (byte) '7',
            (byte) '8', (byte) '9', (byte) 'a', (byte) 'b', (byte) 'c', (byte) 'd', (byte) 'e', (byte) 'f'
        };

        private static readonly byte[] FromHexDigitTable;

        static BitUtil()
        {
            FromHexDigitTable = new byte[128];
            FromHexDigitTable['0'] = 0x00;
            FromHexDigitTable['1'] = 0x01;
            FromHexDigitTable['2'] = 0x02;
            FromHexDigitTable['3'] = 0x03;
            FromHexDigitTable['4'] = 0x04;
            FromHexDigitTable['5'] = 0x05;
            FromHexDigitTable['6'] = 0x06;
            FromHexDigitTable['7'] = 0x07;
            FromHexDigitTable['8'] = 0x08;
            FromHexDigitTable['9'] = 0x09;
            FromHexDigitTable['a'] = 0x0a;
            FromHexDigitTable['A'] = 0x0a;
            FromHexDigitTable['b'] = 0x0b;
            FromHexDigitTable['B'] = 0x0b;
            FromHexDigitTable['c'] = 0x0c;
            FromHexDigitTable['C'] = 0x0c;
            FromHexDigitTable['d'] = 0x0d;
            FromHexDigitTable['D'] = 0x0d;
            FromHexDigitTable['e'] = 0x0e;
            FromHexDigitTable['E'] = 0x0e;
            FromHexDigitTable['f'] = 0x0f;
            FromHexDigitTable['F'] = 0x0f;
        }

        private const int LastDigitMask = 1;

        private static readonly Encoding Utf8Encoding = Encoding.UTF8;

        /// <summary>
        /// Fast method of finding the next power of 2 greater than or equal to the supplied value.
        ///
        /// If the value is &lt;= 0 then 1 will be returned.
        ///
        /// This method is not suitable for <seealso cref="int.MinValue"/> or numbers greater than 2^30.
        /// </summary>
        /// <param name="value"> from which to search for next power of 2 </param>
        /// <returns> The next power of 2 or the value itself if it is a power of 2 </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindNextPositivePowerOfTwo(int value)
        {
            unchecked
            {
                return 1 << (32 - NumberOfLeadingZeros(value - 1));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long FindNextPositivePowerOfTwo(long value)
        {
            unchecked
            {
                return 1L << (64 - NumberOfLeadingZeros(value - 1L));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindPreviousPositivePowerOfTwo(int value)
        {
            unchecked
            {
                return 1 << (31 - NumberOfLeadingZeros(value));
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
        /// Generate a byte array from the hex representation of the given byte array.
        /// </summary>
        /// <param name="buffer"> to convert from a hex representation (in Big Endian) </param>
        /// <returns> new byte array that is decimal representation of the passed array </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] FromHexByteArray(byte[] buffer)
        {
            byte[] outputBuffer = new byte[buffer.Length >> 1];

            for (int i = 0; i < buffer.Length; i += 2)
            {
                outputBuffer[i >> 1] = (byte) ((FromHexDigitTable[buffer[i]] << 4) | FromHexDigitTable[buffer[i + 1]]);
            }

            return outputBuffer;
        }

        /// <summary>
        /// Generate a byte array that is a hex representation of a given byte array.
        /// </summary>
        /// <param name="buffer"> to convert to a hex representation </param>
        /// <returns> new byte array that is hex representation (in Big Endian) of the passed array </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToHexByteArray(byte[] buffer)
        {
            return ToHexByteArray(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Generate a byte array that is a hex representation of a given byte array.
        /// </summary>
        /// <param name="buffer"> to convert to a hex representation </param>
        /// <param name="offset"> the offset into the buffer </param>
        /// <param name="length"> the number of bytes to convert </param>
        /// <returns> new byte array that is hex representation (in Big Endian) of the passed array </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] ToHexByteArray(byte[] buffer, int offset, int length)
        {
            var outputBuffer = new byte[length << 1];

            for (var i = 0; i < (length << 1); i += 2)
            {
                var b = buffer[offset + (i >> 1)];

                outputBuffer[i] = HexDigitTable[(b >> 4) & 0x0F];
                outputBuffer[i + 1] = HexDigitTable[b & 0x0F];
            }

            return outputBuffer;
        }

        /// <summary>
        /// Generate a byte array from a string that is the hex representation of the given byte array.
        /// </summary>
        /// <param name="value"> to convert from a hex representation (in Big Endian) </param>
        /// <returns> new byte array holding the decimal representation of the passed array </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] FromHex(string value)
        {
            return FromHexByteArray(Utf8Encoding.GetBytes(value));
        }

        /// <summary>
        /// Generate a string that is the hex representation of a given byte array.
        /// </summary>
        /// <param name="buffer"> to convert to a hex representation </param>
        /// <param name="offset"> the offset into the buffer </param>
        /// <param name="length"> the number of bytes to convert </param>
        /// <returns> new String holding the hex representation (in Big Endian) of the passed array </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHex(byte[] buffer, int offset, int length)
        {
            var hexByteArray = ToHexByteArray(buffer, offset, length);
            return Utf8Encoding.GetString(hexByteArray, 0, hexByteArray.Length);
        }

        /// <summary>
        /// Generate a string that is the hex representation of a given byte array.
        /// </summary>
        /// <param name="buffer"> to convert to a hex representation </param>
        /// <returns> new String holding the hex representation (in Big Endian) of the passed array </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToHex(byte[] buffer)
        {
            var hexByteArray = ToHexByteArray(buffer);
            return Utf8Encoding.GetString(hexByteArray, 0, hexByteArray.Length);
        }

        /// <summary>
        /// Is a number even.
        /// </summary>
        /// <param name="value"> to check. </param>
        /// <returns> true if the number is even otherwise false. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEven(int value)
        {
            return (value & LastDigitMask) == 0;
        }

        /// <summary>
        /// Is a value a positive power of two.
        /// </summary>
        /// <param name="value"> to be checked. </param>
        /// <returns> true if the number is a positive power of two otherwise false. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && ((value & (~value + 1)) == value);
        }

        /// <summary>
        /// Cycles indices of an array one at a time in a forward fashion
        /// </summary>
        /// <param name="current"> value to be incremented. </param>
        /// <param name="max">     value for the cycle. </param>
        /// <returns> the next value, or zero if max is reached. </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Next(int current, int max)
        {
            int next = current + 1;
            if (next == max)
            {
                next = 0;
            }

            return next;
        }

        /// <summary>
        /// Cycles indices of an array one at a time in a backwards fashion
        /// </summary>
        /// <param name="current"> value to be decremented. </param>
        /// <param name="max">     value of the cycle. </param>
        /// <returns> the next value, or max - 1 if current is zero </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Previous(int current, int max)
        {
            if (0 == current)
            {
                return max - 1;
            }

            return current - 1;
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
            if (!IsPowerOfTwo(alignment))
            {
                ThrowHelper.ThrowArgumentException("Alignment must be a power of 2: alignment=" + alignment);
            }

            return (address & (alignment - 1)) == 0;
        }

        /// <summary>
        /// Returns the number of zero bits following the lowest-order ("rightmost")
        /// one-bit in the two's complement binary representation of the specified
        /// {@code int} value.  Returns 32 if the specified value has no
        /// one-bits in its two's complement representation, in other words if it is
        /// equal to zero.
        /// </summary>
        /// <param name="i"> the value whose number of trailing zeros is to be computed </param>
        /// <returns> the number of zero bits following the lowest-order ("rightmost")
        ///     one-bit in the two's complement binary representation of the
        ///     specified {@code int} value, or 32 if the value is equal
        ///     to zero.
        /// @since 1.5 </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberOfTrailingZeros(int i)
        {
            // HD, Figure 5-14
            int y;
            if (i == 0)
            {
                return 32;
            }

            int n = 31;
            y = i << 16;
            if (y != 0)
            {
                n = n - 16;
                i = y;
            }

            y = i << 8;
            if (y != 0)
            {
                n = n - 8;
                i = y;
            }

            y = i << 4;
            if (y != 0)
            {
                n = n - 4;
                i = y;
            }

            y = i << 2;
            if (y != 0)
            {
                n = n - 2;
                i = y;
            }

            return n - ((int) ((uint) (i << 1) >> 31));
        }

        /// <summary>
        /// Note Olivier: Direct port of the Java method Integer.NumberOfLeadingZeros
        ///
        /// Returns the number of zero bits preceding the highest-order
        /// ("leftmost") one-bit in the two's complement binary representation
        /// of the specified {@code int} value.  Returns 32 if the
        /// specified value has no one-bits in its two's complement representation,
        /// in other words if it is equal to zero.
        ///
        /// <para>Note that this method is closely related to the logarithm base 2.
        /// For all positive {@code int} values x:
        /// &lt;ul&gt;
        /// &lt;li&gt;floor(log&lt;sub&gt;2&lt;/sub&gt;(x)) = {@code 31 - numberOfLeadingZeros(x)}
        /// &lt;li&gt;ceil(log&lt;sub&gt;2&lt;/sub&gt;(x)) = {@code 32 - numberOfLeadingZeros(x - 1)}
        /// &lt;/ul&gt;
        ///
        /// </para>
        /// </summary>
        /// <param name="i"> the value whose number of leading zeros is to be computed </param>
        /// <returns> the number of zero bits preceding the highest-order
        ///     ("leftmost") one-bit in the two's complement binary representation
        ///     of the specified {@code int} value, or 32 if the value
        ///     is equal to zero.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberOfLeadingZeros(int i)
        {
            unchecked
            {
#if NETCOREAPP3_0
                if (System.Runtime.Intrinsics.X86.Lzcnt.IsSupported)
                {
                    return (int) System.Runtime.Intrinsics.X86.Lzcnt.LeadingZeroCount((uint) i);
                }
#endif

                unchecked
                {
                    // HD, Figure 5-6
                    if (i == 0)
                    {
                        return 32;
                    }

                    int n = 1;
                    if ((int) ((uint) i >> 16) == 0)
                    {
                        n += 16;
                        i <<= 16;
                    }

                    if ((int) ((uint) i >> 24) == 0)
                    {
                        n += 8;
                        i <<= 8;
                    }

                    if ((int) ((uint) i >> 28) == 0)
                    {
                        n += 4;
                        i <<= 4;
                    }

                    if ((int) ((uint) i >> 30) == 0)
                    {
                        n += 2;
                        i <<= 2;
                    }

                    n -= (int) ((uint) i >> 31);
                    return n;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int NumberOfLeadingZeros(long i)
        {
            unchecked
            {
#if NETCOREAPP3_0
                if (System.Runtime.Intrinsics.X86.Lzcnt.X64.IsSupported)
                {
                    return (int) System.Runtime.Intrinsics.X86.Lzcnt.X64.LeadingZeroCount((ulong) i);
                }
#endif

                unchecked
                {
                    // HD, Figure 5-6
                    if (i == 0L)
                    {
                        return 64;
                    }

                    int n = 1;
                    if ((long) ((ulong) i >> 32) == 0)
                    {
                        n += 32;
                        i <<= 32;
                    }

                    if ((long) ((ulong) i >> 48) == 0)
                    {
                        n += 16;
                        i <<= 16;
                    }

                    if ((long) ((ulong) i >> 56) == 0)
                    {
                        n += 8;
                        i <<= 8;
                    }

                    if ((long) ((ulong) i >> 60) == 0)
                    {
                        n += 4;
                        i <<= 4;
                    }

                    if ((long) ((ulong) i >> 62) == 0)
                    {
                        n += 2;
                        i <<= 2;
                    }

                    n -= (int) ((ulong) i >> 63);
                    return n;
                }
            }
        }
    }
}