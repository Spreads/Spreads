using System;
using System.Runtime.CompilerServices;
using Spreads.DataTypes;
using Spreads.Serialization;

namespace Spreads {


    public partial struct Variant {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeEnum GetTypeCode<T>()
        {
            // typeof(T) == typeof() is compile-time const

            if (typeof(T) == typeof(bool)) return TypeEnum.Bool;
            if (typeof(T) == typeof(byte)) return TypeEnum.UInt8;
            if (typeof(T) == typeof(char)) return TypeEnum.UInt16; // as UInt16
            if (typeof(T) == typeof(sbyte)) return TypeEnum.Int8;
            if (typeof(T) == typeof(short)) return TypeEnum.Int16;
            if (typeof(T) == typeof(ushort)) return TypeEnum.UInt16;
            if (typeof(T) == typeof(int)) return TypeEnum.Int32;
            if (typeof(T) == typeof(uint)) return TypeEnum.UInt32;
            if (typeof(T) == typeof(long)) return TypeEnum.Int64;
            if (typeof(T) == typeof(ulong)) return TypeEnum.UInt64;
            if (typeof(T) == typeof(IntPtr)) return TypeEnum.Int64;
            if (typeof(T) == typeof(UIntPtr)) return TypeEnum.UInt64;
            if (typeof(T) == typeof(float)) return TypeEnum.Float32;
            if (typeof(T) == typeof(double)) return TypeEnum.Float64;
            if (typeof(T) == typeof(decimal)) return TypeEnum.Decimal;
            if (typeof(T) == typeof(Price)) return TypeEnum.Price;



            if (typeof(T) == typeof(Variant)) return TypeEnum.Variant;
            if (typeof(T) == typeof(Array)) return TypeEnum.Array; // TODO check if typeof(T).IsArray is any different

            // TODO known types, otherwise will fallback to fixed binary

#pragma warning disable 618
            if (TypeHelper<T>.Size > 0) return TypeEnum.FixedBinary;
#pragma warning restore 618

            return TypeEnum.Object;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static TypeEnum GetTypeCode(Type ty) {
            // typeof(T) == typeof() is compile-time const

            if (ty == typeof(bool)) return TypeEnum.Bool;
            if (ty == typeof(byte)) return TypeEnum.UInt8;
            if (ty == typeof(char)) return TypeEnum.UInt16; // as UInt16
            if (ty == typeof(sbyte)) return TypeEnum.Int8;
            if (ty == typeof(short)) return TypeEnum.Int16;
            if (ty == typeof(ushort)) return TypeEnum.UInt16;
            if (ty == typeof(int)) return TypeEnum.Int32;
            if (ty == typeof(uint)) return TypeEnum.UInt32;
            if (ty == typeof(long)) return TypeEnum.Int64;
            if (ty == typeof(ulong)) return TypeEnum.UInt64;
            if (ty == typeof(IntPtr)) return TypeEnum.Int64;
            if (ty == typeof(UIntPtr)) return TypeEnum.UInt64;
            if (ty == typeof(float)) return TypeEnum.Float32;
            if (ty == typeof(double)) return TypeEnum.Float64;
            if (ty == typeof(decimal)) return TypeEnum.Decimal;
            if (ty == typeof(Price)) return TypeEnum.Price;


            if (ty == typeof(Variant)) return TypeEnum.Variant;
            if (ty.IsArray) return TypeEnum.Array; // TODO check if ty.IsArray is any different

            // TODO known types, otherwise will fallback to fixed binary

#pragma warning disable 618
            if (TypeHelper.GetSize(ty) > 0) return TypeEnum.FixedBinary;
#pragma warning restore 618

            return TypeEnum.Object;
        }

        #region Known Type Sizes

        private static readonly byte[] KnownTypeSizes = new byte[198]
        {
            // Unknown
            0, // 0
            // Int
            1,
            2,
            4,
            8,
            1, // 5
            2,
            4,
            8,
            // Float
            4,
            8, // 0
            
            16, // Decimal 
            8,  // Price
            0, // TODO Money not implemented
            
            // DateTime
            8,
            8, // 5
            4,
            4, 

            // Complex
            8,
            16,

            // Symbols
            0, // 20 TODO Symbol8, 32-128 
            0, // 
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            0,
            0,
            0,
            0, // 0
            0,
            0,
            0,
            0,
            0, // 5
            0,
            1, // 197 - bool

        };
        #endregion

    }
}
