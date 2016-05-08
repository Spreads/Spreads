using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spreads.Storage.Aeron {

    /**
     * Miscellaneous useful functions for dealing with low level bits and bytes.
     */
    public class BitUtil {
        [ThreadStatic]
        private static System.Random _rng;

        /**
         * Size of a byte in bytes
         */
        public const int SIZE_OF_BYTE = 1;

        /**
         * Size of a boolean in bytes
         */
        public const int SIZE_OF_BOOLEAN = 1;

        /**
         * Size of a char in bytes
         */
        public const int SIZE_OF_CHAR = 2;

        /**
         * Size of a short in bytes
         */
        public const int SIZE_OF_SHORT = 2;

        /**
         * Size of an int in bytes
         */
        public const int SIZE_OF_INT = 4;

        /**
         * Size of a a float in bytes
         */
        public const int SIZE_OF_FLOAT = 4;

        /**
         * Size of a long in bytes
         */
        public const int SIZE_OF_LONG = 8;

        /**
         * Size of a double in bytes
         */
        public const int SIZE_OF_DOUBLE = 8;

        /**
         * Length of the data blocks used by the CPU cache sub-system in bytes.
         */
        public const int CACHE_LINE_LENGTH = 64;

        private static byte[] HEX_DIGIT_TABLE =
        {
            (byte)'0',
            (byte)'1',
            (byte)'2',
            (byte)'3',
            (byte)'4',
            (byte)'5',
            (byte)'6',
            (byte)'7',
            (byte)'8',
            (byte)'9',
            (byte)'a',
            (byte)'b',
            (byte)'c',
            (byte)'d',
            (byte)'e',
            (byte)'f'
    };

        private static byte[] FROM_HEX_DIGIT_TABLE;

        static BitUtil() {
            FROM_HEX_DIGIT_TABLE = new byte[128];
            FROM_HEX_DIGIT_TABLE['0'] = 0x00;
            FROM_HEX_DIGIT_TABLE['1'] = 0x01;
            FROM_HEX_DIGIT_TABLE['2'] = 0x02;
            FROM_HEX_DIGIT_TABLE['3'] = 0x03;
            FROM_HEX_DIGIT_TABLE['4'] = 0x04;
            FROM_HEX_DIGIT_TABLE['5'] = 0x05;
            FROM_HEX_DIGIT_TABLE['6'] = 0x06;
            FROM_HEX_DIGIT_TABLE['7'] = 0x07;
            FROM_HEX_DIGIT_TABLE['8'] = 0x08;
            FROM_HEX_DIGIT_TABLE['9'] = 0x09;
            FROM_HEX_DIGIT_TABLE['a'] = 0x0a;
            FROM_HEX_DIGIT_TABLE['A'] = 0x0a;
            FROM_HEX_DIGIT_TABLE['b'] = 0x0b;
            FROM_HEX_DIGIT_TABLE['B'] = 0x0b;
            FROM_HEX_DIGIT_TABLE['c'] = 0x0c;
            FROM_HEX_DIGIT_TABLE['C'] = 0x0c;
            FROM_HEX_DIGIT_TABLE['d'] = 0x0d;
            FROM_HEX_DIGIT_TABLE['D'] = 0x0d;
            FROM_HEX_DIGIT_TABLE['e'] = 0x0e;
            FROM_HEX_DIGIT_TABLE['E'] = 0x0e;
            FROM_HEX_DIGIT_TABLE['f'] = 0x0f;
            FROM_HEX_DIGIT_TABLE['F'] = 0x0f;
        }

        private const int LAST_DIGIT_MASK = 1; //0b1;


        public static int numberOfLeadingZeros(int i) {
            // HD, Figure 5-6
            if (i == 0)
                return 32;
            int n = 1;
            if (i >> 16 == 0) { n += 16; i <<= 16; }
            if (i >> 24 == 0) { n += 8; i <<= 8; }
            if (i >> 28 == 0) { n += 4; i <<= 4; }
            if (i >> 30 == 0) { n += 2; i <<= 2; }
            n -= i >> 31;
            return n;
        }


        public static int numberOfTrailingZeros(int i) {
            // HD, Figure 5-14
            int y;
            if (i == 0) return 32;
            int n = 31;
            y = i << 16; if (y != 0) { n = n - 16; i = y; }
            y = i << 8; if (y != 0) { n = n - 8; i = y; }
            y = i << 4; if (y != 0) { n = n - 4; i = y; }
            y = i << 2; if (y != 0) { n = n - 2; i = y; }
            return n - ((i << 1) >> 31);
        }

        private static Encoding UTF8_CHARSET = System.Text.Encoding.UTF8;

        /**
         * Fast method of finding the next power of 2 greater than or equal to the supplied value.
         *
         * If the value is &lt;= 0 then 1 will be returned.
         *
         * This method is not suitable for {@link Integer#MIN_VALUE} or numbers greater than 2^30.
         *
         * @param value from which to search for next power of 2
         * @return The next power of 2 or the value itself if it is a power of 2
         */
        public static int findNextPositivePowerOfTwo(int value) {
            return 1 << (32 - numberOfLeadingZeros(value - 1));
        }

        /**
         * Align a value to the next multiple up of alignment.
         * If the value equals an alignment multiple then it is returned unchanged.
         * <p>
         * This method executes without branching. This code is designed to be use in the fast path and should not
         * be used with negative numbers. Negative numbers will result in undefined behaviour.
         *
         * @param value     to be aligned up.
         * @param alignment to be used.
         * @return the value aligned to the next boundary.
         */
        public static int align(int value, int alignment) {
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        /**
         * Generate a byte array from the hex representation of the given byte array.
         *
         * @param buffer to convert from a hex representation (in Big Endian)
         * @return new byte array that is decimal representation of the passed array
         */
        public static byte[] fromHexByteArray(byte[] buffer) {
            byte[] outputBuffer = new byte[buffer.Length >> 1];

            for (int i = 0; i < buffer.Length; i += 2) {
                outputBuffer[i >> 1] =
                    (byte)((FROM_HEX_DIGIT_TABLE[buffer[i]] << 4) | FROM_HEX_DIGIT_TABLE[buffer[i + 1]]);
            }

            return outputBuffer;
        }

        /**
         * Generate a byte array that is a hex representation of a given byte array.
         *
         * @param buffer to convert to a hex representation
         * @return new byte array that is hex representation (in Big Endian) of the passed array
         */
        public static byte[] toHexByteArray(byte[] buffer) {
            return toHexByteArray(buffer, 0, buffer.Length);
        }

        /**
         * Generate a byte array that is a hex representation of a given byte array.
         *
         * @param buffer to convert to a hex representation
         * @param offset the offset into the buffer
         * @param length the number of bytes to convert
         * @return new byte array that is hex representation (in Big Endian) of the passed array
         */
        public static byte[] toHexByteArray(byte[] buffer, int offset, int length) {
            byte[] outputBuffer = new byte[length << 1];

            for (int i = 0; i < (length << 1); i += 2) {
                byte b = buffer[offset + (i >> 1)];

                outputBuffer[i] = HEX_DIGIT_TABLE[(b >> 4) & 0x0F];
                outputBuffer[i + 1] = HEX_DIGIT_TABLE[b & 0x0F];
            }

            return outputBuffer;
        }

        /**
         * Generate a byte array from a string that is the hex representation of the given byte array.
         *
         * @param string to convert from a hex representation (in Big Endian)
         * @return new byte array holding the decimal representation of the passed array
         */
        public static byte[] fromHex(string value) {
            return fromHexByteArray(UTF8_CHARSET.GetBytes(value));
        }

        /**
         * Generate a string that is the hex representation of a given byte array.
         *
         * @param buffer to convert to a hex representation
         * @param offset the offset into the buffer
         * @param length the number of bytes to convert
         * @return new String holding the hex representation (in Big Endian) of the passed array
         */
        public static String toHex(byte[] buffer, int offset, int length) {
            return UTF8_CHARSET.GetString(toHexByteArray(buffer, offset, length));
        }

        /**
         * Generate a string that is the hex representation of a given byte array.
         *
         * @param buffer to convert to a hex representation
         * @return new String holding the hex representation (in Big Endian) of the passed array
         */
        public static String toHex(byte[] buffer) {
            return UTF8_CHARSET.GetString(toHexByteArray(buffer));
        }

        /**
         * Is a number even.
         *
         * @param value to check.
         * @return true if the number is even otherwise false.
         */
        public static bool isEven(int value) {
            return (value & LAST_DIGIT_MASK) == 0;
        }

        /**
         * Is a value a positive power of two.
         *
         * @param value to be checked.
         * @return true if the number is a positive power of two otherwise false.
         */
        public static bool isPowerOfTwo(int value) {
            return value > 0 && ((value & (~value + 1)) == value);
        }

        /**
         * Cycles indices of an array one at a time in a forward fashion
         *
         * @param current value to be incremented.
         * @param max     value for the cycle.
         * @return the next value, or zero if max is reached.
         */
        public static int next(int current, int max) {
            int next = current + 1;
            if (next == max) {
                next = 0;
            }

            return next;
        }

        /**
         * Cycles indices of an array one at a time in a backwards fashion
         *
         * @param current value to be decremented.
         * @param max     value of the cycle.
         * @return the next value, or max - 1 if current is zero
         */
        public static int previous(int current, int max) {
            if (0 == current) {
                return max - 1;
            }

            return current - 1;
        }

        /**
         * Calculate the shift value to scale a number based on how refs are compressed or not.
         *
         * @param scale of the number reported by Unsafe.
         * @return how many times the number needs to be shifted to the left.
         */
        public static int calculateShiftForScale(int scale) {
            if (4 == scale) {
                return 2;
            } else if (8 == scale) {
                return 3;
            } else {
                throw new ArgumentException("Unknown pointer size");
            }
        }

        /**
         * Generate a randomized integer over [{@link Integer#MIN_VALUE}, {@link Integer#MAX_VALUE}] suitable for
         * use as an Aeron Id.
         *
         * @return randomized integer suitable as an Id.
         */
        public static int generateRandomisedId() {
            if (_rng == null) _rng = new Random();
            return _rng.Next(int.MinValue, int.MaxValue);
        }
    }

}
