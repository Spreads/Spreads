// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Runtime.CompilerServices;

namespace Spreads.DataTypes
{
    public unsafe partial struct Variant : IComparable<Variant>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(Variant other)
        {
            if (TypeEnum != other.TypeEnum)
            {
                ThrowInvalidOperationExceptionTypesDoNotMatch(TypeEnum, other.TypeEnum);
            }

            // unwrap potentially boxed known types
            var typeEnum = TypeEnum;
            if ((int)typeEnum < KnownSmallTypesLimit)
            {
                switch (typeEnum)
                {
                    case TypeEnum.Int8:
                        return (Get<sbyte>()).CompareTo(other.Get<sbyte>());

                    case TypeEnum.Int16:
                        return (Get<short>()).CompareTo(other.Get<short>());

                    case TypeEnum.Int32:
                        return (Get<int>()).CompareTo(other.Get<int>());

                    case TypeEnum.Int64:
                        return (Get<long>()).CompareTo(other.Get<long>());

                    case TypeEnum.UInt8:
                        return (Get<byte>()).CompareTo(other.Get<byte>());

                    case TypeEnum.UInt16:
                        return (Get<ushort>()).CompareTo(other.Get<ushort>());

                    case TypeEnum.UInt32:
                        return (Get<uint>()).CompareTo(other.Get<uint>());

                    case TypeEnum.UInt64:
                        return (Get<ulong>()).CompareTo(other.Get<ulong>());

                    case TypeEnum.Float32:
                        return (Get<float>()).CompareTo(other.Get<float>());

                    case TypeEnum.Float64:
                        return (Get<double>()).CompareTo(other.Get<double>());

                    case TypeEnum.Decimal:
                        return (Get<decimal>()).CompareTo(other.Get<decimal>());

                    case TypeEnum.Price:
                        return (Get<Price>()).CompareTo(other.Get<Price>());

                    case TypeEnum.Money:
                        ThrowHelper.ThrowNotImplementedException();
                        return 0;
                    //return value.Get<Money>();

                    case TypeEnum.DateTime:
                        return (Get<DateTime>()).CompareTo(other.Get<DateTime>());

                    case TypeEnum.Timestamp:
                        return (Get<Timestamp>()).CompareTo(other.Get<Timestamp>());

                    case TypeEnum.Date:
                        ThrowHelper.ThrowNotImplementedException(); return 0;

                    case TypeEnum.Time:
                        ThrowHelper.ThrowNotImplementedException(); return 0;
                    case TypeEnum.Complex32:
                        ThrowHelper.ThrowNotImplementedException(); return 0;
                    case TypeEnum.Complex64:
                        ThrowHelper.ThrowNotImplementedException(); return 0;
                    case TypeEnum.Bool:
                        return (Get<bool>()).CompareTo(other.Get<bool>());

                    case TypeEnum.ErrorCode:
                        ThrowNotSupportedException(TypeEnum.ErrorCode);
                        goto default;

                    default:
                        ThrowHelper.ThrowArgumentOutOfRangeException(); return 0;
                }
            }

            // all other types are stored directly as objects
            if (_object != null)
            {
                if (_object is IComparable ic)
                {
                    return ic.CompareTo(other._object);
                }
            }

            ThrowHelper.ThrowNotImplementedException(); return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInvalidOperationExceptionTypesDoNotMatch(TypeEnum first, TypeEnum second)
        {
            throw new InvalidOperationException($"Variant types do not match: {first.ToString()} vs {second.ToString()}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowNotSupportedException(TypeEnum typeEnum)
        {
            throw new NotSupportedException($"Comparison not supported for the type {typeEnum.ToString()}");
        }

    }
}