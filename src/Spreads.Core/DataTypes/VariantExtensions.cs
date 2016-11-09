using System;

namespace Spreads.DataTypes
{
    public static class VariantExtensions {
        public static Variant AsVariant<T>(this T value) {
            return Variant.Create<T>(value);
        }

        public static Variant AsVariant(this object value) {
            return Variant.Create(value);
        }

        public static void WriteToMemory(this Variant value, Memory<byte> memory) {



        }
    }
}