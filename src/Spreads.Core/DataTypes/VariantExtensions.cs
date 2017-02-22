using System;
using System.Collections.Generic;

namespace Spreads.DataTypes {
    public static class VariantExtensions {
        public static Variant AsVariant<T>(this T value) {
            return Variant.Create<T>(value);
        }

        public static Variant AsVariant(this object value) {
            return Variant.Create(value);
        }

        public static object ToObject(this Variant value) {
            return Variant.ToObject(value);
        }

        public static void WriteToMemory(this Variant value, Memory<byte> memory) {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Compare string Variants with StringComparison.InvariantCultureIgnoreCase
    /// </summary>
    public class CaseInsensitiveVariantEqualityComparer : IEqualityComparer<Variant> {
        public bool Equals(Variant x, Variant y) {
            if (x.TypeEnum == TypeEnum.String && y.TypeEnum == TypeEnum.String) {
                return x.Get<string>().Equals(y.Get<string>(), StringComparison.OrdinalIgnoreCase);
            }
            return x.Equals(y);
        }

        public int GetHashCode(Variant obj) {
            return obj.GetHashCode();
        }
    }
}