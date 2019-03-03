// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.InteropServices;
using Spreads.DataTypes;
using Spreads.Utils;
using static System.Runtime.CompilerServices.Unsafe;

namespace Spreads.Serialization.Experimental
{
    internal static unsafe class TypeEnumHelper
    {
        public static readonly short* Sizes; // = new short[256];

        static TypeEnumHelper()
        {
            var allocSize = SizeOf<short>() * 256 + 64;
            // Never free until process dies.
            var ptr = Marshal.AllocHGlobal(allocSize);
            (new Span<byte>((void*)ptr, allocSize)).Clear();

            var alignedPtr = (IntPtr)BitUtil.Align((long)ptr, 64);

            Sizes = (short*)alignedPtr;

            #region Known fixed-size scalar types

            // ReSharper disable once PossibleNullReferenceException
            Sizes[(byte)TypeEnumEx.None] = 0;

            Sizes[(byte)TypeEnumEx.Int8] = (short)SizeOf<sbyte>();
            Sizes[(byte)TypeEnumEx.Int16] = (short)SizeOf<short>();
            Sizes[(byte)TypeEnumEx.Int32] = (short)SizeOf<int>();
            Sizes[(byte)TypeEnumEx.Int64] = (short)SizeOf<long>();
            Sizes[(byte)TypeEnumEx.Int128] = 16;

            Sizes[(byte)TypeEnumEx.UInt8] = (short)SizeOf<byte>();
            Sizes[(byte)TypeEnumEx.UInt16] = (short)SizeOf<ushort>();
            Sizes[(byte)TypeEnumEx.UInt32] = (short)SizeOf<uint>();
            Sizes[(byte)TypeEnumEx.UInt64] = (short)SizeOf<ulong>();
            Sizes[(byte)TypeEnumEx.UInt128] = 16;

            Sizes[(byte)TypeEnumEx.Float16] = 2;
            Sizes[(byte)TypeEnumEx.Float32] = (short)SizeOf<float>();
            Sizes[(byte)TypeEnumEx.Float64] = (short)SizeOf<double>();
            Sizes[(byte)TypeEnumEx.Float128] = 16;

            Sizes[(byte)TypeEnumEx.Decimal32] = 4;
            Sizes[(byte)TypeEnumEx.Decimal64] = 8;
            Sizes[(byte)TypeEnumEx.Decimal128] = 16;

            Sizes[(byte)TypeEnumEx.DecimalDotNet] = 16;
            Sizes[(byte)TypeEnumEx.SmallDecimal] = SmallDecimal.Size;

            Sizes[(byte)TypeEnumEx.Money] = Money.Size;

            Sizes[(byte)TypeEnumEx.Bool] = 1;
            Sizes[(byte)TypeEnumEx.Utf16Char] = 2;
            Sizes[(byte)TypeEnumEx.Symbol] = Symbol.Size;

            Sizes[(byte)TypeEnumEx.DateTime] = 8;
            Sizes[(byte)TypeEnumEx.Timestamp] = Timestamp.Size;


            #endregion

            for (int i = 1; i < 63; i++)
            {
                if (unchecked((uint)Sizes[i]) > 16)
                {
                    ThrowHelper.FailFast($"Sizes[{i}] == {Sizes[i]} > 16");
                }
            }

            for (int i = 64; i <= 127; i++)
            {
                Sizes[i] = -1;
            }

            for (int i = 1; i <= TypeEnumOrFixedSize.MaxFixedSize; i++)
            {
                Sizes[TypeEnumOrFixedSize.MaxTypeEnum + i] = (byte)(i);
            }
        }
    }
}